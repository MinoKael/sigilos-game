using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;

namespace Sigilos.UI
{
	/// <summary>
	/// O ritmo da luta na tela: quanto cada evento fica à mostra, em segundos na velocidade 1×. A tela
	/// de batalha espera isso entre um evento e outro, e a Batalha automática usa a mesma conta para
	/// saber quanto uma luta levaria — assim as duas nunca discordam.
	/// </summary>
	public static class BattlePace
	{
		/// <summary>As velocidades da tela: o que o botão mostra e o quanto acelera. O "2×" acelera 3 vezes.</summary>
		public static readonly IReadOnlyList<(int Label, float Factor)> Speeds = new[] { (1, 1f), (2, 3f) };

		/// <summary>A Batalha automática leva o tempo da luta no automático, na velocidade mais rápida da tela.</summary>
		public static float AutoBattleFactor => Speeds[^1].Factor;

		public static double Seconds(BattleEvent battleEvent) => battleEvent switch
		{
			WaveStarted => 0.8,
			TurnStarted => 0.12,
			SkillUsed => 0.35,
			ExtraTurn => 0.3,
			Counterattack => 0.3,
			MaxHealthReduced => 0.1,
			Damaged => 0.14,
			Missed => 0.1,
			Warded => 0.1,
			Healed => 0.1,
			StatusApplied => 0.06,
			Resisted => 0.06,
			Immune => 0.06,
			ImpetoChanged => 0.05,
			TurnSkipped => 0.4,
			Died => 0.3,
			Revived => 0.4,
			BattleEnded => 0.6,
			_ => 0,
		};

		/// <summary>Quanto uma luta inteira leva na tela, dividida pela aceleração.</summary>
		public static double Seconds(IEnumerable<BattleEvent> events, float factor) => events.Sum(Seconds) / factor;
	}
}
