using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Quem pode ser escolhido e quem cada <see cref="TargetKind"/> atinge.
	///
	/// - Provocado só mira em quem provocou, enquanto essa unidade estiver viva.
	/// - Oculto não pode ser alvo único, a não ser que todos os candidatos estejam Ocultos.
	/// </summary>
	public static class Targeting
	{
		/// <summary>Inimigos que <paramref name="caster"/> pode escolher como alvo único.</summary>
		public static IReadOnlyList<BattleUnit> Choosable(Caster caster, IReadOnlyList<BattleUnit> opponents)
		{
			var alive = opponents.Where(u => u.IsAlive).ToList();

			var taunter = caster.Unit?.Find(StatusKind.Taunt)?.Source;
			if (taunter != null && alive.Contains(taunter))
				return new[] { taunter };

			var visible = alive.Where(u => !u.Has(StatusKind.Hidden)).ToList();
			return visible.Count > 0 ? visible : alive;
		}

		/// <summary>
		/// O alvo principal de uma habilidade: o escolhido, se ainda vale; senão, o primeiro que vale.
		/// Nulo quando não sobrou inimigo.
		/// </summary>
		public static BattleUnit? PickMain(Caster caster, BattleUnit? chosen, IReadOnlyList<BattleUnit> opponents)
		{
			var choosable = Choosable(caster, opponents);
			return chosen != null && choosable.Contains(chosen) ? chosen : choosable.FirstOrDefault();
		}

		/// <summary>As unidades atingidas por um efeito. <paramref name="main"/> vem de <see cref="PickMain"/>.</summary>
		public static IReadOnlyList<BattleUnit> Resolve(
			TargetKind kind,
			Caster caster,
			BattleUnit? main,
			IReadOnlyList<BattleUnit> allies,
			IReadOnlyList<BattleUnit> opponents)
		{
			switch (kind)
			{
				case TargetKind.Target:
					return main is { IsAlive: true } ? new List<BattleUnit> { main } : new List<BattleUnit>();

				case TargetKind.TwoEnemies:
				{
					var hit = new List<BattleUnit>();
					if (main is { IsAlive: true })
						hit.Add(main);

					var second = opponents
						.Where(u => u.IsAlive && u != main && !u.Has(StatusKind.Hidden))
						.OrderBy(u => u.HealthFraction)
						.FirstOrDefault();
					if (second != null)
						hit.Add(second);
					return hit;
				}

				case TargetKind.AllEnemies:
					return opponents.Where(u => u.IsAlive).ToList();

				case TargetKind.Self:
					return caster.Unit is { IsAlive: true } self ? new List<BattleUnit> { self } : new List<BattleUnit>();

				case TargetKind.LowestAlly:
					return allies.Where(u => u.IsAlive).OrderBy(u => u.HealthFraction).Take(1).ToList();

				case TargetKind.TwoAllies:
					return allies.Where(u => u.IsAlive).OrderBy(u => u.HealthFraction).Take(2).ToList();

				case TargetKind.AllAllies:
					return allies.Where(u => u.IsAlive).ToList();

				case TargetKind.DeadAlly:
					return allies.Where(u => !u.IsAlive && !u.PendingRebirth).Take(1).ToList();

				default:
					return new List<BattleUnit>();
			}
		}
	}
}
