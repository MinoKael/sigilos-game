namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Limos: recebe menos dano.</summary>
	internal sealed class DamageReductionPassive : UnitBehavior
	{
		public override double DamageTaken(UnitRule rule) => 1 - rule.Value;
	}
}
