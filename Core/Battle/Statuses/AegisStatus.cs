namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Égide: enquanto durar, o dono é inatingível, não recebe dano nenhum: os golpes são anulados
	/// (<see cref="OnDefend"/>) e o dano que não é golpe (Aflição, Bomba) também (<see cref="BlocksDamage"/>).
	/// Ainda recebe efeitos negativos e pode perder a Égide (Purificação, duração).
	/// </summary>
	internal sealed class AegisStatus : StatusBehavior
	{
		public override bool BlocksDamage => true;

		public override void OnDefend(UnitRule rule, Strike strike) => strike.Blocked = true;
	}
}
