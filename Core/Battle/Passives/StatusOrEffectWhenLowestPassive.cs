using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>No começo do turno do dono, se ele é o aliado com menos Vida (estritamente, como a Passiva dos Diabretes).</summary>
	internal sealed class StatusOrEffectWhenLowestPassive : EffectPassive
	{
		public StatusOrEffectWhenLowestPassive(PassiveDefinition passive) : base(passive) { }

		public override void OnTurnStart(UnitRule rule, EffectResolver resolver)
		{
			if (SpeedWhenLowestPassive.IsLowestInTeam(rule.Owner))
				Fire(rule, resolver, null);
		}
	}
}
