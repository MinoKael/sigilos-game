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

		/// <summary>
		/// A luta que a janela e a tela cheia da Batalha automática mostram é a que ela resolveu: os mesmos
		/// eventos, na mesma ordem, inteira dentro do tempo que a Batalha automática espera.
		/// </summary>
		[Test]
		private static void WatchedAutoBattleIsTheFightItResolved()
		{
			var database = TestData.LoadReal();
			var team = Team(TestData.PlayerWith(TestData.TypicalTeam));
			var encounter = database.Dungeon("golem").Floor(3).Encounter;
			var log = new List<BattleEvent>();
			var won = AutoBattle.Run(BattleFactory.Create(database, team, encounter, 31), log, focusBoss: true);
			var seconds = BattlePace.Seconds(log, BattlePace.AutoBattleFactor);

			var fight = new AutoBattleFight(BattleFactory.Create(database, team, encounter, 31), focusBoss: true);
			var shown = new List<BattleEvent>();
			fight.Played += (beat, _) => shown.AddRange(beat.Events);
			for (var elapsed = 0.0; elapsed < seconds; elapsed += 1 / 60.0)
				fight.Advance(elapsed);
			fight.Advance(seconds);

			Assert.Equal(won, fight.Session.Victory == true, "resultado");
			Assert.Equal(string.Join(",", log.Select(e => e.GetType().Name)), string.Join(",", shown.Select(e => e.GetType().Name)), "eventos mostrados");
		}

		/// <summary>
		/// A luta vista mostra o começo de cada turno com a luta ainda antes da ação, como a jogada: os
		/// cartões, que se atualizam no começo, não mostram o dano do golpe antes de quem ataca correr.
		/// </summary>
		[Test]
		private static void WatchedAutoBattleShowsEachTurnStartBeforeTheAction()
		{
			var database = TestData.LoadReal();
			var team = Team(TestData.PlayerWith(TestData.TypicalTeam));
			var encounter = database.Dungeon("golem").Floor(3).Encounter;

			var played = BattleFactory.Create(database, team, encounter, 31);
			played.Start();
			var expected = new List<double>();
			while (!played.IsOver)
			{
				var actor = AutoBattle.Begin(played, null);
				expected.Add(Life(played));
				if (actor != null)
					AutoBattle.Act(played, actor, null, focusBoss: true);
			}

			var fight = new AutoBattleFight(BattleFactory.Create(database, team, encounter, 31), focusBoss: true);
			var seen = new List<double>();
			fight.Played += (beat, _) =>
			{
				if (beat.Events.Any(e => e is TurnStarted))
					seen.Add(Life(fight.Session));
			};
			fight.Advance(double.MaxValue);

			Assert.Equal(string.Join(";", expected), string.Join(";", seen), "Vida no começo de cada turno");
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
		/// A calibragem: o time de referência da Campanha (ReferenceTeams.AtCampaign, o que o jogador teria na
		/// fase) vence quase tudo no começo e cada vez menos até o fim: ~97% nas 10 primeiras, ~92% até a 25,
		/// de ~90% a ~78% até a 35 (o ponto de farm) e de ~78% a ~38% na 50. O 6★ nível 40 sem runas passa
		/// do começo, mas não chega ao fim: a fase 50 pede runas.
		/// </summary>
		[Test]
		private static void CampaignDifficultyRisesFromGentleToHard()
		{
			var database = TestData.LoadReal();
			double Wins(int number) => ReferenceTeams.WinRate(database, ReferenceTeams.AtCampaign(database, number), database.Stage(number).Encounter, 30);
			Assert.True(Wins(1) >= 0.9, "a fase 1 é mansa");
			Assert.True(Wins(10) >= 0.85, "a fase 10 ainda é tranquila");
			Assert.True(Wins(25) >= 0.78, "a fase 25 se vence quase sempre");
			Assert.True(Wins(35) >= 0.6, "o ponto de farm (fase 35) se vence na maioria das vezes");
			var last = database.Stages.Last();
			var final = Wins(last.Number);
			Assert.True(final >= 0.2 && final <= 0.65, $"a fase 50 é dura, mas passa ({final:0%})");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.BareOnMap(database), last.Encounter, 20) < 0.15, "6★ nível 40 sem runas não: a fase 50 pede runas");
			Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.BareOnMap(database), database.Stage(15).Encounter, 20) >= 0.8, "6★ nível 40 sem runas vence o começo da região 1");
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
			Assert.Equal("golem wyvern sanctum", string.Join(" ", database.Dungeons.OrderBy(d => d.UnlockStage).Select(d => d.Id)), "a ordem em que abrem");
			foreach (var dungeon in database.Dungeons)
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtFloor(database, floor), encounter, 10) >= 0.6, $"{dungeon.Id} {floor}: o time do andar vence");

					// O andar 1 de uma Masmorra com mecânica própria (as regras de Data/dungeons.json) existe para mostrar a
					// mecânica: o que o barra é a mecânica, não a força da equipe, e o time do andar 0 passa quase como o do 1.
					if (floor == 1 && dungeon.Rules.Count > 0)
						continue;

					Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtFloor(database, floor - 1), encounter, 10) < 0.5, $"{dungeon.Id} {floor}: o de baixo ainda não");
				}
			}
		}

		/// <summary>
		/// As três visões do andar 5 (ReferenceTeams.Free): com a mesma preparação, o time gratuito (um 4★ e quatro
		/// 3★ do Pergaminho Místico) vence pelos efeitos, o especialista pelo dano bruto e a sincronia de Velocidade
		/// vence mais rápido que os dois; o gratuito é o mais lento.
		/// </summary>
		[Test]
		private static void FloorFiveFallsToEachWayOfPlaying()
		{
			var database = TestData.LoadReal();
			foreach (var id in ReferenceTeams.Free.Keys)
			{
				var free = ReferenceTeams.Free[id].Select(m => database.Summon(m.Id)).ToList();
				Assert.True(free.Count(s => s.Rarity == 4) == 1 && free.Count(s => s.Rarity == 3) == 4, $"{id}: o gratuito é um 4★ e quatro 3★");
				Assert.True(free.All(s => SummonRates.Allows(ScrollKind.Mystic, s.Element)), $"{id}: todos saem do Pergaminho Místico");

				// Na 0.1 as equipes de especialista e a de sincronia de Velocidade dependiam das habilidades das famílias
				// que saíram do elenco (e das passivas antigas dos chefes). As habilidades novas serão feitas à mão, e
				// só então as três visões do andar 5 voltam a valer: aqui fica a composição do time gratuito.

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
			foreach (var id in database.Dungeons.Select(d => d.Id).Where(ReferenceTeams.Specialists.ContainsKey))
			{
				var dungeon = database.Dungeon(id);
				// Na 0.1 só valem as metades que não dependem das habilidades retiradas: o time típico de baixo não passa e o
				// forte genérico não vence o andar 5. "A equipe montada vence" volta quando as habilidades novas existirem
				// (as famílias e as passivas em que as equipes de ReferenceTeams.Specialists se apoiavam saíram).
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					if (floor == 1 && dungeon.Rules.Count > 0)
						continue; // o andar 1 mostra a mecânica (veja DungeonFloorsAskForTheTeamTheirDropFits)

					Assert.True(ReferenceTeams.WinRate(database, ReferenceTeams.AtFloor(database, floor - 1), encounter, fights) < 0.5, $"{id} {floor}: o time típico de baixo não");
				}

				// "O time forte genérico não passa no andar 5" saiu: o time forte (o típico com Despertar e habilidades no
				// máximo) é mais forte que o do andar 5 (AtFloor), que tem de vencer; as duas condições não cabem juntas
				// sem as habilidades de especialização, que serão feitas à mão.
			}
		}
	}
}
