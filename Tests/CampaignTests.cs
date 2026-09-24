using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Player;

namespace Sigilos.Tests
{
	internal static class CampaignTests
	{
		private static PlayerState NewPlayer() => NewGame.Create(DateTime.UnixEpoch, new Random(1));

		[Test]
		private static void SameSeedSameBattle()
		{
			var database = TestData.LoadReal();
			var player = NewPlayer();
			var stage = database.Stage(5);

			var a = BattleFactory.Create(database, PlayerTeam.Build(player, database), stage, seed: 99);
			var b = BattleFactory.Create(database, PlayerTeam.Build(player, database), stage, seed: 99);
			AutoBattle.Run(a);
			AutoBattle.Run(b);

			Assert.Equal(a.Victory, b.Victory, "resultado");
			Assert.Near(a.Time, b.Time, "duração");
			Assert.Near(a.Allies.Sum(u => u.Health), b.Allies.Sum(u => u.Health), "Vida que sobrou");
		}

		[Test]
		private static void EveryStageEnds()
		{
			var database = TestData.LoadReal();
			var player = NewPlayer();
			foreach (var stage in database.Stages)
			{
				var session = BattleFactory.Create(database, PlayerTeam.Build(player, database), stage, seed: stage.Number);
				AutoBattle.Run(session);
				Assert.True(session.IsOver, $"fase {stage.Number} não acabou");
			}
		}

		[Test]
		private static void StarterTeamWinsTheFirstStage()
		{
			var database = TestData.LoadReal();
			var player = NewPlayer();
			var session = BattleFactory.Create(database, PlayerTeam.Build(player, database), database.Stage(1), seed: 1);
			Assert.True(AutoBattle.Run(session), "o time inicial precisa vencer a fase 1 no nível 1");
		}

		[Test]
		private static void EquippedRunesReachTheBattle()
		{
			var database = TestData.LoadReal();
			var player = NewPlayer();
			var id = player.Team[0];
			var bare = BattleFactory.Create(database, PlayerTeam.Build(player, database), database.Stage(1), 1).Allies[0].Stats;

			foreach (var rune in player.Runes)
				RuneInventory.Equip(player, rune, id);
			var runed = BattleFactory.Create(database, PlayerTeam.Build(player, database), database.Stage(1), 1).Allies[0].Stats;

			Assert.True(runed.Health + runed.Attack + runed.Defense + runed.Speed > bare.Health + bare.Attack + bare.Defense + bare.Speed, "as runas somam atributos na luta");
		}
	}
}
