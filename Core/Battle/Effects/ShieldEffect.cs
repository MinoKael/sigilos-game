using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>Escudo: <see cref="EffectDefinition.Power"/> é a fração da Vida máxima de quem lança.</summary>
	internal sealed class ShieldEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			foreach (var target in cast.Targets(effect.Target))
				cast.Resolver.GiveShield(target, effect.Power * cast.Caster.MaxHealth, effect.Turns);
		}
	}
}
