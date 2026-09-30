namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Atordoamento: o dono perde o turno.</summary>
	internal sealed class StunStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool SkipsTurn => true;
	}
}
