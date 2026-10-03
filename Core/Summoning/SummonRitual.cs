using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Summoning
{
	/// <summary>
	/// Invocação ritual (GDD, seção 9): paga Pergaminhos, sorteia raridade e variante, respeita a
	/// garantia e entrega uma cópia nova à coleção (ou ao Baú, se a coleção está cheia). A conta começa
	/// com duas invocações certas: a primeira é o <see cref="SummonRates.FirstSummon"/>, e a segunda, uma 5★.
	///
	/// O sorteio recebe o <see cref="Random"/> de fora: com a mesma semente, o mesmo resultado.
	/// </summary>
	public static class SummonRitual
	{
		/// <summary>Quantas invocações faltam para a 5★ garantida, contando a próxima.</summary>
		public static int PullsUntilPity(PlayerState player) => SummonRates.Pity - player.PullsSinceFiveStar;

		public static int CostFor(int count) => count >= 10 ? SummonRates.TenCost : SummonRates.SingleCost * count;

		/// <summary>
		/// Faz <paramref name="count"/> invocações (1 ou 10). Sem Pergaminhos suficientes, não faz nada
		/// e devolve lista vazia.
		/// </summary>
		public static IReadOnlyList<SummonResult> Perform(Random random, GameDatabase database, PlayerState player, int count)
		{
			var cost = CostFor(count);
			if (player.Scrolls < cost)
				return Array.Empty<SummonResult>();

			player.Scrolls -= cost;
			var results = new List<SummonResult>();
			for (var i = 0; i < count; i++)
				results.Add(Receive(player, Roll(random, database, player)));

			return results;
		}

		/// <summary>Sorteia uma invocação e atualiza os contadores de garantia. Não mexe na coleção.</summary>
		public static SummonDefinition Roll(Random random, GameDatabase database, PlayerState player)
		{
			SummonDefinition summon;
			if (player.TotalPulls == 0 && database.HasSummon(SummonRates.FirstSummon))
			{
				summon = database.Summon(SummonRates.FirstSummon);
			}
			else
			{
				var rarity = RollRarity(random, player, database);
				summon = WeightedPick(random, database.Summons.Where(s => s.Rarity == rarity).ToList());
			}

			player.TotalPulls++;
			player.PullsSinceFiveStar = summon.Rarity == 5 ? 0 : player.PullsSinceFiveStar + 1;
			return summon;
		}

		/// <summary>Cria a cópia nova: nível 1, sem Despertar, mesmo que a conta já tenha outra.</summary>
		public static SummonResult Receive(PlayerState player, SummonDefinition summon)
		{
			var firstCopy = !player.Owns(summon.Id);
			return new SummonResult(summon, Roster.Add(player, summon), firstCopy);
		}

		private static int RollRarity(Random random, PlayerState player, GameDatabase database)
		{
			int rarity;
			// A segunda invocação da conta, logo depois da primeira sem 5★, é a 5★ garantida.
			var firstFiveStar = player.TotalPulls <= 1 && player.PullsSinceFiveStar == player.TotalPulls;
			if (firstFiveStar || player.PullsSinceFiveStar >= SummonRates.Pity - 1)
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

		public static SummonDefinition WeightedPick(Random random, IReadOnlyList<SummonDefinition> pool)
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
