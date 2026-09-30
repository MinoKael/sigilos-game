using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Efeito de status: tenta pôr <see cref="EffectDefinition.Status"/> em cada alvo, com
	/// <see cref="EffectDefinition.Chance"/>, por <see cref="EffectDefinition.Turns"/>. O que o efeito
	/// faz depois de posto é da estratégia dele (Statuses/StatusBehaviors).
	/// </summary>
	internal sealed class InflictEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			foreach (var target in cast.Targets(effect.Target))
				cast.Resolver.ApplyStatus(cast.Caster, target, effect.Status, effect.Chance, effect.Turns);
		}
	}
}
