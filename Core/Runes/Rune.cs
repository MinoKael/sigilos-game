using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Uma runa (GDD, seção 10, "as runas de Summoners War, com menos sorteio"): conjunto (um Glifo),
	/// espaço de 1 a 6, estrelas de 1 a 5, melhora de +0 a +9, um atributo principal e 3 subatributos.
	/// O valor do principal não é guardado: sai de <see cref="RuneRules.MainValue"/>.
	/// </summary>
	public sealed class Rune
	{
		public int Id { get; set; }
		public Glyph Set { get; set; }

		/// <summary>1 a 6. Os espaços 1, 3 e 5 têm principal fixo (Ataque, Defesa e Vida).</summary>
		public int Slot { get; set; }

		/// <summary>Estrelas, de 1 a 5.</summary>
		public int Grade { get; set; }

		/// <summary>Melhora, de 0 a 9.</summary>
		public int Level { get; set; }

		public RuneStat Main { get; set; }
		public List<RuneSubstat> Substats { get; set; } = new();

		/// <summary>Id da invocação que usa a runa; nulo no inventário.</summary>
		public string? EquippedOn { get; set; }

		public double MainValue => RuneRules.MainValue(Main, Grade, Level);
	}
}
