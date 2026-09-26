using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	internal static class DungeonTests
	{
		[Test]
		private static void DungeonOpensAfterItsStageAndFloorsInOrder()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			var golem = database.Dungeon("golem");

			Assert.Equal(EntryProblem.Locked, Dungeons.Check(player, golem, 1), "fechada antes da fase");
			player.HighestStage = golem.UnlockStage;
			Assert.False(Dungeons.IsFloorUnlocked(player, golem, 2), "andar 2 espera o 1");

			// Conta no nível máximo: a vitória não enche a Mana, então dá para ver o custo.
			player.AccountLevel = Account.MaxLevel;
			var mana = player.Mana;
			Assert.Equal(EntryProblem.None, Dungeons.Check(player, golem, 1), "entra");
			Assert.Equal(mana, player.Mana, "entrar não cobra: a derrota não custa nada");

			var first = Dungeons.ApplyVictory(new Random(1), player, golem, 1);
			Assert.Equal(golem.Floor(1).Mana, first.Mana, "a vitória cobra a Mana do andar");
			Assert.Equal(mana - golem.Floor(1).Mana, player.Mana, "Mana na conta");
			Assert.True(Dungeons.IsFloorUnlocked(player, golem, 2), "vencer abre o próximo");
			Assert.Equal(golem.Floor(1).FirstClearGold, first.Gold, "Ouro da primeira vitória");
			Assert.Equal(0, Dungeons.ApplyVictory(new Random(1), player, golem, 1).Gold, "repetir não dá Ouro");

			player.Mana = golem.Floor(1).Mana - 1;
			Assert.Equal(EntryProblem.NoMana, Dungeons.Check(player, golem, 1), "sem a Mana da vitória não entra");
		}

		[Test]
		private static void RuneDungeonDropsBigRunesOfItsSets()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			var golem = database.Dungeon("golem");
			var random = new Random(4);

			for (var floor = 1; floor <= golem.Floors.Count; floor++)
			{
				var rules = golem.Floor(floor);
				for (var i = 0; i < 30; i++)
				{
					var rune = Dungeons.ApplyVictory(random, player, golem, floor).Rune!;
					Assert.True(golem.Sets.Contains(rune.Set), "só conjuntos da Masmorra");
					Assert.True(rune.Grade >= rules.MinGrade && rune.Grade <= rules.MaxGrade, $"andar {floor}: {rune.Grade}★");
					Assert.True(rune.Rarity >= rules.MinRarity, $"andar {floor}: raridade {rune.Rarity}");
				}
			}
		}

		[Test]
		private static void ForgeDropsTools()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			var forge = database.Dungeons.First(d => d.Kind == DungeonKind.Tools);
			var last = forge.Floors.Count;

			var reward = Dungeons.ApplyVictory(new Random(2), player, forge, last);
			Assert.Equal(forge.Floor(last).ToolCount, reward.Tools.Count, "quantas pedras");
			Assert.True(reward.Tools.All(t => (int)t.Grade == forge.Floor(last).ToolGrade), "grau do andar");
			Assert.Equal(reward.Tools.Count, player.Tools.Count, "guardadas");
			Assert.Equal(null, reward.Rune, "a Forja não solta runa");
		}

		[Test]
		private static void DungeonExperienceGoesToItsOwnTeam()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			var golemMonster = Roster.Add(player, "imp_fire");
			Teams.Toggle(player, "golem", golemMonster.Id);

			Dungeons.ApplyVictory(new Random(1), player, database.Dungeon("golem"), 1);
			Assert.True(golemMonster.Level > 1, "a equipe do Golem ganha experiência");
			Assert.Equal(1, player.Monsters[0].Level, "a da Campanha não");
		}
	}
}
