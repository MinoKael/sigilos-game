using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Ímpeto: soma <see cref="EffectDefinition.Power"/> pontos à barra de cada alvo (negativo atrasa).
	/// Atrasar inimigo passa pela chance do efeito e pela Resistência, como qualquer efeito negativo
	/// (a Imunidade não barra); adiantar nunca falha.
	/// </summary>
	internal sealed class ImpetoEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var resolver = cast.Resolver;
			var caster = cast.Caster;
			foreach (var target in cast.Targets(effect))
			{
				if (!target.IsAlive)
					continue;

				if (effect.Power < 0 && target.Side != caster.Side)
				{
					if (resolver.Random.NextDouble() >= effect.Chance || resolver.Random.NextDouble() < BattleRules.ResistChance(target.Stats, caster.Stats))
					{
						resolver.Emit(new Resisted(target));
						continue;
					}
				}

				resolver.GainImpeto(target, effect.Power);
			}
		}
	}
}
