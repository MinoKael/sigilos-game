namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Esquecimento: a Passiva do dono para de funcionar enquanto durar (os conjuntos de runas seguem).</summary>
	internal sealed class OblivionStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool SuppressesPassive => true;
	}
}
