using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>Detonar Bombas: todas as Bombas de cada alvo explodem na hora (<see cref="EffectResolver.Detonate"/>).</summary>
	internal sealed class BombTriggerEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			foreach (var target in cast.Targets(effect))
				cast.Resolver.Detonate(target);
		}
	}
}
