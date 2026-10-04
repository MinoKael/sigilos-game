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
		/// A calibragem (GDD, seção 10): a fase 50 se vence com nível 20 e runas, e não sem runas, nem no
		/// nível 20 nem no 6★ nível 40; sem runas, o 6★ nível 40 chega ao fim da região 2 (fase 40). E a
		/// primeira fase é mansa para quem acabou de começar.
		/// </summary>
		[Test]
		private static void CampaignEndsAtLevelTwentyWithRunes()
		{
			var database = TestData.LoadReal();
			var last = database.Stages.Last();
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtStage(database, last.Number), last.Encounter, 20) >= 0.7, "nível 20 com runas vence a fase 50");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.Bare(database), last.Encounter, 20) < 0.3, "6★ nível 40 sem runas não: a fase 50 pede runas");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.Bare(database), database.Stage(40).Encounter, 20) >= 0.6, "6★ nível 40 sem runas vence o fim da região 2");
			var noRunes = new BattleTeam(ReferenceTeams.AtStage(database, last.Number).Members.Select(m => m with { Runes = Array.Empty<Core.Runes.Rune>() }).ToList());
			Assert.True(ReferenceTeams.WinRate(database, noRunes, last.Encounter, 20) < 0.3, "nível 20 sem runas não: as runas contam");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtStage(database, 1), database.Stage(1).Encounter, 20) >= 0.9, "a fase 1 é mansa");
		}

		/// <summary>
		/// A dificuldade do andar acompanha o drop: o time que já usa runas como as do andar vence, e o do
		/// andar de baixo ainda não (ReferenceTeams.AtFloor). Elas abrem durante a Campanha, a Golem
		/// primeiro. As de especialização têm o teste delas (<see cref="SpecializedDungeonsAskForTheTeamBuiltForThem"/>).
		/// </summary>
		[Test]
		private static void DungeonFloorsAskForTheTeamTheirDropFits()
		{
			var database = TestData.LoadReal();
			Assert.Equal("golem wyvern crypt sanctum forge", string.Join(" ", database.Dungeons.OrderBy(d => d.UnlockStage).Select(d => d.Id)), "a ordem em que abrem");
			foreach (var dungeon in database.Dungeons.Where(d => !ReferenceTeams.Specialists.ContainsKey(d.Id)))
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtFloor(database, floor), encounter, 10) >= 0.6, $"{dungeon.Id} {floor}: o time do andar vence");
					Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtFloor(database, floor - 1), encounter, 10) < 0.5, $"{dungeon.Id} {floor}: o de baixo ainda não");
				}
			}
		}

		/// <summary>
		/// As Masmorras de especialização (GDD, seção 11): cada andar se vence com a equipe montada para o
		/// chefe, no degrau do andar (ReferenceTeams.Specialist), e não com o time típico do degrau de baixo;
		/// do andar 3 em diante, nem com a equipe certa ainda pouco investida. O ponto doce (5★, habilidades
		/// no máximo, runas 5★ +12) domina o andar 4 e não o 5. O andar 5 pede a preparação de cerca de 20
		/// dias (6★ desperta, runas 6★), e o time forte genérico com o mesmo investimento não passa. Cada
		/// equipe preparada falha no andar 5 de outra Masmorra: o time certo para uma não serve para todas.
		/// </summary>
		[Test]
		private static void SpecializedDungeonsAskForTheTeamBuiltForThem()
		{
			const int fights = 10;
			var database = TestData.LoadReal();
			var powerful = ReferenceTeams.Powerful(database);
			foreach (var id in ReferenceTeams.Specialists.Keys)
			{
				var dungeon = database.Dungeon(id);
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.Specialist(database, id, floor), encounter, fights) >= 0.7, $"{id} {floor}: a equipe montada para ele vence");
					Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtFloor(database, floor - 1), encounter, fights) < 0.5, $"{id} {floor}: o time típico de baixo não");
					if (floor >= 3)
						Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.Specialist(database, id, floor - 1), encounter, fights) < 0.5, $"{id} {floor}: a equipe certa, ainda sem o investimento, não");
				}

				var last = dungeon.Floor(dungeon.Floors.Count).Encounter;
				Assert.True(ReferenceTeams.WinRate(database, powerful, last, fights) < 0.3, $"{id} 5: o time forte genérico não passa");
				var prepared = ReferenceTeams.Specialist(database, id, dungeon.Floors.Count);
				Assert.True(ReferenceTeams.Specialists.Keys.Where(other => other != id).Any(other =>
					ReferenceTeams.WinRate(database, prepared, database.Dungeon(other).Floor(5).Encounter, fights) < 0.3), $"{id}: a equipe preparada falha em outra Masmorra");
			}
		}
	}
}
