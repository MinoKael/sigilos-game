using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// A conta de um efeito com valor (<see cref="EffectScaling"/>): power × o atributo, mais os termos de
	/// <see cref="EffectDefinition.Plus"/>, vezes o fator e a Velocidade. O dano sai daqui antes da Defesa;
	/// a cura, o escudo e a volta dos caídos saem daqui prontos.
	/// </summary>
	internal static class EffectAmount
	{
		private static readonly Dictionary<ScaleStat, Func<BattleUnit, BattleUnit, double, double>> Values = new()
		{
			[ScaleStat.Attack] = (caster, _, _) => caster.Attack,
			[ScaleStat.Defense] = (caster, _, _) => caster.Defense,
			[ScaleStat.MaxHealth] = (caster, _, _) => caster.MaxHealth,
			[ScaleStat.Speed] = (caster, _, _) => caster.Current(Stat.Speed),
			[ScaleStat.TargetMaxHealth] = (_, target, _) => target.MaxHealth,
			[ScaleStat.Level] = (caster, _, _) => caster.Level,
			[ScaleStat.Sacrificed] = (_, _, sacrificed) => sacrificed,
		};

		private static readonly Dictionary<ScaleMeasure, Func<BattleUnit, BattleUnit, double>> Measures = new()
		{
			[ScaleMeasure.HealthFraction] = (caster, _) => caster.HealthFraction,
			[ScaleMeasure.TargetHealthFraction] = (_, target) => target.HealthFraction,
			[ScaleMeasure.LivingAllies] = (caster, _) => caster.Team.Count == 0 ? 1 : caster.Team.Count(unit => unit.IsAlive) / (double)caster.Team.Count,
		};

		/// <param name="power">O power do efeito com os bônus da habilidade (o contra-ataque, o bônus por efeitos).</param>
		/// <param name="scale">Os mesmos bônus, para os termos de <see cref="EffectDefinition.Plus"/>.</param>
		/// <param name="sacrificed">A Vida que a habilidade já sacrificou (<see cref="Cast.Sacrificed"/>), para <see cref="ScaleStat.Sacrificed"/>.</param>
		public static double Of(EffectDefinition effect, double power, double scale, BattleUnit caster, BattleUnit target, double sacrificed = 0)
		{
			var amount = power * Values[EffectScaling.MainStat(effect)](caster, target, sacrificed);
			foreach (var term in effect.Plus)
				amount += term.Power * scale * Values[term.Stat](caster, target, sacrificed);

			if (effect.Factor is { } factor)
				amount *= factor.Base + factor.Slope * Measures[factor.By](caster, target);

			if (effect.Speed is { } speed)
				amount *= (caster.Current(Stat.Speed) + speed.Add) / (speed.OverTarget ? target.TurnSpeed : speed.Over);

			return Math.Max(0, amount);
		}
	}
}
