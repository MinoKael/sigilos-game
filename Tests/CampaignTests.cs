using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

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
		private static void CampaignNeverDropsToolsNorBigRunes()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var random = new Random(3);
			foreach (var stage in database.Stages)
			{
				var reward = Campaign.ApplyVictory(random, player, stage, database);
				Assert.Equal(0, reward.Tools.Count, $"fase {stage.Number} sem pedras");
				Assert.True(reward.Rune == null || reward.Rune.Grade <= 4, $"fase {stage.Number}: runa até 4★");
			}
		}

		[Test]
		private static void MonsterDropKnowsTheFirstCopy()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var random = new Random(5);
			var stage = database.Stage(1);
			var seen = new System.Collections.Generic.HashSet<string>(player.Monsters.Select(m => m.SummonId));
			var drops = 0;
			for (var i = 0; i < 400 && drops < 12; i++)
			{
				player.Mana = stage.Mana;
				if (Campaign.ApplyVictory(random, player, stage, database).SummonResult is not { } drop)
					continue;
				drops++;
				Assert.Equal(seen.Add(drop.Summon.Id), drop.FirstCopy, $"queda {drops}: primeira cópia de {drop.Summon.Id}");
			}

			Assert.True(drops >= 12, "o monstro cai de vez em quando");
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

		/// <summary>
		/// A calibragem (GDD, seção 10): a fase 50 se vence com nível 20 e runas, ou com 6★ nível 40 sem
		/// runas, mas não com nível 20 sem runas; e a primeira fase é mansa para quem acabou de começar.
		/// </summary>
		[Test]
		private static void CampaignEndsAtLevelTwentyWithRunesOrFortyWithout()
		{
			var database = TestData.LoadReal();
			var last = database.Stages.Last();
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtStage(database, last.Number), last.Encounter, 20) >= 0.7, "nível 20 com runas vence a fase 50");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.Bare(database), last.Encounter, 20) >= 0.6, "6★ nível 40 sem runas também");
			var noRunes = new BattleTeam(ReferenceTeams.AtStage(database, last.Number).Members.Select(m => m with { Runes = Array.Empty<Core.Runes.Rune>() }).ToList());
			Assert.True(ReferenceTeams.WinRate(database, noRunes, last.Encounter, 20) < 0.3, "nível 20 sem runas não: as runas contam");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtStage(database, 1), database.Stage(1).Encounter, 20) >= 0.9, "a fase 1 é mansa");
		}

		/// <summary>
		/// As Masmorras vêm depois da Campanha e em ordem: o andar 5 da Golem já pede mais que o fim da
		/// Campanha, e o andar 5 da Forja só cai com o degrau mais alto (6★, runas 6★ +15, Despertar).
		/// </summary>
		[Test]
		private static void DungeonFloorsAskForTheirTier()
		{
			var database = TestData.LoadReal();
			Assert.Equal("golem wyvern crypt sanctum forge", string.Join(" ", database.Dungeons.Select(d => d.Id)), "a ordem de dificuldade");
			var golem = database.Dungeons.Single(d => d.Id == "golem").Floor(5).Encounter;
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtStage(database, 50), golem, 20) < 0.5, "o fim da Campanha ainda não vence a Golem 5");
			var forge = database.Dungeons.Single(d => d.Id == "forge").Floor(5).Encounter;
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.Tier(database, 9), forge, 20) < 0.5, "sem Despertar nem habilidades, a Forja 5 não cai");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.Tier(database, 10), forge, 20) >= 0.6, "com tudo, cai");
		}
	}
}
