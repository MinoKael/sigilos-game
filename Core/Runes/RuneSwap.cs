using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// As runas que um monstro teria trocando algumas, sem equipar nada: cada runa que chega toma o
	/// espaço dela, e a que estava lá sai. É a prévia da tela de Runas: a ficha com essas runas
	/// (<see cref="Progression.SummonStats"/>) mostra o que sobe e o que desce, com os conjuntos que fecham
	/// ou abrem, antes de equipar.
	/// </summary>
	public static class RuneSwap
	{
		/// <param name="equipped">As runas do monstro agora.</param>
		/// <param name="incoming">As que chegam; duas no mesmo espaço, fica a última.</param>
		public static IReadOnlyList<Rune> Apply(IEnumerable<Rune> equipped, IEnumerable<Rune> incoming)
		{
			var bySlot = new SortedDictionary<int, Rune>();
			foreach (var rune in equipped.Concat(incoming))
				bySlot[rune.Slot] = rune;
			return bySlot.Values.ToList();
		}
	}
}
