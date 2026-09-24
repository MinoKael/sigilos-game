using System;
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
		private static void FirstSummonEverIsFiveStars()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1));
			var results = SummonRitual.Perform(new Random(7), database, player, 1, Array.Empty<Glyph>());
			Assert.Equal(5, results.Single().Summon.Rarity, "primeira invocação");
		}

		[Test]
		private static void PityGuaranteesFiveStarsOnSixtiethPull()
		{
			var database = TestData.LoadReal();
			var player = Player();
			player.PullsSinceFiveStar = SummonRates.Pity - 1;
			var summon = SummonRitual.Roll(new Random(1), database, player, Array.Empty<Glyph>());
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
				counts[SummonRitual.Roll(random, database, player, Array.Empty<Glyph>()).Rarity]++;
			}

			Assert.Near(0.65, counts[3] / (double)pulls, "taxa de 3★", 0.02);
			Assert.Near(0.28, counts[4] / (double)pulls, "taxa de 4★", 0.02);
			Assert.Near(0.07, counts[5] / (double)pulls, "taxa de 5★", 0.01);
		}

		[Test]
		private static void DirectingWithOneGlyphGivesAboutHalf()
		{
			var database = TestData.LoadReal();
			var player = Player();
			var random = new Random(3);
			const int pulls = 10_000;
			var hits = 0;
			for (var i = 0; i < pulls; i++)
			{
				player.PullsSinceFiveStar = 0;
				if (SummonRitual.Roll(random, database, player, new[] { Glyph.Eye }).Glyph == Glyph.Eye)
					hits++;
			}

			// Metade direcionada, mais o que o sorteio livre já daria de Olho.
			Assert.True(hits / (double)pulls > 0.5, $"Olho em {hits} de {pulls}");
		}

		[Test]
		private static void DuplicatesBecomeEchoesThenFragments()
		{
			var database = TestData.LoadReal();
			var player = Player();
			var summon = database.Summon("fenix_fogo");

			Assert.Equal(SummonOutcome.New, SummonRitual.Receive(player, summon).Outcome, "primeira cópia");
			for (var echo = 1; echo <= 5; echo++)
				Assert.Equal(echo, SummonRitual.Receive(player, summon).Echoes, $"Eco {echo}");

			var extra = SummonRitual.Receive(player, summon);
			Assert.Equal(SummonOutcome.Fragments, extra.Outcome, "sexta duplicata");
			Assert.Equal(20, player.Fragments, "5★ vira 20 Fragmentos");
		}

		[Test]
		private static void TenSummonCostsTenAndNeedsScrolls()
		{
			var database = TestData.LoadReal();
			var player = Player(scrolls: 9);
			Assert.Equal(0, SummonRitual.Perform(new Random(1), database, player, 10, Array.Empty<Glyph>()).Count, "sem Pergaminhos");
			Assert.Equal(9, player.Scrolls, "nada gasto");

			player.Scrolls = 10;
			Assert.Equal(10, SummonRitual.Perform(new Random(1), database, player, 10, Array.Empty<Glyph>()).Count, "dez invocações");
			Assert.Equal(0, player.Scrolls, "10 gastos");
		}

		[Test]
		private static void OnlyKnownGlyphsDirect()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1));
			var known = SummonRitual.KnownGlyphs(player, database);
			Assert.True(known.Contains(Glyph.Shard) && known.Contains(Glyph.Bone) && known.Contains(Glyph.Eye), "Glifos do time inicial");
			Assert.False(known.Contains(Glyph.Veil), "Véu ainda desconhecido");
		}
	}
}
