using System.Linq;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Goblins: mais dano em quem está com efeito negativo.</summary>
	internal sealed class BonusVsDebuffedPassive : UnitBehavior
	{
		public override double DamageDealt(UnitRule rule, BattleUnit target) =>
			target.Statuses.Any(status => BattleRules.IsNegative(status.Kind)) ? 1 + rule.Value : 1;
	}
}
