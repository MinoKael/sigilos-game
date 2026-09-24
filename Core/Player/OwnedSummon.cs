namespace Sigilos.Core.Player
{
	/// <summary>Uma invocação da coleção: o quanto ela cresceu. O que ela é mora em Data/summons.</summary>
	public sealed class OwnedSummon
	{
		/// <summary>1 a 40 (Core/Progression/Leveling).</summary>
		public int Level { get; set; } = 1;

		/// <summary>Experiência acumulada dentro do nível atual.</summary>
		public int Experience { get; set; }

		/// <summary>Duplicatas recebidas, de 0 a 5.</summary>
		public int Echoes { get; set; }

		public bool Awakened { get; set; }
	}
}
