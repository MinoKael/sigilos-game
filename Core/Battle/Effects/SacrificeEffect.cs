using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Sacrifício: quem lança perde Vida pela conta do efeito (<see cref="EffectAmount"/>; "50% da Vida máxima × a fração de Vida de
	/// agora" é metade da Vida atual) e o que perdeu vira <see cref="Cast.Sacrificed"/> para os efeitos seguintes. Sem
	/// <see cref="EffectDefinition.Destroy"/>, a Vida é descontada e nunca chega a 0. Com ele, é a Vida máxima que cai,
	/// como no Oblívio (<see cref="Sets.OblivionSet"/>): a Vida atual só desce se passa da nova máxima, até
	/// <see cref="BattleRules.SacrificeDestroyLimit"/> da Vida base somando tudo o que já foi destruído.
	/// </summary>
	internal sealed class SacrificeEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var caster = cast.Caster;
			if (!caster.IsAlive)
				return;

			var amount = Math.Round(EffectAmount.Of(effect, effect.Power, 1, caster, caster));
			cast.Sacrificed += effect.Destroy ? Destroy(cast, amount) : Spend(cast, amount);
		}

		private static double Spend(Cast cast, double amount)
		{
			var caster = cast.Caster;
			var lost = Math.Min(amount, caster.Health - 1);
			if (lost <= 0)
				return 0;

			cast.Resolver.SetHealth(caster, caster.Health - lost);
			return lost;
		}

		private static double Destroy(Cast cast, double amount)
		{
			var caster = cast.Caster;
			var destroyed = Math.Min(amount, BattleRules.SacrificeDestroyLimit * caster.Stats.Health - caster.HealthDestroyed);
			if (destroyed <= 0)
				return 0;

			caster.HealthDestroyed += destroyed;
			caster.Health = Math.Min(caster.Health, caster.MaxHealth);
			cast.Resolver.Emit(new MaxHealthReduced(caster, (int)destroyed, caster.Health, caster.MaxHealth));
			return destroyed;
		}
	}
}
