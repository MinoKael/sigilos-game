using System;
using System.Linq;
using System.Text.Json.Nodes;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>A Arena Astral: Convites, pontos, sequência, lista, Rivais, ciclo semanal, Loja Cósmica e o duelo.</summary>
	internal static class ArenaTests
	{
		private static readonly DateTime Monday = new(2026, 10, 12, 12, 0, 0);

		private static (GameDatabase Database, ArenaDefinition Arena, PlayerState Player) Setup()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			Arena.Renew(player, database.Arena, Monday, new Random(1));
			return (database, database.Arena, player);
		}

		[Test]
		private static void ArenaDataIsValidAndRisesByGrade()
		{
			var database = TestData.LoadReal();
			Assert.Empty(database.Validate().Where(p => p.StartsWith("Arena", StringComparison.Ordinal)), "dados da Arena");
			var arena = database.Arena;
			Assert.Equal(8, arena.Grades.Count, "Aprendiz, Adepto I a III, Alquimista I a III e Magister");
			Assert.Equal(ArenaState.StartPoints, arena.Grades.First(g => g.Id == "adept_3").Points, "toda conta começa no Adepto III");
			Assert.Equal("magister", arena.GradeOf(5000).Id, "muito acima: o maior Grau");
			Assert.Equal("apprentice", arena.GradeOf(0).Id, "sem pontos: o primeiro");
		}

		[Test]
		private static void OldSaveWithoutArenaStartsAtThousand()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("wolf_fire");
			var json = (JsonObject)JsonNode.Parse(PlayerSave.ToJson(player))!;
			var key = json.Select(pair => pair.Key).First(k => string.Equals(k, nameof(PlayerState.Arena), StringComparison.OrdinalIgnoreCase));
			json.Remove(key);

			var loaded = PlayerSave.FromJson(json.ToJsonString())!;
			Assert.Equal(1000, loaded.Arena.Points, "sem o campo, 1000 pontos e não 0");
			Assert.Equal(10, loaded.Arena.Invites, "e os Convites cheios");
			Assert.Equal(0, loaded.Arena.Astrarium, "e nenhum Astrarium");
			Assert.True(database.Arena.Grades.Count > 0, "dados carregam");
		}

		[Test]
		private static void DefenseSurvivesTheWire()
		{
			var (database, _, player) = Setup();
			var team = PlayerTeam.Build(player, database, Teams.Campaign);
			Assert.True(team.Members.Count > 0, "a equipe típica tem gente");

			var json = ArenaWire.ToJson(team);
			var back = ArenaWire.ToBattle(database, ArenaWire.ReadTeam(json)!)!;
			Assert.Equal(team.Members.Count, back.Members.Count, "mesmo tamanho");
			Assert.Equal(team.Members[0].Summon.Id, back.Members[0].Summon.Id, "mesma Líder");
			Assert.Equal(team.Members[0].Level, back.Members[0].Level, "mesmo nível");
			Assert.Equal(team.Members.Sum(m => m.Runes.Count), back.Members.Sum(m => m.Runes.Count), "as mesmas runas");
		}

		[Test]
		private static void WireDropsWhatTheGameDoesNotKnow()
		{
			var (database, _, _) = Setup();
			var known = database.Summons.First().Id;
			var members = new System.Collections.Generic.List<ArenaWireMember>
			{
				new() { Summon = "nao_existe", Stars = 6, Level = 40 },
				new() { Summon = known, Stars = 99, Level = 9999, Skills = new() { 50, -3 } },
			};
			var team = ArenaWire.ToBattle(database, members)!;
			Assert.Equal(1, team.Members.Count, "o desconhecido some");
			Assert.Equal(Growth.MaxStars, team.Members[0].Stars, "estrelas no teto");
			Assert.Equal(Growth.MaxLevel(Growth.MaxStars), team.Members[0].Level, "nível no teto");
			Assert.True(team.Members[0].SkillLevels.All(l => l >= 1), "habilidade nunca abaixo de 1");
			Assert.True(ArenaWire.ToBattle(database, new[] { new ArenaWireMember { Summon = "nao_existe" } }) == null, "ninguém válido: sem equipe");
		}

		[Test]
		private static void ClosingMidFightCountsAsLoss()
		{
			var (_, arena, player) = Setup();
			var points = player.Arena.Points;
			var invites = player.Arena.Invites;
			Assert.True(Arena.Begin(player, Arena.ListKey(2)), "começa");
			Assert.False(Arena.Begin(player, Arena.ListKey(3)), "não abre duas lutas");

			// O jogo fechou no meio: ao abrir de novo, a luta vale derrota e o Convite já foi.
			Arena.Renew(player, arena, Monday.AddMinutes(1), new Random(1));
			Assert.True(player.Arena.Points < points, "pontos perdidos");
			Assert.Equal(invites - 1, player.Arena.Invites, "o Convite não volta");
			Assert.True(player.Arena.Pending == null, "nada pendente");
		}

		[Test]
		private static void WeeklyCyclePaysOnceAndResets()
		{
			var (database, arena, player) = Setup();
			var cycle = player.Arena.Cycle;
			player.Arena.Points = 1150;
			player.Arena.CycleWins = 3;
			var gold = player.Gold;

			var nextWeek = Arena.CycleEnds(Monday).AddMinutes(1);
			var payout = Arena.Renew(player, arena, nextWeek, new Random(1));
			Assert.True(payout is { Gold: > 0 }, "paga o prêmio do Grau em que terminou");
			Assert.Equal(gold + payout!.Gold, player.Gold, "o Ouro entra");
			Assert.Equal(1000, player.Arena.Points, "pontos voltam para 1000");
			Assert.True(player.Arena.Cycle > cycle, "ciclo novo");

			Assert.True(Arena.Renew(player, arena, nextWeek.AddHours(1), new Random(1)) == null, "não paga de novo");
			Assert.True(Arena.Renew(player, arena, Monday, new Random(1)) == null && player.Arena.Cycle > cycle, "relógio para trás não repete nem desfaz");
			Assert.True(database.Arena.Grades.Count > 0, "dados");
		}

		[Test]
		private static void CosmicShopLimitsAndDelivers()
		{
			var (_, arena, player) = Setup();
			player.Arena.Astrarium = 10_000;
			var scroll = arena.Shop.First(o => o.Item == ArenaItem.Scrolls);
			var scrolls = player.Scrolls;
			for (var i = 0; i < scroll.Limit; i++)
				Assert.True(Arena.Buy(player, arena, scroll, Monday), $"compra {i + 1}");
			Assert.False(Arena.Buy(player, arena, scroll, Monday), "esgotou na semana");
			Assert.Equal(scrolls + scroll.Limit * scroll.Amount, player.Scrolls, "entregou");

			var mana = arena.Shop.First(o => o.Item == ArenaItem.Mana);
			for (var i = 0; i < mana.Limit; i++)
				Arena.Buy(player, arena, mana, Monday);
			Assert.False(Arena.Buy(player, arena, mana, Monday), "o frasco esgota no dia");
			Assert.True(Arena.Buy(player, arena, mana, Monday.AddDays(1)), "e volta no dia seguinte");

			// Esgota o frasco no dia seguinte e volta o relógio: o dia anterior não zera o limite.
			var tomorrow = Monday.AddDays(1);
			for (var i = 0; i < mana.Limit; i++)
				Arena.Buy(player, arena, mana, tomorrow);
			Assert.False(Arena.Buy(player, arena, mana, tomorrow), "esgotado amanhã");
			Assert.False(Arena.Buy(player, arena, mana, Monday), "o relógio no dia anterior não zera o limite do dia");

			var core = arena.Shop.First(o => o.Item == ArenaItem.InfusionCores);
			var cores = player.Monsters.Count(m => m.IsInfusionCore);
			Assert.True(Arena.Buy(player, arena, core, Monday), "Núcleo");
			Assert.Equal(cores + 1, player.Monsters.Count(m => m.IsInfusionCore), "entra na coleção");

			player.Arena.Astrarium = 0;
			Assert.False(Arena.Buy(player, arena, core, Monday), "sem Astrarium não compra");
		}

		[Test]
		private static void DuelRunsAndTheDefenderIsNotACampaignFoe()
		{
			var (database, arena, player) = Setup();
			var attacker = new BattleTeam(ReferenceTeams.AtCampaign(database, 30).Members);
			var opponent = Arena.Opponents(player, database)[4];
			var session = BattleFactory.CreateDuel(database, attacker, opponent.Team, 11, 0.2, 0.2);
			Assert.Equal(4, session.Enemies.Count, "quatro do outro lado");
			Assert.True(session.Allies.Count == 4 && session.Allies.All(a => a.Side == Side.Allies), "os meus");

			var plain = BattleFactory.CreateDuel(database, attacker, opponent.Team, 11);
			Assert.True(session.Allies[0].MaxHealth > plain.Allies[0].MaxHealth, "a sequência soma Vida");
			Assert.True(session.Enemies.All(e => e.Side == Side.Enemies), "a defesa luta do lado de lá");

			var log = new System.Collections.Generic.List<BattleEvent>();
			AutoBattle.Run(plain, log, false);
			Assert.True(log.Count > 0, "a luta anda até o fim");
			Assert.True(plain.IsOver, "e acaba");
		}

		[Test]
		private static void FeaturesNeverBreakOnArenaOrObservatory()
		{
			var database = TestData.LoadReal();
			for (var stage = 1; stage <= database.Stages.Count; stage++)
				Features.OpenedBy(database, stage);

			Assert.True(Features.OpenedBy(database, 20).Contains(Feature.Arena), "a fase 20 abre a Arena");
			Assert.False(Enumerable.Range(1, 100).Any(stage => Features.OpenedBy(database, stage).Contains(Feature.Observatory)), "o Observatório nunca abre por fase");

			var player = TestData.PlayerWith("wolf_fire");
			player.HighestStage = 50;
			Assert.False(Features.IsOpen(player, database, Feature.Observatory), "fechado para sempre nesta versão");
			Assert.False(Features.IsNew(player, database, Feature.Observatory), "e nunca pulsa");
			player.HighestStage = 19;
			Assert.False(Features.IsOpen(player, database, Feature.Arena), "a Arena fecha antes da fase 20");
			player.HighestStage = 20;
			Assert.True(Features.IsOpen(player, database, Feature.Arena), "e abre na 20");
		}
	}
}
