using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Junta as fontes de atributo de uma invocação: os atributos da variante no 6★ nível 40 (os de
	/// base ou os despertos, prontos em Data/summons), encolhidos para as estrelas e o nível de agora
	/// (<see cref="Growth"/>), o bônus de atributo do <see cref="Awakening"/> e as runas
	/// (<see cref="RuneBonuses"/>). A tela de Monstros e a batalha chamam o mesmo cálculo, então o
	/// número que o jogador vê é o que luta.
	/// </summary>
	public static class SummonStats
	{
		public static StatSheet For(
			SummonDefinition summon,
			int stars,
			int level,
			bool awakened,
			IReadOnlyCollection<Rune> runes)
		{
			var stats = Growth.Stats(summon.StatsFor(awakened), stars, level);
			if (awakened)
				stats = Awakening.Apply(stats, summon.Awakening);

			// Runa de conjunto fora do jogo (RuneSets.ActiveSets) fica no save, mas não soma atributo nem conjunto.
			var active = runes.Where(rune => RuneSets.IsActive(rune.Set)).ToList();
			return new StatSheet(stats, RuneBonuses.Compute(stats, active));
		}
	}
}
