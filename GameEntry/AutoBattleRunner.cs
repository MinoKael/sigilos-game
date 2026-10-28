using System;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Progression;
using Sigilos.UI;
using Sigilos.UI.Audio;
using static Sigilos.UI.Locale;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Roda a Batalha automática fora das telas: é um nó do GameRoot, vivo o tempo todo, então trocar de
	/// tela, abrir as Runas ou fechar a janela não para nada.
	///
	/// Cada luta começa no servidor (<see cref="Start"/> recebe como): ele a decide na hora e dá o bilhete, mas só paga
	/// no fim, depois do tempo que ela levaria na tela no automático (<see cref="AutoFight.Finish"/>); então vem a
	/// próxima. Para no fim das lutas, com o botão Parar, ou quando a próxima não pode começar (Mana, inventário de
	/// runas cheio) — nesses dois casos o jogador resolve e retoma de onde parou. Parar no meio de uma luta descarta
	/// essa luta: nada ganho, nada gasto. Quem mostra é o aviso flutuante e a janela (UI), pelo
	/// <see cref="AutoBattleRun"/>; a luta de agora anda no mesmo relógio, para ver (<see cref="AutoBattleFight"/>), sem
	/// mexer no resultado. As lutas não soam; só o começo (e a retomada) e a parada.
	/// </summary>
	public partial class AutoBattleRunner : Node
	{
		private Func<EntryProblem> _check = () => EntryProblem.None;
		private Func<Task<AutoFight?>> _fight = () => throw new InvalidOperationException();
		private Func<Task<AutoSettled?>>? _finish;
		private Action _save = () => { };
		private int _mana;
		private bool _waiting;

		public AutoBattleRunner()
		{
			Name = "AutoBattleRunner";
			ProcessMode = ProcessModeEnum.Always;
		}

		/// <summary>A Batalha automática de agora (rodando, parada ou acabada); nula quando não há nenhuma.</summary>
		public AutoBattleRun? Run { get; private set; }

		/// <summary>Uma Batalha automática está lutando agora.</summary>
		public bool Running => Run is { Running: true };

		/// <summary>O relógio parado (o jogo na conta parou sem conexão): a luta de agora não anda nem termina.</summary>
		public bool Held { get; set; }

		/// <summary>Começou uma nova, ou a de agora foi dispensada (nula).</summary>
		public event Action<AutoBattleRun?>? RunChanged;

		/// <summary>Uma vitória foi paga: o nível, a experiência e as moedas da conta mudaram.</summary>
		public event Action? Rewarded;

		/// <param name="check">Se a próxima luta pode começar (sem cobrar nada).</param>
		/// <param name="fight">Começa uma luta no servidor: se venceu, quantos segundos ela leva na tela, a mesma luta para ver e como terminá-la; nula se não deu para começar.</param>
		public void Start(AutoBattleRun run, Func<EntryProblem> check, Func<Task<AutoFight?>> fight, Action save)
		{
			Run = run;
			_check = check;
			_fight = fight;
			_finish = null;
			_save = save;
			_mana = run.Mana;
			run.Running = true;
			RunChanged?.Invoke(run);
			Sfx.Play("progression.auto_battle_start");
			Next();
		}

		/// <summary>O botão Parar: descarta a luta de agora e deixa retomar o que falta.</summary>
		public void Stop()
		{
			if (Run is not { Running: true })
				return;
			Halt(T("auto.stopped"), canResume: true);
		}

		public void Resume()
		{
			if (Run is not { Running: false, CanResume: true } run)
				return;
			run.Running = true;
			run.StopReason = null;
			run.CanResume = false;
			Sfx.Play("progression.auto_battle_start");
			Next();
		}

		/// <summary>Muda quantas lutas fazer, no meio: nunca abaixo da luta de agora.</summary>
		public void SetRuns(int runs)
		{
			if (Run is not { } run)
				return;
			run.Runs = Math.Max(Math.Max(1, run.Number), runs);
			run.Notify();
		}

		/// <summary>Encerra de vez a Batalha automática (parada ou acabada): o aviso some.</summary>
		public void Dismiss()
		{
			if (Run is { Running: true })
				Halt(T("auto.stopped"), canResume: false);
			Run = null;
			RunChanged?.Invoke(null);
		}

		public override void _Process(double delta)
		{
			if (Held || _waiting || Run is not { Running: true } run || _finish is not { } finish)
				return;

			run.Elapsed += delta;
			run.Fight?.Advance(run.Elapsed);
			if (run.Elapsed < run.Duration)
				return;

			_finish = null;
			Settle(run, finish);
		}

		/// <summary>
		/// O fim da luta de agora: o servidor confere o bilhete e o tempo e, na vitória, paga. Se não deu para terminar
		/// (sem conexão, bilhete vencido), a luta não conta e a corrida para, com retomada.
		/// </summary>
		private async void Settle(AutoBattleRun run, Func<Task<AutoSettled?>> finish)
		{
			_waiting = true;
			AutoSettled? settled;
			try
			{
				settled = await finish();
			}
			finally
			{
				_waiting = false;
			}

			// O servidor pagou mesmo que a corrida tenha parado enquanto ele respondia: a vitória entra.
			if (settled is { Victory: true, Reward: { } reward })
			{
				if (Run == run)
					run.Add(reward);
				_save();
				Rewarded?.Invoke();
			}

			if (Run != run || !run.Running)
				return;

			if (settled == null)
			{
				Halt(T("serverBattle.offline"), canResume: true);
				return;
			}

			if (settled.Problem != EntryProblem.None)
			{
				Halt(Texts.Refusal(settled.Problem, _mana), canResume: settled.Problem is EntryProblem.NoMana or EntryProblem.RunesFull);
				return;
			}

			run.Record(run.Duration);
			if (!settled.Victory)
				run.Defeats++;
			Next();
		}

		/// <summary>A próxima luta, ou o fim: acabaram as lutas, ou a próxima não pode começar.</summary>
		private async void Next()
		{
			if (Run is not { } run)
				return;

			if (run.Done >= run.Runs)
			{
				Halt(T("auto.done", run.Done), canResume: false);
				return;
			}

			var problem = _check();
			if (problem != EntryProblem.None)
			{
				Halt(Texts.Refusal(problem, _mana), canResume: problem is EntryProblem.NoMana or EntryProblem.RunesFull);
				return;
			}

			if (_waiting)
				return;

			_waiting = true;
			AutoFight? result;
			try
			{
				result = await _fight();
			}
			finally
			{
				_waiting = false;
			}

			// Parou enquanto o servidor começava: a luta começada fica sem fim e não paga nada.
			if (Run != run || !run.Running)
				return;

			if (result == null)
			{
				Halt(T("serverBattle.offline"), canResume: true);
				return;
			}

			if (result.Problem != EntryProblem.None)
			{
				Halt(Texts.Refusal(result.Problem, _mana), canResume: result.Problem is EntryProblem.NoMana or EntryProblem.RunesFull);
				return;
			}

			_finish = result.Finish;
			run.Fight = result.Fight;
			run.Number = run.Done + 1;
			run.Duration = Math.Max(0.1, result.Seconds);
			run.Elapsed = 0;
			run.Notify();
		}

		private void Halt(string reason, bool canResume)
		{
			if (Run is not { } run)
				return;
			_finish = null;
			run.Running = false;
			run.StopReason = reason;
			run.CanResume = canResume && run.Done < run.Runs;
			run.Number = run.Done;
			run.Elapsed = 0;
			run.Fight = null;
			run.Notify();
			Sfx.Play("progression.auto_battle_end");
		}
	}

	/// <summary>
	/// Uma luta do automático começada no servidor: o resultado que ele já decidiu, quanto ela leva na tela, a mesma luta
	/// para ver e como pedir o fim dela (que paga).
	/// </summary>
	public sealed record AutoFight(bool Victory, double Seconds, AutoBattleFight? Fight, Func<Task<AutoSettled?>>? Finish, EntryProblem Problem);

	/// <summary>
	/// O fim de uma luta do automático: se venceu e a recompensa paga, ou a porta que fechou no meio (a Mana que acabou,
	/// o inventário de runas cheio), e a luta não conta.
	/// </summary>
	public sealed record AutoSettled(bool Victory, VictoryReward? Reward, EntryProblem Problem = EntryProblem.None);
}
