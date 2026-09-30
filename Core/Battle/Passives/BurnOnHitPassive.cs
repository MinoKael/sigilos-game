using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Dragões: chance de Queimadura em cada alvo atingido, um sorteio por alvo a cada habilidade.</summary>
	internal sealed class BurnOnHitPassive : UnitBehavior
	{
		public override void AfterHit(UnitRule rule, Strike strike)
		{
			if (strike.Cast.FirstTime(rule, strike.Target))
				strike.Resolver.ApplyStatus(strike.Attacker, strike.Target, StatusKind.Burn, rule.Value, BattleRules.BurnOnHitTurns);
		}
	}
}
