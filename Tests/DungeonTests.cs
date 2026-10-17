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
			var player = TestData.PlayerWith("wizard_fire");
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

	}
}
