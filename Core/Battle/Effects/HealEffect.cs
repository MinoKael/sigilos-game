using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>Cura: <see cref="EffectDefinition.Power"/> é a fração da Vida máxima de cada alvo.</summary>
	internal sealed class HealEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			foreach (var target in cast.Targets(effect.Target))
				cast.Resolver.Heal(target, effect.Power * target.MaxHealth);
		}
	}
}
