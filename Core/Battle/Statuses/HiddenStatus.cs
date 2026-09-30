namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Oculto: o dono não pode ser alvo de ataque único enquanto sobrar outro alvo.</summary>
	internal sealed class HiddenStatus : StatusBehavior
	{
		public override bool HidesOwner => true;
	}
}
