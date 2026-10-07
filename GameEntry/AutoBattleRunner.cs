using System;
using Godot;
using Sigilos.Core.Progression;
using Sigilos.UI;
using static Sigilos.UI.Locale;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Roda a Batalha automática fora das telas: é um nó do GameRoot, vivo o tempo todo, então trocar de
	/// tela, abrir as Runas ou fechar a janela não para nada.
	///
	/// Cada luta é resolvida na hora (<see cref="Start"/> recebe como), mas a recompensa só entra depois do
	/// tempo que ela levaria na tela no automático em 2×; então vem a próxima. Para no fim das lutas, com o
	/// botão Parar, ou quando a próxima não pode começar (Mana, inventário de runas cheio) — nesses dois
	/// casos o jogador resolve e retoma de onde parou. Parar no meio de uma luta descarta essa luta: nada
	/// ganho, nada gasto. Quem mostra é o aviso flutuante e a janela (UI), pelo <see cref="AutoBattleRun"/>;
	/// a luta de agora anda no mesmo relógio, para ver (<see cref="AutoBattleFight"/>), sem mexer no resultado.
	/// </summary>
	public partial class AutoBattleRunner : Node
	{
		private Func<EntryProblem> _check = () => EntryProblem.None;
		private Func<(bool Victory, double Seconds, AutoBattleFight Fight)> _fight = () => throw new InvalidOperationException();
		private Func<VictoryReward> _victory = () => throw new InvalidOperationException();
		private Action _save = () => { };
		private int _mana;
		private bool _victoryPending;

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

		/// <param name="check">Se a próxima luta pode começar (sem cobrar nada).</param>
		/// <param name="fight">Resolve uma luta inteira no automático: se venceu, quantos segundos ela levaria na tela e a mesma luta para ver.</param>
		/// <param name="victory">Aplica a vitória (cobra a Mana, entrega a recompensa).</param>
		public void Start(AutoBattleRun run, Func<EntryProblem> check, Func<(bool Victory, double Seconds, AutoBattleFight Fight)> fight, Func<VictoryReward> victory, Action save)
		{
			Run = run;
			_check = check;
			_fight = fight;
			_victory = victory;
			_save = save;
			_mana = run.Mana;
			run.Running = true;
			RunChanged?.Invoke(run);
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
			if (Held || Run is not { Running: true } run)
				return;

			run.Elapsed += delta;
			run.Fight?.Advance(run.Elapsed);
			if (run.Elapsed < run.Duration)
				return;

			run.Record(run.Duration);
			if (_victoryPending)
			{
				run.Add(_victory());
				_save();
			}
			else
			{
				run.Defeats++;
			}

			Next();
		}

		/// <summary>A próxima luta, ou o fim: acabaram as lutas, ou a próxima não pode começar.</summary>
		private void Next()
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

			var (victory, seconds, fight) = _fight();
			_victoryPending = victory;
			run.Fight = fight;
			run.Number = run.Done + 1;
			run.Duration = Math.Max(0.1, seconds);
			run.Elapsed = 0;
			run.Notify();
		}

		private void Halt(string reason, bool canResume)
		{
			if (Run is not { } run)
				return;
			run.Running = false;
			run.StopReason = reason;
			run.CanResume = canResume && run.Done < run.Runs;
			run.Number = run.Done;
			run.Elapsed = 0;
			run.Fight = null;
			_victoryPending = false;
			run.Notify();
		}
	}
}
