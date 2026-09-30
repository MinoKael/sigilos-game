namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Trolls: recupera uma fração da Vida máxima no começo de cada turno, mesmo atordoado.</summary>
	internal sealed class RegenEachTurnPassive : UnitBehavior
	{
		public override void OnTurnStart(UnitRule rule, EffectResolver resolver) =>
			resolver.Heal(rule.Owner, rule.Value * rule.Owner.MaxHealth);
	}
}
