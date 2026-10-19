using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Bônus por efeitos: conta os efeitos que valem (<see cref="StatusFilter"/>) nas unidades de
	/// <see cref="EffectDefinition.From"/> e dá <see cref="EffectDefinition.Power"/> por efeito do
	/// <see cref="EffectDefinition.Bonus"/>: dano ou cura a mais nos efeitos seguintes da mesma habilidade
	/// (o efeito vem antes deles na lista), ou Ímpeto na hora para os alvos do efeito.
	/// </summary>
	internal sealed class BonusPerStatusEffect : SkillEffect
	{
		/// <summary>O bônus somado, sem passar do teto (<see cref="EffectDefinition.Max"/>; 0 = sem teto).</summary>
		private static double Capped(double total, double max) => max > 0 ? Math.Min(total, max) : total;

		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var count = cast.Targets(effect.From, effect.By).Sum(unit => StatusFilter.Of(unit, effect).Count);
			if (count == 0)
				return;

			var bonus = count * effect.Power;
			switch (effect.Bonus)
			{
				case BonusKind.Damage:
					cast.DamageBonus = Capped(cast.DamageBonus + bonus, effect.Max);
					break;
				case BonusKind.Heal:
					cast.HealBonus = Capped(cast.HealBonus + bonus, effect.Max);
					break;
				default:
					foreach (var target in cast.Targets(effect))
						cast.Resolver.GainImpeto(target, bonus);
					break;
			}
		}
	}
}
