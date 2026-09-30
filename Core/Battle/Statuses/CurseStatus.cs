namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Maldição: o dono recebe mais dano.</summary>
	internal sealed class CurseStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override double DamageTaken(UnitRule rule) => 1 + BattleRules.CurseBonus;
	}
}
