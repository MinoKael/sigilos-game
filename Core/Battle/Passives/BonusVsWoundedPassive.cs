namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Lobos: mais dano em quem está abaixo de <see cref="BattleRules.WoundedFraction"/> da Vida máxima.</summary>
	internal sealed class BonusVsWoundedPassive : UnitBehavior
	{
		public override double DamageDealt(UnitRule rule, BattleUnit target) =>
			target.HealthFraction < BattleRules.WoundedFraction ? 1 + rule.Value : 1;
	}
}
