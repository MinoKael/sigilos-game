namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Maldição: nenhum efeito positivo pega no dono (nem escudo, nem efeito roubado).</summary>
	internal sealed class CurseStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool BlocksBeneficial => true;
	}
}
