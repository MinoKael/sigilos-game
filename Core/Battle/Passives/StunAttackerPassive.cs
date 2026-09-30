using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Gárgulas: chance de atordoar quem as atinge, um sorteio por atacante a cada habilidade. É efeito
	/// negativo como qualquer outro: a Resistência de quem atacou pode barrar.
	/// </summary>
	internal sealed class StunAttackerPassive : UnitBehavior
	{
		public override void AfterHurt(UnitRule rule, Strike strike)
		{
			if (strike.Cast.FirstTime(rule, strike.Attacker))
				strike.Resolver.ApplyStatus(rule.Owner, strike.Attacker, StatusKind.Stun, rule.Value, BattleRules.StunAttackerTurns);
		}
	}
}
