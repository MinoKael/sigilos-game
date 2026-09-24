using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Summoning
{
	/// <summary>
	/// Invocação ritual (GDD, seção 9): paga Pergaminhos, sorteia raridade e variante, respeita a
	/// garantia e o direcionamento por Glifo, e entrega o resultado à coleção.
	///
	/// O sorteio recebe o <see cref="Random"/> de fora: com a mesma semente, o mesmo resultado.
	/// </summary>
	public static class SummonRitual
	{
		/// <summary>Glifos que o jogador conhece: os das invocações que já tem.</summary>
		public static IReadOnlyList<Glyph> KnownGlyphs(PlayerState player, GameDatabase database) => database.Summons
			.Where(s => player.Owns(s.Id))
			.Select(s => s.Glyph)
			.Distinct()
			.OrderBy(g => g)
			.ToList();

		/// <summary>Quantas invocações faltam para a 5★ garantida, contando a próxima.</summary>
		public static int PullsUntilPity(PlayerState player) => SummonRates.Pity - player.PullsSinceFiveStar;

		public static int CostFor(int count) => count >= 10 ? SummonRates.TenCost : SummonRates.SingleCost * count;

		/// <summary>
		/// Faz <paramref name="count"/> invocações (1 ou 10). Sem Pergaminhos suficientes, não faz nada
		/// e devolve lista vazia. Glifos desconhecidos ou além de dois são ignorados.
		/// </summary>
		public static IReadOnlyList<SummonResult> Perform(Random random, GameDatabase database, PlayerState player, int count, IReadOnlyList<Glyph> glyphs)
		{
			var cost = CostFor(count);
			if (player.Scrolls < cost)
				return Array.Empty<SummonResult>();

			var known = KnownGlyphs(player, database);
			var directed = glyphs.Where(known.Contains).Distinct().Take(SummonRates.MaxDirectedGlyphs).ToList();

			player.Scrolls -= cost;
			var results = new List<SummonResult>();
			for (var i = 0; i < count; i++)
				results.Add(Receive(player, Roll(random, database, player, directed)));

			return results;
		}

		/// <summary>Sorteia uma invocação e atualiza os contadores de garantia. Não mexe na coleção.</summary>
		public static SummonDefinition Roll(Random random, GameDatabase database, PlayerState player, IReadOnlyList<Glyph> directed)
		{
			var rarity = RollRarity(random, player, database);
			var pool = database.Summons.Where(s => s.Rarity == rarity).ToList();
			pool = Direct(random, pool, directed);

			var summon = WeightedPick(random, pool);
			player.TotalPulls++;
			player.PullsSinceFiveStar = summon.Rarity == 5 ? 0 : player.PullsSinceFiveStar + 1;
			return summon;
		}

		/// <summary>Põe a invocação na coleção: nova, +1 Eco, ou Fragmentos quando os Ecos já estão cheios.</summary>
		public static SummonResult Receive(PlayerState player, SummonDefinition summon)
		{
			if (!player.Owns(summon.Id))
			{
				player.Summons[summon.Id] = new OwnedSummon();
				return new SummonResult(summon, SummonOutcome.New, 0, 0);
			}

			var owned = player.Summon(summon.Id);
			if (owned.Echoes < Growth.MaxEchoes)
			{
				owned.Echoes++;
				return new SummonResult(summon, SummonOutcome.Echo, owned.Echoes, 0);
			}

			var fragments = SummonRates.FragmentsFor(summon.Rarity);
			player.Fragments += fragments;
			return new SummonResult(summon, SummonOutcome.Fragments, owned.Echoes, fragments);
		}

		private static int RollRarity(Random random, PlayerState player, GameDatabase database)
		{
			int rarity;
			if (player.TotalPulls == 0 || player.PullsSinceFiveStar >= SummonRates.Pity - 1)
				rarity = 5;
			else
			{
				var roll = random.NextDouble();
				rarity = roll < SummonRates.FiveStar ? 5 : roll < SummonRates.FiveStar + SummonRates.FourStar ? 4 : 3;
			}

			// Conteúdo incompleto (uma raridade sem família) cai para a raridade mais próxima que existe.
			var available = database.Summons.Select(s => s.Rarity).Distinct().ToList();
			return available.Contains(rarity) ? rarity : available.OrderBy(r => Math.Abs(r - rarity)).First();
		}

		private static List<SummonDefinition> Direct(Random random, List<SummonDefinition> pool, IReadOnlyList<Glyph> directed)
		{
			if (directed.Count == 0)
				return pool;

			var roll = random.NextDouble();
			Glyph? chosen = null;
			if (directed.Count == 1 && roll < SummonRates.OneGlyphShare)
				chosen = directed[0];
			else if (directed.Count == 2 && roll < SummonRates.TwoGlyphShare)
				chosen = directed[0];
			else if (directed.Count == 2 && roll < 2 * SummonRates.TwoGlyphShare)
				chosen = directed[1];

			if (chosen == null)
				return pool;

			var narrowed = pool.Where(s => s.Glyph == chosen).ToList();
			return narrowed.Count > 0 ? narrowed : pool;
		}

		private static SummonDefinition WeightedPick(Random random, IReadOnlyList<SummonDefinition> pool)
		{
			static double Weight(SummonDefinition s) => s.Element is Element.Light or Element.Dark ? SummonRates.LightDarkWeight : 1;

			var roll = random.NextDouble() * pool.Sum(Weight);
			foreach (var summon in pool)
			{
				roll -= Weight(summon);
				if (roll < 0)
					return summon;
			}

			return pool[^1];
		}
	}
}
