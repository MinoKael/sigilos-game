namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Égide: anula o próximo golpe que o dono levaria e some.</summary>
	internal sealed class AegisStatus : StatusBehavior
	{
		public override void OnDefend(UnitRule rule, Strike strike)
		{
			rule.Owner.Remove(rule);
			strike.Blocked = true;
		}
	}
}
