using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Cura (Heal e HealTeam): <see cref="EffectDefinition.Power"/> é a fração da Vida máxima de cada alvo,
	/// mais o bônus da habilidade (<see cref="Cast.HealBonus"/>). Com <see cref="EffectDefinition.Count"/>,
	/// só os tantos alvos mais feridos (pela fração de Vida).
	/// </summary>
	internal sealed class HealEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var targets = cast.Targets(effect).AsEnumerable();
			if (effect.Count > 0)
				targets = targets.OrderBy(t => t.HealthFraction).Take(effect.Count);

			foreach (var target in targets.ToList())
				cast.Resolver.Heal(target, effect.Power * (1 + cast.HealBonus) * target.MaxHealth);
		}
	}
}
