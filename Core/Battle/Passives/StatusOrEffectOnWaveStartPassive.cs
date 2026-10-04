using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>No começo de cada onda, com o dono vivo.</summary>
	internal sealed class StatusOrEffectOnWaveStartPassive : EffectPassive
	{
		public StatusOrEffectOnWaveStartPassive(PassiveDefinition passive) : base(passive) { }

		public override void OnWaveStart(UnitRule rule, EffectResolver resolver) => Fire(rule, resolver, null);
	}
}
