using System;
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
		public static IReadOnlyList<BattleUnit> Choosable(BattleUnit caster, IReadOnlyList<BattleUnit> opponents)
		{
			var alive = opponents.Where(u => u.IsAlive).ToList();

			var taunter = caster.Find(StatusKind.Taunt)?.Source;
			if (taunter != null && alive.Contains(taunter))
				return new[] { taunter };

			var visible = alive.Where(u => !u.Has(StatusKind.Hidden)).ToList();
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

		/// <summary>As unidades atingidas por um efeito. <paramref name="main"/> vem de <see cref="PickMain"/>.</summary>
		public static IReadOnlyList<BattleUnit> Resolve(
			TargetKind kind,
			BattleUnit caster,
			BattleUnit? main,
			IReadOnlyList<BattleUnit> allies,
			IReadOnlyList<BattleUnit> opponents) => kind switch
		{
			TargetKind.Target => main is { IsAlive: true } ? new[] { main } : Array.Empty<BattleUnit>(),
			TargetKind.AllEnemies => opponents.Where(u => u.IsAlive).ToList(),
			TargetKind.Self => caster.IsAlive ? new[] { caster } : Array.Empty<BattleUnit>(),
			TargetKind.LowestAlly => allies.Where(u => u.IsAlive).OrderBy(u => u.HealthFraction).Take(1).ToList(),
			TargetKind.AllAllies => allies.Where(u => u.IsAlive).ToList(),
			_ => Array.Empty<BattleUnit>(),
		};
	}
}
