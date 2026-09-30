namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Imunidade: nenhum efeito negativo pega no dono.</summary>
	internal sealed class ImmunityStatus : StatusBehavior
	{
		public override bool BlocksHarmful => true;
	}
}
