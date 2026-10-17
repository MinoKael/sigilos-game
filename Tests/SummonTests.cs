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
