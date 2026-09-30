using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>Purificação: remove um efeito negativo de cada alvo.</summary>
	internal sealed class CleanseEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			foreach (var target in cast.Targets(effect.Target))
				cast.Resolver.Cleanse(target);
		}
	}
}
