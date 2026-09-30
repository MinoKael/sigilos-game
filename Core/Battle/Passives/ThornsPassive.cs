namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Druidas: quem os atinge recebe de volta uma fração do dano que causou. Não é golpe: não passa
	/// por Defesa nem dispara nada, mas o escudo de quem atacou absorve. Pode derrubar quem atacou, e
	/// aí a habilidade dele para no meio.
	/// </summary>
	internal sealed class ThornsPassive : UnitBehavior
	{
		public override void AfterHurt(UnitRule rule, Strike strike)
		{
			if (strike.Dealt > 0)
				strike.Resolver.Wound(strike.Attacker, strike.Dealt * rule.Value, shielded: true);
		}
	}
}
