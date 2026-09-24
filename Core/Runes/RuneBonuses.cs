using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Calcula o <see cref="RuneBonus"/> das runas equipadas: toda porcentagem é
	/// sobre o atributo de base, nunca uma sobre a outra (duas runas de +10% de Ataque dão +20%), e o
	/// que não fecha número inteiro arredonda para cima.
	/// </summary>
	public static class RuneBonuses
	{
		public static RuneBonus Compute(StatBlock baseStats, IReadOnlyCollection<Rune> runes)
		{
			var bonus = new StatBlock();
			foreach (var rune in runes)
			{
				bonus = Add(bonus, baseStats, rune.Main, rune.MainValue);
				if (rune.Innate != null)
					bonus = Add(bonus, baseStats, rune.Innate.Stat, rune.Innate.Total);
				foreach (var substat in rune.Substats)
					bonus = Add(bonus, baseStats, substat.Stat, substat.Total);
			}

			var active = new List<RuneSetDefinition>();
			double drain = 0, stun = 0, extraTurn = 0, shield = 0, counter = 0, nemesis = 0, destroy = 0;
			var immunity = 0;
			foreach (var group in runes.GroupBy(r => r.Set))
			{
				var set = RuneSets.For(group.Key);
				for (var i = 0; i < group.Count() / set.Pieces; i++)
				{
					active.Add(set);
					if (set.Stat is { } stat)
						bonus = bonus.With(stat, bonus.Get(stat) + (StatBlock.IsAbsolute(stat) ? RoundUp(baseStats.Get(stat) * set.Value) : set.Value));

					switch (set.Effect)
					{
						case RuneSetEffect.Drain:
							drain += set.Value;
							break;
						case RuneSetEffect.Stun:
							stun += set.Value;
							break;
						case RuneSetEffect.ExtraTurn:
							extraTurn += set.Value;
							break;
						case RuneSetEffect.AllyShield:
							shield += RoundUp(baseStats.Health * set.Value);
							break;
						case RuneSetEffect.Immunity:
							immunity += (int)set.Value;
							break;
						case RuneSetEffect.Counter:
							counter += set.Value;
							break;
						case RuneSetEffect.Nemesis:
							nemesis += set.Value;
							break;
						case RuneSetEffect.Destroy:
							destroy += set.Value;
							break;
					}
				}
			}

			var effects = new RuneSetEffects(drain, stun, extraTurn, shield, immunity, counter, nemesis, destroy);
			return new RuneBonus(bonus, effects, active);
		}

		private static StatBlock Add(StatBlock bonus, StatBlock baseStats, RuneStat stat, double value) => stat switch
		{
			RuneStat.HealthFlat => bonus with { Health = bonus.Health + value },
			RuneStat.HealthPercent => bonus with { Health = bonus.Health + RoundUp(baseStats.Health * value) },
			RuneStat.AttackFlat => bonus with { Attack = bonus.Attack + value },
			RuneStat.AttackPercent => bonus with { Attack = bonus.Attack + RoundUp(baseStats.Attack * value) },
			RuneStat.DefenseFlat => bonus with { Defense = bonus.Defense + value },
			RuneStat.DefensePercent => bonus with { Defense = bonus.Defense + RoundUp(baseStats.Defense * value) },
			RuneStat.Speed => bonus with { Speed = bonus.Speed + value },
			RuneStat.Crit => bonus with { Crit = bonus.Crit + value },
			RuneStat.CritDamage => bonus with { CritDamage = bonus.CritDamage + value },
			RuneStat.Resistance => bonus with { Resistance = bonus.Resistance + value },
			_ => bonus with { Accuracy = bonus.Accuracy + value },
		};

		/// <summary>+99,01 vira +100. A folga absorve o erro de fração (0,1 × 300).</summary>
		private static double RoundUp(double value) => Math.Ceiling(value - 1e-9);
	}
}
