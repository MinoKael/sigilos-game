using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.UI
{
	/// <summary>
	/// Uma Batalha automática em andamento (ou acabada): o que a tela precisa mostrar dela. Quem roda as
	/// lutas e muda estes números é o GameRoot (GameEntry/AutoBattleRunner); o aviso flutuante e a janela
	/// da Batalha automática só leem e escutam <see cref="Changed"/>.
	///
	/// A Batalha automática vive fora das telas: sair de uma tela, abrir as Runas ou fechar a janela não
	/// a interrompe. Só o botão Parar a interrompe (ou o fim das lutas, a falta de Mana, o inventário de
	/// runas cheio).
	/// </summary>
	public sealed class AutoBattleRun
	{
		private readonly List<double> _durations = new();

		public AutoBattleRun(string title, string where, int mana, int runs)
		{
			Title = title;
			Where = where;
			Mana = mana;
			Runs = runs;
		}

		/// <summary>O nome da luta ("Fase 12 · Stone Gate").</summary>
		public string Title { get; }

		/// <summary>Chave do conteúdo (a equipe que luta: "campaign" ou o id da Masmorra).</summary>
		public string Where { get; }

		/// <summary>A Mana de uma vitória.</summary>
		public int Mana { get; }

		/// <summary>Quantas lutas o jogador pediu. Muda no meio (a janela deixa aumentar ou diminuir).</summary>
		public int Runs { get; set; }

		/// <summary>A luta em andamento (1 a <see cref="Runs"/>); 0 antes da primeira.</summary>
		public int Number { get; set; }

		public int Victories { get; set; }
		public int Defeats { get; set; }
		public int ManaSpent { get; set; }
		public int Essence { get; set; }
		public int Gold { get; set; }
		public int Scrolls { get; set; }
		public int Experience { get; set; }
		public int LevelUps { get; set; }
		public int AccountLevels { get; set; }

		/// <summary>Os prêmios de marco somados: Pergaminhos especiais e Núcleos de Infusão.</summary>
		public Prize Prize { get; set; } = Prize.None;

		/// <summary>As runas que caíram, na ordem; as vendidas continuam aqui (<see cref="IsSold"/>).</summary>
		public List<Rune> Runes { get; } = new();

		public List<RuneTool> Tools { get; } = new();

		/// <summary>Monstros que caíram (a Campanha às vezes solta um).</summary>
		public List<SummonResult> Monsters { get; } = new();

		/// <summary>Quanto já passou da luta em andamento, e quanto ela leva na tela.</summary>
		public double Elapsed { get; set; }

		public double Duration { get; set; }

		/// <summary>Rodando agora (falso depois do fim ou da parada).</summary>
		public bool Running { get; set; }

		/// <summary>Por que parou; nulo enquanto roda.</summary>
		public string? StopReason { get; set; }

		/// <summary>Parou antes do fim por algo que o jogador resolve (Mana, inventário): pode retomar.</summary>
		public bool CanResume { get; set; }

		/// <summary>Algo mudou: números, estado ou o relógio da luta.</summary>
		public event Action? Changed;

		/// <summary>Lutas terminadas (vitórias e derrotas).</summary>
		public int Done => Victories + Defeats;

		/// <summary>Fração da luta em andamento, de 0 a 1.</summary>
		public float FightProgress => Duration <= 0 ? 0 : (float)Math.Clamp(Elapsed / Duration, 0, 1);

		/// <summary>
		/// O tempo que falta até a última luta, pelo ritmo das lutas recentes: a de agora e as duas antes
		/// dela. A equipe muda no meio (nível, runas) e cada luta já sai com ela, então as primeiras lutas
		/// não dizem mais quanto as próximas levam.
		/// </summary>
		public TimeSpan TimeLeft
		{
			get
			{
				if (!Running)
					return TimeSpan.Zero;
				var pace = _durations.TakeLast(2).Append(Duration).Average();
				var seconds = Math.Max(0, Duration - Elapsed) + pace * Math.Max(0, Runs - Number);
				return TimeSpan.FromSeconds(seconds);
			}
		}

		/// <summary>
		/// A runa que caiu já saiu da conta: vendida pela janela da Batalha automática, pelo inventário ou
		/// pelo resultado de uma luta. Vender é o único jeito de uma runa sair da conta.
		/// </summary>
		public static bool IsSold(PlayerState player, Rune rune) => !player.Runes.Contains(rune);

		/// <summary>Soma a luta que acabou de passar na tela à média do tempo.</summary>
		public void Record(double seconds) => _durations.Add(seconds);

		/// <summary>Soma o que uma vitória rendeu.</summary>
		public void Add(VictoryReward reward)
		{
			Victories++;
			ManaSpent += reward.Mana;
			Essence += reward.Essence;
			Gold += reward.Gold + reward.AccountLevels * Account.LevelUpGold;
			Scrolls += reward.Scrolls;
			Experience += reward.Experience;
			LevelUps += reward.LevelUps.Count;
			AccountLevels += reward.AccountLevels;
			Prize += reward.Prize ?? Prize.None;
			if (reward.Rune is { } rune)
				Runes.Add(rune);
			Tools.AddRange(reward.Tools);
			if (reward.SummonResult is { } summon)
				Monsters.Add(summon);
		}

		public void Notify() => Changed?.Invoke();
	}
}
