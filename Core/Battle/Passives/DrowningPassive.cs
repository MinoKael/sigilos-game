namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Afogamento (Santuário): no começo de cada rodada, o dono perde <see cref="UnitRule.Value"/> da Vida
	/// máxima (o escudo absorve, como em todo dano que não é golpe) e fica com Velocidade−. Acontece uma vez por
	/// rodada para cada unidade, venha quantos turnos vierem nela.
	/// </summary>
	internal sealed class DrowningPassive : UnitBehavior
	{
		/// <summary>Quantos turnos do dono dura a Velocidade− de cada rodada.</summary>
		private const int SlowTurns = 2;

		public override void OnRoundStart(UnitRule rule, EffectResolver resolver, int round)
		{
			var owner = rule.Owner;
			if (!owner.IsAlive)
				return;

			var fraction = rule.Value > 0 ? rule.Value : BattleRules.DrowningFraction;
			resolver.GiveStatus(owner, Content.StatusKind.SpeedDown, SlowTurns);
			resolver.Wound(owner, owner.MaxHealth * fraction, shielded: true);
		}
	}
}
