using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Escudo: <see cref="EffectDefinition.Power"/> é a fração da Vida máxima de quem lança (ou a conta do efeito:
	/// <see cref="EffectAmount"/>), mais o bônus de cura da habilidade.
	/// </summary>
	internal sealed class ShieldEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var bonus = 1 + cast.HealBonus;
			foreach (var target in cast.Targets(effect))
				cast.Resolver.GiveShield(target, EffectAmount.Of(effect, effect.Power * bonus, bonus, cast.Caster, target), effect.Turns);
		}
	}
}
