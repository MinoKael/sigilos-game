using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>No começo de cada turno do dono, mesmo que vá perder o turno: a regeneração dos Trolls, a cura dos Paladinos.</summary>
	internal sealed class StatusOrEffectEachTurnPassive : EffectPassive
	{
		public StatusOrEffectEachTurnPassive(PassiveDefinition passive) : base(passive) { }

		public override void OnTurnStart(UnitRule rule, EffectResolver resolver) => Fire(rule, resolver, null);
	}
}
