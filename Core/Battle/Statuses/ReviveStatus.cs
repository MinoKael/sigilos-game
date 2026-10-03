namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Reviver: se o dono cai com o efeito, volta na hora com <see cref="BattleRules.ReviveHealth"/> da Vida
	/// máxima. O efeito sai na queda (como todos), então serve uma vez; as outras regras do dono não veem a
	/// queda, e quem o derrubou não conta como derrubada.
	/// </summary>
	internal sealed class ReviveStatus : StatusBehavior
	{
		public override void OnDeath(UnitRule rule, EffectResolver resolver)
		{
			rule.Owner.RevivalHealth = BattleRules.ReviveHealth;
			resolver.Revive(rule.Owner);
		}
	}
}
