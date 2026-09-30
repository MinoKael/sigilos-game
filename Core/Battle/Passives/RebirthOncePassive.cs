namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Fênix: na primeira vez que cai, volta no turno seguinte dela com uma fração da Vida máxima. A
	/// regra sai da unidade depois de usada: é uma vez por luta.
	/// </summary>
	internal sealed class RebirthOncePassive : UnitBehavior
	{
		public override void OnDeath(UnitRule rule, EffectResolver resolver)
		{
			rule.Owner.RevivalHealth = rule.Value;
			rule.Owner.Remove(rule);
		}
	}
}
