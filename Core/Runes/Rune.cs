using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Uma runa (GDD, seção 10): conjunto, espaço de 1 a 6, estrelas de 1 a 6, melhora
	/// de +0 a +15, um atributo principal, às vezes um atributo nativo e até 4 subatributos. A única
	/// diferença é que a melhora nunca falha (<see cref="RuneRules.UpgradeCost"/>).
	/// O valor do principal não é guardado: sai de <see cref="RuneRules.MainValue"/>.
	/// </summary>
	public sealed class Rune
	{
		public int Id { get; set; }
		public RuneSet Set { get; set; }

		/// <summary>1 a 6. Os espaços 1, 3 e 5 têm principal fixo (Ataque, Defesa e Vida).</summary>
		public int Slot { get; set; }

		/// <summary>Estrelas, de 1 a 6. Decidem o tamanho de todos os números da runa.</summary>
		public int Grade { get; set; }

		/// <summary>Melhora, de 0 a 15.</summary>
		public int Level { get; set; }

		public RuneStat Main { get; set; }

		/// <summary>O atributo nativo ("prefixo"): vem no drop, nunca cresce e não conta como subatributo.</summary>
		public RuneSubstat? Innate { get; set; }

		/// <summary>De 0 a 4. Em +3, +6, +9 e +12 entra um novo ou, com 4, um deles cresce.</summary>
		public List<RuneSubstat> Substats { get; set; } = new();

		/// <summary>Id do monstro que usa a runa; nulo no inventário.</summary>
		public int? EquippedOn { get; set; }

		[JsonIgnore]
		public double MainValue => RuneRules.MainValue(Main, Grade, Level);

		/// <summary>A cor da runa: sobe com o número de subatributos.</summary>
		[JsonIgnore]
		public RuneRarity Rarity => (RuneRarity)Substats.Count;
	}
}
