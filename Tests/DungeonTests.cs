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

		/// <summary>As estrelas e a raridade saem na proporção da tabela do andar, e só o que ela tem.</summary>
		[Test]
		private static void RuneDungeonDropsByTheFloorChances()
		{
			const int drops = 4000;
			var database = TestData.LoadReal();
			var golem = database.Dungeon("golem");
			var random = new Random(4);

			for (var floor = 1; floor <= golem.Floors.Count; floor++)
			{
				var rules = golem.Floor(floor);
				var player = TestData.PlayerWith("phoenix_fire");
				var runes = Enumerable.Range(0, drops).Select(_ => Dungeons.ApplyVictory(random, player, golem, floor).Rune!).ToList();
				Assert.True(runes.All(r => golem.Sets.Contains(r.Set)), "só conjuntos da Masmorra");
				Assert.True(runes.All(r => rules.Grades.ContainsKey(r.Grade)), $"andar {floor}: só as estrelas da tabela");
				Assert.True(runes.All(r => rules.Rarities.ContainsKey(r.Rarity)), $"andar {floor}: só as raridades da tabela");
				foreach (var (grade, chance) in rules.Grades)
					Assert.True(Math.Abs(100.0 * runes.Count(r => r.Grade == grade) / drops - chance) < 3, $"andar {floor}: {grade}★ perto de {chance}%");
				foreach (var (rarity, chance) in rules.Rarities)
					Assert.True(Math.Abs(100.0 * runes.Count(r => r.Rarity == rarity) / drops - chance) < 3, $"andar {floor}: {rarity} perto de {chance}%");
			}
		}

		[Test]
		private static void EveryDungeonPaysTheSameOnTheSameFloor()
		{
			var database = TestData.LoadReal();
			var golem = database.Dungeon("golem");
			foreach (var dungeon in database.Dungeons)
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var rules = dungeon.Floor(floor);
					Assert.True(rules.Rarities.SequenceEqual(golem.Floor(floor).Rarities), $"{dungeon.Id} {floor}: raridades da tabela");
					if (dungeon.Kind == DungeonKind.Runes)
						Assert.True(rules.Grades.SequenceEqual(golem.Floor(floor).Grades), $"{dungeon.Id} {floor}: estrelas da tabela");
					Assert.Equal(golem.Floor(floor).Experience, rules.Experience, $"{dungeon.Id} {floor}: experiência");
				}
			}
		}

		[Test]
		private static void EveryFloorSometimesDropsAMysticScroll()
		{
			const int victories = 6000;
			var database = TestData.LoadReal();
			Assert.True(database.Dungeons.All(d => d.Floors.All(f => f.ScrollChance == 2)), "2% em todo andar de toda Masmorra");

			var random = new Random(9);
			foreach (var dungeon in new[] { database.Dungeon("golem"), database.Dungeons.First(d => d.Kind == DungeonKind.Tools) })
			{
				var player = TestData.PlayerWith("phoenix_fire");
				var before = player.Scrolls;
				var scrolls = 0;
				for (var i = 0; i < victories; i++)
				{
					player.Runes.Clear();
					var reward = Dungeons.ApplyVictory(random, player, dungeon, 1);
					Assert.True(reward.Scrolls is 0 or 1, "um Pergaminho, no máximo");
					scrolls += reward.Scrolls;
				}

				Assert.Equal(before + scrolls, player.Scrolls, $"{dungeon.Id}: os Pergaminhos vão para a conta");
				Assert.True(Math.Abs(100.0 * scrolls / victories - 2) < 0.6, $"{dungeon.Id}: perto de 2% ({scrolls} em {victories})");
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
			Assert.True(reward.Tools.All(t => forge.Floor(last).Rarities.ContainsKey(t.Grade)), "grau da tabela do andar");
			Assert.Equal(reward.Tools.Count, player.Tools.Count, "guardadas");
			Assert.Equal(null, reward.Rune, "a Forja não solta runa");
		}

		[Test]
		private static void DungeonExperienceGoesToItsOwnTeam()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			var golemMonster = Roster.Add(player, TestData.Summon("imp_fire"));
			Teams.Toggle(player, "golem", golemMonster.Id);

			Dungeons.ApplyVictory(new Random(1), player, database.Dungeon("golem"), 1);
			Assert.True(golemMonster.Level > 1, "a equipe do Golem ganha experiência");
			Assert.Equal(1, player.Monsters[0].Level, "a da Campanha não");
		}
	}
}
