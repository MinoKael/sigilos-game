using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Quem pode ser escolhido e quem cada <see cref="TargetKind"/> atinge.
	///
	/// - Quem tem uma regra que obriga o alvo (Provocação) só mira nessa unidade, enquanto ela estiver viva.
	/// - Quem está oculto não pode ser alvo único, a não ser que todos os candidatos estejam ocultos.
	/// - Os ao acaso (RandomAlly, RandomEnemy) sorteiam entre os de pé, oculto ou não.
	/// </summary>
	public static class Targeting
	{
		/// <summary>Inimigos que <paramref name="caster"/> pode escolher como alvo único.</summary>
		public static IReadOnlyList<BattleUnit> Choosable(BattleUnit caster, IReadOnlyList<BattleUnit> opponents)
		{
			var alive = opponents.Where(u => u.IsAlive).ToList();

			foreach (var rule in caster.Rules())
			{
				if (rule.Behavior.ForcedTarget(rule) is { } forced && alive.Contains(forced))
					return new[] { forced };
			}

			var visible = alive.Where(u => !u.Any(behavior => behavior.HidesOwner)).ToList();
			return visible.Count > 0 ? visible : alive;
		}

		/// <summary>
		/// O alvo principal de uma habilidade: o escolhido, se ainda vale; senão, o primeiro que vale.
		/// Nulo quando não sobrou inimigo.
		/// </summary>
		public static BattleUnit? PickMain(BattleUnit caster, BattleUnit? chosen, IReadOnlyList<BattleUnit> opponents)
		{
			var choosable = Choosable(caster, opponents);
			return chosen != null && choosable.Contains(chosen) ? chosen : choosable.FirstOrDefault();
		}

		/// <summary>
		/// As unidades atingidas por um efeito. <paramref name="main"/> vem de <see cref="PickMain"/>;
		/// <paramref name="by"/> é o que LowestAlly e HighestAlly comparam (empate: o primeiro da equipe).
		/// </summary>
		public static IReadOnlyList<BattleUnit> Resolve(
			TargetKind kind,
			TargetRank by,
			BattleUnit caster,
			BattleUnit? main,
			IReadOnlyList<BattleUnit> allies,
			IReadOnlyList<BattleUnit> opponents,
			Random random) => kind switch
		{
			TargetKind.Target => main is { IsAlive: true } ? new[] { main } : Array.Empty<BattleUnit>(),
			TargetKind.AllEnemies => opponents.Where(u => u.IsAlive).ToList(),
			TargetKind.Self => caster.IsAlive ? new[] { caster } : Array.Empty<BattleUnit>(),
			TargetKind.LowestAlly => allies.Where(u => u.IsAlive).OrderBy(u => Rank(u, by)).Take(1).ToList(),
			TargetKind.AllAllies => allies.Where(u => u.IsAlive).ToList(),
			TargetKind.HighestAlly => allies.Where(u => u.IsAlive).OrderByDescending(u => Rank(u, by)).Take(1).ToList(),
			TargetKind.RandomAlly => Draw(allies, random),
			TargetKind.RandomEnemy => Draw(opponents, random),
			_ => Array.Empty<BattleUnit>(),
		};

		/// <summary>O valor que LowestAlly e HighestAlly comparam.</summary>
		public static double Rank(BattleUnit unit, TargetRank by) => by switch
		{
			TargetRank.Health => unit.HealthFraction,
			TargetRank.Impeto => unit.Impeto,
			_ => unit.Current(StatOf(by)!.Value),
		};

		/// <summary>O atributo por trás do valor; nulo para o Ímpeto.</summary>
		public static Stat? StatOf(TargetRank by) => by switch
		{
			TargetRank.Health => Stat.Health,
			TargetRank.Attack => Stat.Attack,
			TargetRank.Defense => Stat.Defense,
			TargetRank.Speed => Stat.Speed,
			TargetRank.Crit => Stat.Crit,
			TargetRank.CritDamage => Stat.CritDamage,
			TargetRank.Resistance => Stat.Resistance,
			TargetRank.Accuracy => Stat.Accuracy,
			_ => null,
		};

		/// <summary>Um de pé, sorteado.</summary>
		private static IReadOnlyList<BattleUnit> Draw(IReadOnlyList<BattleUnit> units, Random random)
		{
			var alive = units.Where(u => u.IsAlive).ToList();
			return alive.Count == 0 ? Array.Empty<BattleUnit>() : new[] { alive[random.Next(alive.Count)] };
		}
	}
}
