using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	internal static class SummonTests
	{
		private static PlayerState Player(int scrolls = 1000) => new() { Scrolls = scrolls, TotalPulls = 1 };

		[Test]
		private static void FirstTenBringTheFireKnightAndAFiveStar()
		{
			var database = TestData.LoadReal();
			for (var seed = 1; seed <= 5; seed++)
			{
				var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), database);
				var results = SummonRitual.Perform(new Random(seed), database, player, 10);
				Assert.Equal(SummonRates.FirstSummon, results[0].Summon.Id, $"a primeira é o Cavaleiro de Fogo, semente {seed}");
				Assert.Equal(5, results[1].Summon.Rarity, $"a segunda, uma 5★, semente {seed}");
			}

			var single = NewGame.Create(DateTime.UnixEpoch, new Random(1), database);
			SummonRitual.Perform(new Random(7), database, single, 1);
			Assert.Equal(5, SummonRitual.Perform(new Random(7), database, single, 1).Single().Summon.Rarity, "uma a uma, a 5★ vem logo depois do Cavaleiro");
		}

		[Test]
		private static void PityGuaranteesAFiveStar()
		{
			var database = TestData.LoadReal();
			var player = Player();
			player.PullsSinceFiveStar = SummonRates.Pity - 1;
			var summon = SummonRitual.Roll(new Random(1), database, player);
			Assert.Equal(5, summon.Rarity, "a invocação da garantia, sem 5★ antes, é 5★");
			Assert.Equal(0, player.PullsSinceFiveStar, "contador zera");
		}

		[Test]
		private static void TenSummonThroughThePityBringsTheFiveStar()
		{
			// Em 148/150, a ×10 passa pela garantia: a segunda é a 150ª, e ela vem na lista dos dez.
			var database = TestData.LoadReal();
			for (var seed = 1; seed <= 20; seed++)
			{
				var player = Player();
				player.TotalPulls = 400;
				player.PullsSinceFiveStar = SummonRates.Pity - 2;
				var results = SummonRitual.Perform(new Random(seed), database, player, 10);
				Assert.Equal(10, results.Count, "dez");
				Assert.True(results.Take(2).Any(r => r.Summon.Rarity == 5), $"a 5★ até a 150ª, semente {seed}");
				Assert.True(results.Where(r => r.Summon.Rarity == 5).All(r => player.Monsters.Contains(r.Monster)), "e ela entra na coleção");
			}
		}

		[Test]
		private static void RatesAreCloseToTheTable()
		{
			var database = TestData.LoadReal();
			var player = Player();
			const int pulls = 20_000;
			foreach (var kind in Enum.GetValues<ScrollKind>())
			{
				var random = new Random(42);
				var counts = new int[6];
				for (var i = 0; i < pulls; i++)
				{
					player.PullsSinceFiveStar = 0;
					var summon = SummonRitual.Roll(random, database, player, kind);
					var mysticLightDark = kind == ScrollKind.Mystic && SummonRates.MysticLightDarkEligible(summon);
					Assert.True(SummonRates.Allows(kind, summon.Element) || mysticLightDark, $"{kind}: {summon.Id} é de um elemento que o pergaminho não tira");
					counts[summon.Rarity]++;
				}

				// As taxas da tabela (SummonRates); o que sobra é 3★.
				var (five, four) = SummonRates.Of(kind);
				Assert.Near(1 - four - five, counts[3] / (double)pulls, $"{kind}: taxa de 3★", 0.02);
				Assert.Near(four, counts[4] / (double)pulls, $"{kind}: taxa de 4★", 0.02);
				Assert.Near(five, counts[5] / (double)pulls, $"{kind}: taxa de 5★", 0.01);
			}
		}

		/// <summary>
		/// Transmutação Alquímica: 0,1% de LD no total, tirado da fatia das 3★ e repartido por igual entre os
		/// LD elegíveis (0,1% / N); as outras taxas não mudam.
		/// </summary>
		[Test]
		private static void MysticLightDarkIsATenthOfAPercentSharedEvenly()
		{
			var database = TestData.LoadReal();
			var player = Player();
			var eligible = database.Summons.Where(SummonRates.MysticLightDarkEligible).ToList();
			Assert.True(eligible.Count > 0, "há LD elegível");
			Assert.True(eligible.All(s => s.Element is Element.Light or Element.Dark), "só Luz e Trevas");
			Assert.Near(0.001, SummonRates.MysticLightDark, "a taxa base é 0,1%", 1e-12);

			const int pulls = 400_000;
			var random = new Random(7);
			var hits = new Dictionary<string, int>();
			var five = 0;
			for (var i = 0; i < pulls; i++)
			{
				player.PullsSinceFiveStar = 0;
				player.TotalPulls = 5;
				var summon = SummonRitual.Roll(random, database, player, ScrollKind.Mystic);
				if (summon.Rarity == 5)
					five++;
				if (summon.Element is Element.Light or Element.Dark)
					hits[summon.Id] = hits.GetValueOrDefault(summon.Id) + 1;
			}

			var total = hits.Values.Sum() / (double)pulls;
			Assert.Near(SummonRates.MysticLightDark, total, "LD no total", 0.0005);
			Assert.True(hits.Keys.All(id => eligible.Any(s => s.Id == id)), "só os elegíveis saem");
			Assert.Near(SummonRates.Of(ScrollKind.Mystic).FiveStar, five / (double)pulls, "a 5★ não mudou", 0.002);
		}

		[Test]
		private static void RepeatedSummonIsANewUnawakenedCopy()
		{
			var database = TestData.LoadReal();
			var player = Player();
			var summon = database.Summon("wizard_fire");

			var first = SummonRitual.Receive(player, summon);
			first.Monster.Awakened = true;
			first.Monster.Level = 40;

			var second = SummonRitual.Receive(player, summon);
			Assert.True(first.FirstCopy && !second.FirstCopy, "a primeira cópia é marcada");
			Assert.True(second.Monster.Id != first.Monster.Id, "outra cópia, com id próprio");
			Assert.False(second.Monster.Awakened, "a cópia nova não vem desperta");
			Assert.Equal(1, second.Monster.Level, "a cópia nova vem no nível 1");
			Assert.Equal(2, player.Monsters.Count, "duas cópias na conta");
		}

		[Test]
		private static void FullCollectionSendsToTheChest()
		{
			var database = TestData.LoadReal();
			var player = Player();
			var summon = database.Summon("wolf_fire");
			for (var i = 0; i < player.CollectionCapacity; i++)
				SummonRitual.Receive(player, summon);

			var extra = SummonRitual.Receive(player, summon);
			Assert.True(extra.Monster.Stored, "a coleção cheia manda para o Baú");
			Assert.Equal(player.CollectionCapacity, player.Collection.Count(), "a coleção não passa do limite");
		}

		[Test]
		private static void TenSummonCostsTenAndNeedsScrolls()
		{
			var database = TestData.LoadReal();
			var player = Player(scrolls: 9);
			Assert.Equal(0, SummonRitual.Perform(new Random(1), database, player, 10).Count, "sem Pergaminhos");
			Assert.Equal(9, player.Scrolls, "nada gasto");

			player.Scrolls = 10;
			Assert.Equal(10, SummonRitual.Perform(new Random(1), database, player, 10).Count, "dez invocações");
			Assert.Equal(0, player.Scrolls, "10 gastos");
		}
	}
}
