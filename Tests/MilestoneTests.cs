using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>O Núcleo de Infusão, os marcos que pagam o que é especial e a troca de Fragmentos.</summary>
	internal static class MilestoneTests
	{
		private static PlayerState NewPlayer() => NewGame.Create(DateTime.UnixEpoch, new Random(1), TestData.Database);

		[Test]
		private static void RegionEndsAndDungeonFloorsPayTheirMilestonesOnce()
		{
			var database = TestData.Database;
			var player = NewPlayer();
			player.HighestStage = 19;
			player.AccountLevel = Account.MaxLevel;
			var stage = database.Stage(20);
			var first = Campaign.ApplyVictory(new Random(1), player, stage, database);
			Assert.Equal(new Prize(0, 0, 2, 0, 1), first.Prize, "o fim da região 1 dá um Kit de Sigilos e dois Núcleos");
			Assert.Equal(1, player.PendingRuneKits, "o Kit espera na conta");
			Assert.Equal(2, player.Monsters.Count(m => m.IsInfusionCore), "os Núcleos na coleção");
			Assert.Equal(Prize.None, Campaign.ApplyVictory(new Random(1), player, stage, database).Prize, "repetir não dá de novo");

			var golem = database.Dungeon("golem");
			player.HighestStage = golem.UnlockStage;
			player.DungeonFloors[golem.Id] = 3;
			var fourth = Dungeons.ApplyVictory(new Random(1), player, golem, 4);
			Assert.Equal(1, fourth.Prize!.LegendaryScrolls, "o andar 4 dá um Lendário");
			Assert.True(fourth.Prize.InfusionCores >= golem.Floor(4).FirstClearCores, "e os Núcleos dele");
			var fifth = Dungeons.ApplyVictory(new Random(1), player, golem, 5);
			Assert.Equal(1, fifth.Prize!.LightDarkScrolls, "o andar 5 dá um de Luz e Trevas");
			Assert.Equal(1, player.LightDarkScrolls, "na conta");

			var cores = 0;
			for (var seed = 0; seed < 2000; seed++)
				cores += Dungeons.ApplyVictory(new Random(seed), player, golem, 5).Prize!.InfusionCores;
			Assert.True(Math.Abs(cores / 2000.0 * 100 - golem.Floor(5).CoreChance) < 1.2, $"repetir o andar 5 dá Núcleo pela chance dele ({cores} em 2000)");
		}

		[Test]
		private static void SpecialScrollsSummonOnlyWhatTheyPromise()
		{
			var database = TestData.Database;
			var player = NewPlayer();
			player.TotalPulls = 2;
			player.LegendaryScrolls = 10;
			player.LightDarkScrolls = 10;
			var legendary = SummonRitual.Perform(new Random(3), database, player, 10, ScrollKind.Legendary);
			Assert.Equal(10, legendary.Count, "dez Lendárias");
			Assert.True(legendary.All(r => r.Summon.Rarity >= 4 && SummonRates.Allows(ScrollKind.Legendary, r.Summon.Element)), "só 4★ e 5★ de Fogo, Água e Vento");
			var lightDark = SummonRitual.Perform(new Random(3), database, player, 10, ScrollKind.LightDark);
			Assert.True(lightDark.All(r => r.Summon.Element is Element.Light or Element.Dark), "só Luz e Trevas");
			Assert.Equal(0, player.LegendaryScrolls + player.LightDarkScrolls, "cada uma gasta o pergaminho dela");
			Assert.Equal(0, SummonRitual.Perform(new Random(3), database, player, 1, ScrollKind.Legendary).Count, "sem pergaminho, nada");
		}

	}
}
