using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Um subatributo (ou o nativo) de uma runa. <see cref="Rolls"/> guarda cada sorteio que ele
	/// recebeu, com o nível; <see cref="Value"/> é a soma deles. <see cref="Grind"/> é o que a Pedra de
	/// Afiar somou por cima.
	/// </summary>
	public sealed class RuneSubstat
	{
		public RuneStat Stat { get; set; }

		/// <summary>O sorteio de origem e os das melhoras, em ordem.</summary>
		public List<RuneRoll> Rolls { get; set; } = new();

		/// <summary>Bônus da Pedra de Afiar. Uma pedra nova troca o bônus, não soma.</summary>
		public double Grind { get; set; }

		/// <summary>Veio de uma Gema Encantada. Só um subatributo por runa pode ser encantado.</summary>
		public bool Enchanted { get; set; }

		[JsonIgnore]
		public double Value => Rolls.Sum(r => r.Amount);

		[JsonIgnore]
		public double Total => Value + Grind;

		/// <summary>Um subatributo novo com o sorteio de origem.</summary>
		public static RuneSubstat Rolled(RuneStat stat, int level, double amount) => new()
		{
			Stat = stat,
			Rolls = { new RuneRoll(level, amount) },
		};
	}
}
