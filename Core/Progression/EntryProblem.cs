namespace Sigilos.Core.Progression
{
	/// <summary>Por que uma luta não pode começar. <see cref="None"/>: pode.</summary>
	public enum EntryProblem
	{
		None,

		/// <summary>Fase, Masmorra ou andar ainda fechado.</summary>
		Locked,

		NoMana,

		/// <summary>O inventário de runas está cheio e a luta solta runa.</summary>
		RunesFull,
	}
}
