using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Player;

namespace Sigilos.Tests
{
	internal static class CampaignTests
	{
		[Test]
		private static void SameSeedSameBattle()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch);
			var stage = database.Stage(5);

			var a = BattleFactory.Create(database, PlayerTeam.Build(player, database), stage, seed: 99);
			var b = BattleFactory.Create(database, PlayerTeam.Build(player, database), stage, seed: 99);
			AutoBattle.Run(a, Posture.Balanced);
			AutoBattle.Run(b, Posture.Balanced);

			Assert.Equal(a.Victory, b.Victory, "resultado");
			Assert.Near(a.Time, b.Time, "duração");
			Assert.Near(a.Allies.Sum(u => u.Health), b.Allies.Sum(u => u.Health), "Vida que sobrou");
		}

		[Test]
		private static void EveryStageEnds()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch);
			foreach (var stage in database.Stages)
			{
				var session = BattleFactory.Create(database, PlayerTeam.Build(player, database), stage, seed: stage.Number);
				AutoBattle.Run(session, Posture.Balanced);
				Assert.True(session.IsOver, $"fase {stage.Number} não acabou");
			}
		}

		[Test]
		private static void StarterTeamWinsTheFirstStage()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch);
			var session = BattleFactory.Create(database, PlayerTeam.Build(player, database), database.Stage(1), seed: 1);
			Assert.True(AutoBattle.Run(session, Posture.Balanced), "o time inicial precisa vencer a fase 1 no nível 1");
		}
	}
}
