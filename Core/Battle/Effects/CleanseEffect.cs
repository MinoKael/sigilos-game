using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>Purificação: remove <see cref="EffectDefinition.Count"/> efeitos negativos de cada alvo (0 = todos).</summary>
	internal sealed class CleanseEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			foreach (var target in cast.Targets(effect))
				cast.Resolver.Cleanse(target, effect.Count);
		}
	}
}
