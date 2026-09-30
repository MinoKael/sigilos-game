namespace Sigilos.Core.Battle.Sets
{
	/// <summary>Frenesi: chance de agir de novo depois de agir. O turno extra não sorteia outro: um por turno.</summary>
	internal sealed class ExtraTurnSet : UnitBehavior
	{
		public override void AfterAction(UnitRule rule, EffectResolver resolver)
		{
			if (rule.Owner.IsAlive && !resolver.IsExtraTurn && resolver.Random.NextDouble() < rule.Value)
				resolver.GrantExtraTurn(rule.Owner);
		}
	}
}
