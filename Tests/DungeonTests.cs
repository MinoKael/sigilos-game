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

		/// <summary>
		/// Todo andar de toda Masmorra solta pela tabela dele, e só o que ela tem: a conferência do
		/// <c>--drops</c> (<see cref="DropReport"/>), com menos vitórias.
		/// </summary>
		[Test]
		private static void EveryFloorDropsByItsTable()
		{
			var database = TestData.LoadReal();
			var random = new Random(4);
			var problems = database.Dungeons
				.SelectMany(dungeon => Enumerable.Range(1, dungeon.Floors.Count).SelectMany(floor => DropReport.Inspect(dungeon, floor, 3000, random).Problems))
				.ToList();
			Assert.Empty(problems, "drop fora da tabela");
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

		/// <summary>O Pergaminho Místico cai com as chances do Núcleo de Infusão: nada nos andares 1 e 2; 1%, 2% e 3% nos andares 3, 4 e 5.</summary>
		[Test]
		private static void DeepFloorsSometimesDropAMysticScroll()
		{
			const int victories = 6000;
			var database = TestData.LoadReal();
			var chances = new double[] { 0, 0, 1, 2, 3 };
			Assert.True(database.Dungeons.All(d => d.Floors.Select(f => f.ScrollChance).SequenceEqual(chances)), "0, 0, 1%, 2% e 3% em toda Masmorra");
			Assert.True(database.Dungeons.All(d => d.Floors.All(f => f.ScrollChance == f.CoreChance)), "as mesmas chances do Núcleo de Infusão");

			var random = new Random(9);
			foreach (var dungeon in new[] { database.Dungeon("golem"), database.Dungeons.First(d => d.Kind == DungeonKind.Tools) })
			{
				foreach (var floor in new[] { 1, 5 })
				{
					var player = TestData.PlayerWith("phoenix_fire");
					player.DungeonFloors[dungeon.Id] = dungeon.Floors.Count;
					var before = player.Scrolls;
					var scrolls = 0;
					for (var i = 0; i < victories; i++)
					{
						player.Runes.Clear();
						var reward = Dungeons.ApplyVictory(random, player, dungeon, floor);
						Assert.True(reward.Scrolls is 0 or 1, "um Pergaminho, no máximo");
						scrolls += reward.Scrolls;
					}

					var chance = dungeon.Floor(floor).ScrollChance;
					Assert.Equal(before + scrolls, player.Scrolls, $"{dungeon.Id} {floor}: os Pergaminhos vão para a conta");
					Assert.True(Math.Abs(100.0 * scrolls / victories - chance) < 0.7, $"{dungeon.Id} {floor}: perto de {chance}% ({scrolls} em {victories})");
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
