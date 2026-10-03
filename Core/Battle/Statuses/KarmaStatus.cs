namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Karma: nenhum efeito positivo pega no dono (nem escudo, nem efeito roubado).</summary>
	internal sealed class KarmaStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool BlocksBeneficial => true;
	}
}
