using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Calcula o <see cref="RuneBonus"/> das runas equipadas. Percentuais sempre sobre o atributo de
	/// base, nunca uns sobre os outros: duas runas de +10% de Ataque dão +20%.
	/// </summary>
	public static class RuneBonuses
	{
		public static RuneBonus Compute(StatBlock baseStats, IReadOnlyCollection<Rune> runes)
		{
			var bonus = new StatBlock();
			foreach (var rune in runes)
			{
				bonus = Add(bonus, baseStats, rune.Main, rune.MainValue);
				foreach (var substat in rune.Substats)
					bonus = Add(bonus, baseStats, substat.Stat, substat.Value);
			}

			var active = new List<RuneSetDefinition>();
			double drain = 0, stun = 0, extra = 0;
			foreach (var group in runes.GroupBy(r => r.Set))
			{
				var set = RuneSets.For(group.Key);
				for (var i = 0; i < group.Count() / set.Pieces; i++)
				{
					active.Add(set);
					if (set.Stat is { } stat)
						bonus = bonus.With(stat, bonus.Get(stat) + (StatBlock.IsAbsolute(stat) ? baseStats.Get(stat) * set.Value : set.Value));

					switch (set.Effect)
					{
						case RuneSetEffect.Drain:
							drain += set.Value;
							break;
						case RuneSetEffect.StunOnHit:
							stun += set.Value;
							break;
						case RuneSetEffect.ExtraTurn:
							extra += set.Value;
							break;
					}
				}
			}

			return new RuneBonus(bonus, new RuneSetEffects(drain, stun, extra), active);
		}

		private static StatBlock Add(StatBlock bonus, StatBlock baseStats, RuneStat stat, double value) => stat switch
		{
			RuneStat.HealthFlat => bonus with { Health = bonus.Health + value },
			RuneStat.HealthPercent => bonus with { Health = bonus.Health + baseStats.Health * value },
			RuneStat.AttackFlat => bonus with { Attack = bonus.Attack + value },
			RuneStat.AttackPercent => bonus with { Attack = bonus.Attack + baseStats.Attack * value },
			RuneStat.DefenseFlat => bonus with { Defense = bonus.Defense + value },
			RuneStat.DefensePercent => bonus with { Defense = bonus.Defense + baseStats.Defense * value },
			RuneStat.Speed => bonus with { Speed = bonus.Speed + value },
			RuneStat.Crit => bonus with { Crit = bonus.Crit + value },
			RuneStat.CritDamage => bonus with { CritDamage = bonus.CritDamage + value },
			RuneStat.Resistance => bonus with { Resistance = bonus.Resistance + value },
			_ => bonus with { Focus = bonus.Focus + value },
		};
	}
}
