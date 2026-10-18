namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Estigma: o dono recebe mais dano.</summary>
	internal sealed class StigmaStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override double DamageTaken(UnitRule rule) => 1 + BattleRules.StigmaBonus;
	}
}
