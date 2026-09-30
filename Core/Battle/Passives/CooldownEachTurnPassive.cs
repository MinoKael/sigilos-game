namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Magos: no começo de cada turno, chance de todas as recargas perderem um turno a mais. Sem
	/// habilidade em recarga, nem sorteia.
	/// </summary>
	internal sealed class CooldownEachTurnPassive : UnitBehavior
	{
		public override void OnTurnStart(UnitRule rule, EffectResolver resolver)
		{
			var owner = rule.Owner;
			var waiting = false;
			for (var index = 1; index < owner.Skills.Count; index++)
				waiting |= owner.Cooldown(index) > 0;

			if (waiting && resolver.Random.NextDouble() < rule.Value)
				owner.TickCooldowns();
		}
	}
}
