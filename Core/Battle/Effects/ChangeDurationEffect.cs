using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Alterar duração: com <see cref="EffectDefinition.Chance"/>, soma <see cref="EffectDefinition.Turns"/>
	/// (negativo encurta) a cada efeito que vale (<see cref="StatusFilter"/>) em cada alvo. O que chega a 0
	/// sai. Serve para os dois lados: prolongar os positivos dos aliados, encurtar os negativos deles,
	/// encurtar os positivos dos inimigos...
	/// </summary>
	internal sealed class ChangeDurationEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var resolver = cast.Resolver;
			foreach (var target in cast.Targets(effect))
			{
				if (!target.IsAlive || resolver.Random.NextDouble() >= effect.Chance)
					continue;

				foreach (var status in StatusFilter.Of(target, effect))
					resolver.ChangeDuration(status, effect.Turns);
			}
		}
	}
}
