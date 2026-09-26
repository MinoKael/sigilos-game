using System;
using System.Linq;
using Sigilos.Core.Player;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	internal static class SummonTests
	{
		private static PlayerState Player(int scrolls = 1000) => new() { Scrolls = scrolls, TotalPulls = 1 };

		[Test]
		private static void FirstSummonEverIsFiveStars()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1));
			var results = SummonRitual.Perform(new Random(7), database, player, 1);
			Assert.Equal(5, results.Single().Summon.Rarity, "primeira invocação");
		}

		[Test]
		private static void PityGuaranteesFiveStarsOnSixtiethPull()
		{
			var database = TestData.LoadReal();
			var player = Player();
			player.PullsSinceFiveStar = SummonRates.Pity - 1;
			var summon = SummonRitual.Roll(new Random(1), database, player);
			Assert.Equal(5, summon.Rarity, "60ª invocação sem 5★");
			Assert.Equal(0, player.PullsSinceFiveStar, "contador zera");
		}

		[Test]
		private static void RatesAreCloseToTheTable()
		{
			var database = TestData.LoadReal();
			var player = Player();
			var random = new Random(42);
			var counts = new int[6];
			const int pulls = 20_000;
			for (var i = 0; i < pulls; i++)
			{
				player.PullsSinceFiveStar = 0;
				counts[SummonRitual.Roll(random, database, player).Rarity]++;
			}

			Assert.Near(0.65, counts[3] / (double)pulls, "taxa de 3★", 0.02);
			Assert.Near(0.28, counts[4] / (double)pulls, "taxa de 4★", 0.02);
			Assert.Near(0.07, counts[5] / (double)pulls, "taxa de 5★", 0.01);
		}

		[Test]
		private static void RepeatedSummonIsANewUnawakenedCopy()
		{
			var database = TestData.LoadReal();
			var player = Player();
			var summon = database.Summon("phoenix_fire");

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
			var summon = database.Summon("imp_fire");
			for (var i = 0; i < PlayerState.CollectionCapacity; i++)
				SummonRitual.Receive(player, summon);

			var extra = SummonRitual.Receive(player, summon);
			Assert.True(extra.Monster.Stored, "a coleção cheia manda para o Baú");
			Assert.Equal(PlayerState.CollectionCapacity, player.Collection.Count(), "a coleção não passa do limite");
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
