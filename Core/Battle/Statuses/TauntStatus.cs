namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Provocar: um efeito positivo do próprio dono. Enquanto ele está em campo, os inimigos só podem
	/// escolhê-lo como alvo único (<see cref="Targeting.Choosable"/>); os ataques em área seguem atingindo
	/// quem a regra de área manda.
	/// </summary>
	internal sealed class TauntStatus : StatusBehavior
	{
		public override bool Harmful => false;

		public override bool DrawsAttacks => true;
	}
}
