using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>O delta do save: aplicar o que o cliente mandou dá o mesmo save, por qualquer caminho de mudança.</summary>
	internal static class SaveDeltaTests
	{
		private static PlayerState Player(Random random)
		{
			var player = NewGame.Create(DateTime.UnixEpoch, random, TestData.Database);
			player.Essence = 5_000_000;
			foreach (var summon in TestData.Database.Summons.Take(12))
				Roster.Add(player, summon);
			for (var i = 0; i < 40; i++)
				RuneInventory.Create(random, player, 6, null, RuneRarity.Rare, 1 + i % 6);
			return player;
		}

		private static string Json(PlayerState player) => PlayerSave.ToJson(player);

		[Test]
		private static void TheSaveSurvivesALoadAndSaveRoundTrip()
		{
			var json = Json(Player(new Random(1)));
			Assert.Equal(json, Json(PlayerSave.FromJson(json)!), "ler e gravar não muda nada: o hash do delta depende disso");
		}

		[Test]
		private static void ApplyingTheDiffGivesTheSameSaveForRandomChanges()
		{
			var random = new Random(7);
			var player = Player(random);
			var before = Json(player);
			var smallest = int.MaxValue;
			for (var round = 0; round < 150; round++)
			{
				var next = PlayerSave.FromJson(before)!;
				for (var step = random.Next(1, 5); step > 0; step--)
					Mutate(random, next);

				var after = Json(next);
				var delta = SaveDelta.Diff(before, after);
				Assert.True(delta != null, "diff");
				var applied = SaveDelta.Apply(before, delta!);
				Assert.True(applied != null, $"aplica (rodada {round})");
				Assert.Equal(after, Json(PlayerSave.FromJson(applied!)!), $"mesmo save (rodada {round})");
				smallest = Math.Min(smallest, delta!.Length);
				before = after;
			}

			Assert.True(smallest < before.Length / 20, $"uma mudança pequena é um delta pequeno: {smallest} de {before.Length}");
		}

		[Test]
		private static void OneEquippedRuneAndOneLevelAreTinyComparedToTheSave()
		{
			var player = Player(new Random(3));
			var before = Json(player);
			var next = PlayerSave.FromJson(before)!;
			next.Monsters[0].Level = 5;
			next.Runes[0].EquippedOn = next.Monsters[0].Id;
			next.Essence -= 100;
			var delta = SaveDelta.Diff(before, Json(next))!;
			Assert.True(delta.Length < 1500, $"delta de {delta.Length} bytes");
			Assert.True(delta.Length * 20 < before.Length, $"contra {before.Length} do save");
		}

		[Test]
		private static void ADeltaForAnotherSaveIsRefused()
		{
			var random = new Random(9);
			var player = Player(random);
			var before = Json(player);
			var next = PlayerSave.FromJson(before)!;
			next.Runes.RemoveAt(3);
			var delta = SaveDelta.Diff(before, Json(next))!;

			Assert.True(SaveDelta.Apply("{}", delta) == null, "chaves que não existem");
			Assert.True(SaveDelta.Apply("[]", delta) == null, "não é objeto");
			Assert.True(SaveDelta.Apply(before, "{\"lists\":{\"Runes\":{\"order\":[999999],\"put\":[]}}}") == null, "id que não existe");
			Assert.True(SaveDelta.Apply(before, "não é json") == null, "lixo");
		}

		private static void Mutate(Random random, PlayerState player)
		{
			switch (random.Next(8))
			{
				case 0:
					player.Essence -= random.Next(1, 100);
					break;
				case 1:
					player.Monsters[random.Next(player.Monsters.Count)].Level = random.Next(1, 20);
					break;
				case 2:
					if (player.Runes.Count > 0)
						player.Runes[random.Next(player.Runes.Count)].EquippedOn = player.Monsters[random.Next(player.Monsters.Count)].Id;
					break;
				case 3:
					if (player.Runes.Count > 5)
						player.Runes.RemoveAt(random.Next(player.Runes.Count));
					break;
				case 4:
					RuneInventory.Create(random, player, 6, null, RuneRarity.Rare, 1 + random.Next(6));
					break;
				case 5:
					if (player.Runes.Count > 1)
					{
						var rune = player.Runes[0];
						player.Runes.RemoveAt(0);
						player.Runes.Insert(random.Next(player.Runes.Count + 1), rune);
					}

					break;
				case 6:
					player.Gold += random.Next(1, 1000);
					break;
				default:
					if (player.Monsters.Count > 3)
						player.Monsters.RemoveAt(random.Next(player.Monsters.Count));
					break;
			}
		}
	}
}
