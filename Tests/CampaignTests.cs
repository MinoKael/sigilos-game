using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Summoning;
using Sigilos.UI;

namespace Sigilos.Tests
{
	internal static class CampaignTests
	{
		private static BattleTeam Team(PlayerState player) => PlayerTeam.Build(player, TestData.LoadReal(), Teams.Campaign);

		[Test]
		private static void SameSeedSameBattle()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var stage = database.Stage(5).Encounter;

			var a = BattleFactory.Create(database, Team(player), stage, seed: 99);
			var b = BattleFactory.Create(database, Team(player), stage, seed: 99);
			AutoBattle.Run(a);
			AutoBattle.Run(b);

			Assert.Equal(a.Victory, b.Victory, "resultado");
			Assert.Near(a.Time, b.Time, "duração");
			Assert.Near(a.Allies.Sum(u => u.Health), b.Allies.Sum(u => u.Health), "Vida que sobrou");
		}

		private static double Life(BattleSession session) => session.Allies.Concat(session.Enemies).Sum(u => u.Health);

		[Test]
		private static void EveryStageAndFloorEnds()
		{
			var database = TestData.LoadReal();
			var team = Team(TestData.PlayerWith(TestData.TypicalTeam));
			var encounters = database.Stages.Select(s => s.Encounter)
				.Concat(database.Dungeons.SelectMany(d => d.Floors.Select(f => f.Encounter)));
			var seed = 0;
			foreach (var encounter in encounters)
			{
				var session = BattleFactory.Create(database, team, encounter, seed: ++seed);
				AutoBattle.Run(session);
				Assert.True(session.IsOver, $"luta {seed} não acabou");
			}
		}

		[Test]
		private static void TypicalTeamWinsTheFirstStage()
		{
			var database = TestData.LoadReal();
			var session = BattleFactory.Create(database, Team(TestData.PlayerWith(TestData.TypicalTeam)), database.Stage(1).Encounter, seed: 1);
			Assert.True(AutoBattle.Run(session), "o time de nível 1 precisa vencer a fase 1");
		}

		[Test]
		private static void EquippedRunesReachTheBattle()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var id = Teams.Of(player, Teams.Campaign)[0];
			var bare = BattleFactory.Create(database, Team(player), database.Stage(1).Encounter, 1).Allies[0].Stats;

			for (var i = 0; i < 6; i++)
				RuneInventory.Equip(player, RuneInventory.Create(new Random(i), player, 3), id);
			var runed = BattleFactory.Create(database, Team(player), database.Stage(1).Encounter, 1).Allies[0].Stats;

			Assert.True(runed.Health + runed.Attack + runed.Defense + runed.Speed > bare.Health + bare.Attack + bare.Defense + bare.Speed, "as runas somam atributos na luta");
		}

		[Test]
		private static void RegionsCoverEveryStage()
		{
			var database = TestData.LoadReal();
			var covered = Enumerable.Range(0, Campaign.RegionStarts.Count)
				.Select(region => Campaign.Region(region, database.Stages.Count))
				.SelectMany(r => Enumerable.Range(r.First, r.Last - r.First + 1))
				.ToList();
			Assert.Equal(database.Stages.Count, covered.Count, "cada fase numa região só");
			Assert.True(covered.SequenceEqual(database.Stages.Select(s => s.Number)), "as regiões seguem as fases em ordem");
			Assert.Equal(0, Campaign.RegionOf(20), "a fase 20 fecha a Planície dos Menires");
			Assert.Equal(1, Campaign.RegionOf(21), "e a 21 abre o Arquipélago Afogado");
			Assert.Equal(2, Campaign.RegionOf(database.Stages.Count), "a última fase é da Cidadela");
		}

	}
}
