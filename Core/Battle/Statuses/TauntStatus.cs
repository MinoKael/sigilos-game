namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Provocação: o dono só pode mirar em quem provocou (<see cref="UnitRule.Source"/>).</summary>
	internal sealed class TauntStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override BattleUnit? ForcedTarget(UnitRule rule) => rule.Source;
	}
}
