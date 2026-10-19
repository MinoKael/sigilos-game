namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Filactério: a alma de quem pôs o efeito fica guardada no dono. Enquanto o dono estiver de pé com ele, quem pôs
	/// não morre: a Vida dele para em 1 (<see cref="BattleUnit.IsBound"/>). O efeito é único por quem pôs: pôr em outro
	/// aliado tira o anterior.
	/// </summary>
	internal sealed class PhylacteryStatus : StatusBehavior
	{
		public override bool ProtectsSource => true;

		public override bool UniquePerSource => true;
	}
}
