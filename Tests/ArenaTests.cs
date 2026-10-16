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
		private static void OnlineDefensesTakeTheNearestSlots()
		{
			var (database, arena, player) = Setup();
			Arena.RefreshList(player, new Random(7));
			Assert.True(Arena.NeedsOnline(player), "lista nova pergunta ao servidor");
			Assert.False(Arena.Opponents(player, database).Any(o => o.Player), "sem defesas, tudo local");

			var summon = database.Summons.First().Id;
			var others = new[] { 400, 900, 1300 }.Select(points => new ArenaOnlineOpponent
			{
				Id = $"p{points}", Name = $"Jogador{points}", Points = points, Team = { new ArenaWireMember { Summon = summon, Stars = 6, Level = 40 } },
			}).ToList();
			Arena.SetOnline(player, arena, others);
			Assert.False(Arena.NeedsOnline(player), "perguntou uma vez por lista");

			var list = Arena.Opponents(player, database);
			Assert.Equal(arena.ListOffsets.Count, list.Count, "a lista continua do mesmo tamanho");
			Assert.Equal(3, list.Count(o => o.Player), "três vagas são de jogadores");
			Assert.True(list.Where(o => o.Player).OrderBy(o => o.Points).Select(o => o.Name).SequenceEqual(new[] { "Jogador400", "Jogador900", "Jogador1300" }), "cada um na vaga certa");
			var lowest = list.Where(o => o.Player).OrderBy(o => o.Index).First();
			Assert.Equal("Jogador400", lowest.Name, "o de menos pontos na vaga mais baixa");

			var again = Arena.Opponents(player, database);
			Assert.True(list.Select(o => o.Name).SequenceEqual(again.Select(o => o.Name)), "a lista não muda ao reabrir");

			Arena.RefreshList(player, new Random(8));
			Assert.False(Arena.Opponents(player, database).Any(o => o.Player), "lista renovada volta a ser local");
		}

		[Test]
		private static void EnoughPlayersReplaceEveryLocalOpponent()
		{
			var (database, arena, player) = Setup();
			var summon = database.Summons.First().Id;
			var others = Enumerable.Range(0, 15).Select(i => new ArenaOnlineOpponent
			{
				Id = $"p{i:00}", Name = $"Jogador{i}", Points = 800 + i * 30, Team = { new ArenaWireMember { Summon = summon, Stars = 6, Level = 40 } },
			});
			Arena.SetOnline(player, arena, others);
			Assert.True(Arena.Opponents(player, database).All(o => o.Player), "com jogadores de sobra, nenhum fantasma");
			Assert.Equal(arena.ListOffsets.Count, player.Arena.Online.Count, "só cabem as vagas da lista");
		}

		[Test]
		private static void ArenaTeamsHaveFourSlots()
		{
			Assert.Equal(4, Teams.SizeOf(Teams.Arena), "o time que ataca leva quatro");
			Assert.Equal(4, Teams.SizeOf(Teams.ArenaDefense), "a defesa também");
			Assert.Equal(5, Teams.SizeOf("golem"), "a Masmorra segue com cinco");
		}

		[Test]
		private static void InvitesRefillByTimeAndNeverGoBackwards()
		{
			var (_, arena, player) = Setup();
			player.Arena.Invites = 0;
			player.Arena.InviteClock = Monday;

			Arena.Renew(player, arena, Monday.AddMinutes(95), new Random(1));
			Assert.Equal(3, player.Arena.Invites, "95 min = 3 Convites (um a cada 30)");
			Assert.Equal(Monday.AddMinutes(90), player.Arena.InviteClock!.Value, "a sobra segue contando");

			Arena.Renew(player, arena, Monday.AddHours(48), new Random(1));
			Assert.Equal(arena.MaxInvites, player.Arena.Invites, "no teto");

			var before = player.Arena.Invites;
			Arena.Renew(player, arena, Monday, new Random(1));
			Assert.Equal(before, player.Arena.Invites, "relógio que volta não dá Convite");

			// Gastar, voltar o relógio um dia e avançar de novo não recarrega nada de graça.
			var later = Monday.AddHours(48);
			player.Arena.Invites = 2;
			Arena.Renew(player, arena, later.AddDays(-1), new Random(1));
			Arena.Renew(player, arena, later, new Random(1));
			Assert.Equal(2, player.Arena.Invites, "voltar e avançar o relógio não recarrega");
		}

		[Test]
		private static void WinsRaisePointsAndStackStreakBonus()
		{
			var (database, arena, player) = Setup();
			Assert.Equal(20, Arena.Gain(arena, 1000, 1000), "iguais: o valor-base");
			Assert.Equal(40, Arena.Gain(arena, 1000, 1500), "muito mais forte: o teto");
			Assert.Equal(8, Arena.Gain(arena, 1000, 500), "muito mais fraco: o piso");
			Assert.Equal(40, Arena.Loss(arena, 1000, 500), "perder para o mais fraco custa o teto");
			Assert.Equal(8, Arena.Loss(arena, 1000, 1500), "para o mais forte, o piso");

			var now = Monday;
			var points = player.Arena.Points;
			for (var i = 0; i < 10; i++)
			{
				var index = i % 3;
				player.Arena.Beaten.Remove(index);
				Assert.True(Arena.Begin(player, Arena.ListKey(index)), "gasta um Convite");
				player.Arena.Invites = 5;
				Arena.Finish(player, arena, database, ArenaResult.Win, now, new Random(2));
			}

			Assert.Equal(10, player.Arena.Streak, "dez vitórias seguidas");
			Assert.True(player.Arena.Points > points, "os pontos subiram");
			var (health, attack) = Arena.StreakBonus(player, arena, now);
			Assert.Near(0.20, health, "Vida +20%", 1e-9);
			Assert.Near(0.20, attack, "Ataque +20%", 1e-9);

			var (expiredHealth, expiredAttack) = Arena.StreakBonus(player, arena, now.AddMinutes(arena.StreakMinutes + 1));
			Assert.Equal(0.0, expiredHealth + expiredAttack, "uma hora depois da última vitória, sem bônus");
		}

		[Test]
		private static void LossAndDrawBreakTheStreak()
		{
			var (database, arena, player) = Setup();
			player.Arena.Beaten.Clear();
			Arena.Begin(player, Arena.ListKey(0));
			Arena.Finish(player, arena, database, ArenaResult.Win, Monday, new Random(1));
			Assert.Equal(1, player.Arena.Streak, "uma vitória");

			var points = player.Arena.Points;
			Arena.Begin(player, Arena.ListKey(1));
			var draw = Arena.Finish(player, arena, database, ArenaResult.Draw, Monday, new Random(1))!;
			Assert.Equal(0, player.Arena.Streak, "empate zera a sequência");
			Assert.Equal(points, player.Arena.Points, "e não mexe nos pontos");
			Assert.Equal(0, draw.PointsDelta, "o resultado diz 0");

			Arena.Begin(player, Arena.ListKey(1));
			var loss = Arena.Finish(player, arena, database, ArenaResult.Loss, Monday, new Random(1))!;
			Assert.True(loss.PointsDelta < 0 && player.Arena.Points < points, "derrota tira pontos");
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
		private static void ClearingTheListPaysInvitesAndRenewsIt()
		{
			var (database, arena, player) = Setup();
			var seed = player.Arena.ListSeed;
			var opponents = Arena.Opponents(player, database);
			Assert.Equal(10, opponents.Count, "dez adversários");
			Assert.True(opponents.All(o => o.Team.Members.Count == 4), "equipes de quatro");
			Assert.True(Arena.Opponents(player, database).Select(o => o.Name).SequenceEqual(opponents.Select(o => o.Name)), "a mesma lista ao reabrir");

			player.Arena.Invites = 10;
			for (var i = 0; i < 10; i++)
			{
				Arena.Begin(player, Arena.ListKey(i));
				Arena.Finish(player, arena, database, ArenaResult.Win, Monday, new Random(i + 1));
			}

			Assert.True(player.Arena.ListSeed != seed, "lista nova");
			Assert.Equal(0, player.Arena.Beaten.Count, "ninguém vencido na nova");
			Assert.Equal(10 - 10 + arena.ListBonusInvites, player.Arena.Invites, "gastou dez, ganhou os dois da lista");
			Assert.Equal(10, player.Arena.Streak, "a sequência atravessa a lista nova");
		}

		[Test]
		private static void RivalsPayGoldAndWaitWithoutPoints()
		{
			var (database, arena, player) = Setup();
			var rival = arena.Rivals[0];
			var points = player.Arena.Points;
			var gold = player.Gold;

			Assert.True(Arena.CanFightRival(player, rival, Monday), "pronto");
			Arena.Begin(player, Arena.RivalKey(rival.Id));
			var outcome = Arena.Finish(player, arena, database, ArenaResult.Win, Monday, new Random(1))!;
			Assert.Equal(points, player.Arena.Points, "sem pontos");
			Assert.Equal(gold + rival.Gold, player.Gold, "Ouro garantido");
			Assert.True(outcome.Astrarium > 0, "e Astrarium");
			Assert.False(Arena.CanFightRival(player, rival, Monday.AddHours(1)), "espera depois de vencido");
			Assert.True(Arena.CanFightRival(player, rival, Monday.AddHours(rival.CooldownHours + 1)), "e volta");
			Assert.True(ArenaTeams.Rival(database, rival).Members.Count == 4, "equipe de quatro");
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
