using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Sets
{
	/// <summary>
	/// Tormento: chance de atordoar cada alvo atingido, um sorteio por alvo a cada habilidade. A
	/// Resistência não barra; só a Imunidade.
	/// </summary>
	internal sealed class StunSet : UnitBehavior
	{
		public override void AfterHit(UnitRule rule, Strike strike)
		{
			if (strike.Cast.FirstTime(rule, strike.Target))
				strike.Resolver.ApplyStatus(strike.Attacker, strike.Target, StatusKind.Stun, rule.Value, 1, resistible: false);
		}
	}
}
