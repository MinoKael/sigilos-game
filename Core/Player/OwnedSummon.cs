namespace Sigilos.Core.Player
{
	/// <summary>
	/// Um monstro da conta: uma cópia de uma variante de Data/summons. Cada invocação cria uma cópia
	/// nova, no nível 1 e sem Despertar, mesmo que o jogador já tenha outra da mesma variante.
	/// </summary>
	public sealed class OwnedSummon
	{
		/// <summary>Único na conta: runas e equipes apontam para ele.</summary>
		public int Id { get; set; }

		/// <summary>A variante, pelo id de Data/summons.</summary>
		public string SummonId { get; set; } = "";

		/// <summary>1 a 40 (Core/Progression/Leveling).</summary>
		public int Level { get; set; } = 1;

		/// <summary>Experiência acumulada dentro do nível atual.</summary>
		public int Experience { get; set; }

		/// <summary>Cópias da mesma variante fundidas neste monstro, de 0 a 5.</summary>
		public int Echoes { get; set; }

		public bool Awakened { get; set; }

		/// <summary>Guardado no Baú: fora da coleção e sem equipe; as runas ficam com ele.</summary>
		public bool Stored { get; set; }
	}
}
