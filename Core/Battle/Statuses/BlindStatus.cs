namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Cegueira: cada golpe do dono tem chance de errar.</summary>
	internal sealed class BlindStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override void OnAttack(UnitRule rule, Strike strike)
		{
			if (strike.Resolver.Random.NextDouble() < BattleRules.BlindMissChance)
				strike.Missed = true;
		}
	}
}
