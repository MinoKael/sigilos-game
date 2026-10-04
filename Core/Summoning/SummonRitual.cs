using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Summoning
{
	/// <summary>
	/// Invocação ritual (GDD, seção 9): paga Pergaminhos do tipo escolhido (<see cref="ScrollKind"/>), sorteia
	/// raridade e variante pelas taxas dele, respeita a garantia do Místico e entrega uma cópia nova à coleção
	/// (ou ao Baú, se a coleção está cheia). A conta começa com duas invocações Místicas certas: a primeira é
	/// o <see cref="SummonRates.FirstSummon"/>, e a segunda, uma 5★.
	///
	/// O sorteio recebe o <see cref="Random"/> de fora: com a mesma semente, o mesmo resultado.
	/// </summary>
	public static class SummonRitual
	{
		/// <summary>Quantas invocações Místicas faltam para a 5★ garantida, contando a próxima.</summary>
		public static int PullsUntilPity(PlayerState player) => SummonRates.Pity - player.PullsSinceFiveStar;

		public static int CostFor(int count) => count >= 10 ? SummonRates.TenCost : SummonRates.SingleCost * count;

		/// <summary>Quantos Pergaminhos deste tipo a conta tem.</summary>
		public static int Scrolls(PlayerState player, ScrollKind kind) => kind switch
		{
			ScrollKind.LightDark => player.LightDarkScrolls,
			ScrollKind.Legendary => player.LegendaryScrolls,
			_ => player.Scrolls,
		};

		/// <summary>Soma (ou tira, com número negativo) Pergaminhos deste tipo.</summary>
		public static void AddScrolls(PlayerState player, ScrollKind kind, int amount)
		{
			switch (kind)
			{
				case ScrollKind.LightDark:
					player.LightDarkScrolls += amount;
					break;
				case ScrollKind.Legendary:
					player.LegendaryScrolls += amount;
					break;
				default:
					player.Scrolls += amount;
					break;
			}
		}

		/// <summary>
		/// Faz <paramref name="count"/> invocações (1 ou 10) com Pergaminhos de <paramref name="kind"/>. Sem
		/// Pergaminhos suficientes, não faz nada e devolve lista vazia.
		/// </summary>
		public static IReadOnlyList<SummonResult> Perform(Random random, GameDatabase database, PlayerState player, int count, ScrollKind kind = ScrollKind.Mystic)
		{
			var cost = CostFor(count);
			if (Scrolls(player, kind) < cost)
				return Array.Empty<SummonResult>();

			AddScrolls(player, kind, -cost);
			var results = new List<SummonResult>();
			for (var i = 0; i < count; i++)
				results.Add(Receive(player, Roll(random, database, player, kind)));

			return results;
		}

		/// <summary>
		/// Sorteia uma invocação e atualiza os contadores. Não mexe na coleção. A garantia e o começo certo da
		/// conta são só do Místico; uma 5★ de qualquer pergaminho zera a contagem da garantia.
		/// </summary>
		public static SummonDefinition Roll(Random random, GameDatabase database, PlayerState player, ScrollKind kind = ScrollKind.Mystic)
		{
			SummonDefinition summon;
			if (kind == ScrollKind.Mystic && player.TotalPulls == 0 && database.HasSummon(SummonRates.FirstSummon))
			{
				summon = database.Summon(SummonRates.FirstSummon);
			}
			else
			{
				var rarity = RollRarity(random, player, database, kind);
				var pool = database.Summons.Where(s => s.Rarity == rarity && SummonRates.Allows(kind, s.Element)).ToList();
				summon = pool[random.Next(pool.Count)];
			}

			player.TotalPulls++;
			if (summon.Rarity == 5)
				player.PullsSinceFiveStar = 0;
			else if (kind == ScrollKind.Mystic)
				player.PullsSinceFiveStar++;
			return summon;
		}

		/// <summary>Cria a cópia nova: nível 1, sem Despertar, mesmo que a conta já tenha outra.</summary>
		public static SummonResult Receive(PlayerState player, SummonDefinition summon)
		{
			var firstCopy = !player.Owns(summon.Id);
			return new SummonResult(summon, Roster.Add(player, summon), firstCopy);
		}

		private static int RollRarity(Random random, PlayerState player, GameDatabase database, ScrollKind kind)
		{
			int rarity;
			// A segunda invocação da conta, logo depois da primeira sem 5★, é a 5★ garantida.
			var mystic = kind == ScrollKind.Mystic;
			var firstFiveStar = mystic && player.TotalPulls <= 1 && player.PullsSinceFiveStar == player.TotalPulls;
			if (firstFiveStar || (mystic && player.PullsSinceFiveStar >= SummonRates.Pity - 1))
				rarity = 5;
			else
			{
				var (five, four) = SummonRates.Of(kind);
				var roll = random.NextDouble();
				rarity = roll < five ? 5 : roll < five + four ? 4 : 3;
			}

			// Conteúdo incompleto (uma raridade sem família no pergaminho) cai para a raridade mais próxima que existe.
			var available = database.Summons.Where(s => SummonRates.Allows(kind, s.Element)).Select(s => s.Rarity).Distinct().ToList();
			return available.Contains(rarity) ? rarity : available.OrderBy(r => Math.Abs(r - rarity)).First();
		}

		/// <summary>O sorteio de variante com Luz e Trevas mais raras (<see cref="SummonRates.LightDarkWeight"/>): o drop de 2★ da Campanha.</summary>
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
