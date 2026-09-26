using System.Collections.Generic;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Junta as três fontes de atributo de uma invocação: estrelas e nível (<see cref="Growth"/>),
	/// <see cref="Awakening"/> e runas (<see cref="RuneBonuses"/>). A tela de Monstros e a batalha
	/// chamam o mesmo cálculo, então o número que o jogador vê é o que luta.
	/// </summary>
	public static class SummonStats
	{
		public static StatSheet For(
			StatBlock roleBase,
			SummonDefinition summon,
			int stars,
			int level,
			bool awakened,
			IReadOnlyCollection<Rune> runes)
		{
			var stats = Growth.Stats(roleBase, summon.Rarity, stars, level);
			if (awakened)
				stats = Awakening.Apply(stats, summon.Awakening);

			return new StatSheet(stats, RuneBonuses.Compute(stats, runes));
		}
	}
}
