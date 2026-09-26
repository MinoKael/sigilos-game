using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// A busca de runas do inventário: conjunto, espaço, principal, subatributos (a runa precisa ter
	/// todos; o nativo conta), estrelas, raridade e melhora mínimas, e a ordem. Campo nulo ou vazio não
	/// filtra.
	/// </summary>
	public sealed record RuneFilter
	{
		public RuneSet? Set { get; init; }
		public int? Slot { get; init; }
		public RuneStat? Main { get; init; }
		public IReadOnlyList<RuneStat> Substats { get; init; } = Array.Empty<RuneStat>();
		public int MinGrade { get; init; } = 1;
		public RuneRarity MinRarity { get; init; }
		public int MinLevel { get; init; }
		public RuneSort Sort { get; init; } = RuneSort.Grade;

		public bool Matches(Rune rune) =>
			(Set == null || rune.Set == Set) &&
			(Slot == null || rune.Slot == Slot) &&
			(Main == null || rune.Main == Main) &&
			Substats.All(stat => rune.Innate?.Stat == stat || rune.Substats.Any(s => s.Stat == stat)) &&
			rune.Grade >= MinGrade &&
			rune.Rarity >= MinRarity &&
			rune.Level >= MinLevel;

		/// <summary>As runas que passam, na ordem escolhida (a maior primeiro; empate pelas estrelas e pela melhora).</summary>
		public IEnumerable<Rune> Apply(IEnumerable<Rune> runes)
		{
			var matching = runes.Where(Matches);
			var ordered = Sort switch
			{
				RuneSort.Level => matching.OrderByDescending(r => r.Level).ThenByDescending(r => r.Grade),
				RuneSort.Rarity => matching.OrderByDescending(r => r.Rarity).ThenByDescending(r => r.Grade),
				RuneSort.Set => matching.OrderBy(r => r.Set).ThenByDescending(r => r.Grade),
				RuneSort.Slot => matching.OrderBy(r => r.Slot).ThenByDescending(r => r.Grade),
				RuneSort.Newest => matching.OrderByDescending(r => r.Id),
				_ => matching.OrderByDescending(r => r.Grade).ThenByDescending(r => r.Level),
			};

			return ordered.ThenByDescending(r => r.Rarity).ThenBy(r => r.Id);
		}
	}
}
