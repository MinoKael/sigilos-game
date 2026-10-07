using System;
using System.Linq;
using System.Text.RegularExpressions;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>O Grimório do Invocador: desde quando a conta existe e os selos, lidos do save.</summary>
	internal static class SealTests
	{
		[Test]
		private static void NewAccountKnowsWhenItStartedAndOldSaveDoesNot()
		{
			var now = new DateTime(2026, 10, 7, 12, 0, 0);
			var player = NewGame.Create(now, new Random(1), TestData.Database);
			Assert.Equal(now, player.Started, "a conta nova anota quando começou");

			var json = PlayerSave.ToJson(player);
			Assert.Equal(now, PlayerSave.FromJson(json)!.Started, "o save guarda a data");
			var old = Regex.Replace(json, @"\s*""Started"":\s*""[^""]*"",", "");
			Assert.True(old != json, "o teste tirou o campo");
			Assert.Equal(null, PlayerSave.FromJson(old)!.Started, "save sem o campo: sem data, nada inventado");
		}

		[Test]
		private static void SealsReadTheSaveWithoutChangingIt()
		{
			var database = TestData.Database;
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), database);
			var seals = Seals.Journey(player, database).Concat(Seals.Collection(player, database)).ToList();
			Assert.Equal(12, seals.Count, "seis da jornada e seis da coleção");
			Assert.Equal(seals.Count, seals.Select(s => s.Id).Distinct().Count(), "ids diferentes (são as chaves dos textos)");
			Assert.False(seals.Any(s => s.Done), "a conta nova não tem selo");
			Assert.True(seals.All(s => s.Goal >= 1), "toda meta pede alguma coisa");

			var boreal = database.Exploration.Range(Hemisphere.Boreal).Last;
			player.HighestStage = database.Stages.Count;
			foreach (var dungeon in database.Dungeons)
				player.DungeonFloors[dungeon.Id] = dungeon.Floors.Count;
			player.ExplorationBest = boreal;
			var before = PlayerSave.ToJson(player);
			var journey = Seals.Journey(player, database).ToDictionary(s => s.Id);
			Assert.Equal(before, PlayerSave.ToJson(player), "ler os selos não muda o save");
			Assert.True(journey["campaign"].Done && journey["first_victory"].Done, "a Campanha inteira lacra os dois");
			Assert.True(journey["all_dungeons"].Done && journey["deep_dungeon"].Done, "todos os andares de todas as Masmorras");
			Assert.True(journey["northern_sky"].Done, "o céu do norte acaba na última Boreal");
			Assert.False(journey["whole_sky"].Done, "o céu inteiro ainda não");
			Assert.Equal(boreal, journey["whole_sky"].Progress, "e mostra quanto já foi");

			Milestones.Grant(player, new Prize(InfusionCores: 2));
			Assert.Equal(0, Seals.Monsters(player).Count(), "Núcleo de Infusão não conta como monstro");
			Roster.Add(player, database.Summon("phoenix_fire"));
			player.TotalPulls = Seals.ManyPulls;
			player.AccountLevel = Account.MaxLevel;
			var collection = Seals.Collection(player, database).ToDictionary(s => s.Id);
			Assert.True(collection["legend"].Done, "uma 5★ na conta");
			Assert.Equal(1, collection["families"].Progress, "uma família");
			Assert.True(collection["pulls"].Done && collection["account"].Done, "as invocações e o nível máximo");
			Assert.False(collection["awakened"].Done, "nenhum desperto ainda");
		}
	}
}
