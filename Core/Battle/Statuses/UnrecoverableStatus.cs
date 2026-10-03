namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Ferida: o dono não recebe cura (nem a da Bênção, nem o dreno).</summary>
	internal sealed class UnrecoverableStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool BlocksHealing => true;
	}
}
