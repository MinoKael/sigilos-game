using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Nivelar Vida: os alvos de pé ficam todos com a média da fração de Vida deles (quem tinha mais desce,
	/// quem tinha menos sobe pela cura). Ninguém passa da Vida máxima nem cai por isso.
	/// </summary>
	internal sealed class EqualizeHealthEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var team = cast.Targets(effect.Target).Where(unit => unit.IsAlive).ToList();
			if (team.Count < 2)
				return;

			var fraction = team.Average(unit => unit.HealthFraction);
			foreach (var unit in team)
				cast.Resolver.SetHealth(unit, fraction * unit.MaxHealth);
		}
	}
}
