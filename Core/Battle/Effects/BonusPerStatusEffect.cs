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
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var count = cast.Targets(effect.From).Sum(unit => StatusFilter.Of(unit, effect).Count);
			if (count == 0)
				return;

			var bonus = count * effect.Power;
			switch (effect.Bonus)
			{
				case BonusKind.Damage:
					cast.DamageBonus += bonus;
					break;
				case BonusKind.Heal:
					cast.HealBonus += bonus;
					break;
				default:
					foreach (var target in cast.Targets(effect.Target))
						cast.Resolver.GainImpeto(target, bonus);
					break;
			}
		}
	}
}
