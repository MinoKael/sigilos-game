using System.Text.Json.Serialization;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Um subatributo (ou o nativo) de uma runa. <see cref="Value"/> é o sorteio de origem mais as
	/// melhoras de +3, +6, +9 e +12; <see cref="Grind"/> é o que a Pedra de Afiar somou por cima.
	/// </summary>
	public sealed class RuneSubstat
	{
		public RuneStat Stat { get; set; }
		public double Value { get; set; }

		/// <summary>Bônus da Pedra de Afiar. Uma pedra nova troca o bônus, não soma.</summary>
		public double Grind { get; set; }

		/// <summary>Veio de uma Gema Encantada. Só um subatributo por runa pode ser encantado.</summary>
		public bool Enchanted { get; set; }

		[JsonIgnore]
		public double Total => Value + Grind;
	}
}
