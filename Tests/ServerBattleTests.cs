using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>A luta decidida pelo servidor: o piloto automático, a repetição das decisões do jogador, o bilhete e o patch do estado.</summary>
	internal static class ServerBattleTests
	{
		/// <summary>Uma quarta-feira no meio da semana da Arena, em UTC.</summary>
		private static readonly DateTime Wednesday = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

		private static readonly Guid Owner = Guid.NewGuid();

		private static PlayerState Strong(GameDatabase database, int level = 40)
		{
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), database);
			player.Mana = 100;
			var summon = database.Summons.First(s => s.Rarity == 3);
			for (var i = 0; i < 3; i++)
			{
				var monster = Roster.Add(player, summon);
				monster.Stars = 6;
				monster.Level = level;
				Teams.Toggle(player, Teams.Campaign, monster.Id);
			}

			return player;
		}

		private static PlayerState Copy(PlayerState player) => PlayerSave.FromJson(PlayerSave.ToJson(player))!;

		/// <summary>O livro de bilhetes com um relógio que o teste anda.</summary>
		private sealed class Clock
		{
			public DateTime Now = Wednesday;

			public BattleTickets Tickets { get; }

			public Clock() => Tickets = new BattleTickets(() => Now);
		}

		private static BattleRequest Stage(int number, Guid? ticket = null, bool auto = false) =>
			new() { Kind = BattleKind.Stage, Number = number, Ticket = ticket, Auto = auto };

		[Test]
		private static void ReplayWithoutDecisionsIsTheAutoBattle()
		{
			var database = TestData.LoadReal();
			var player = Strong(database);
			var team = PlayerTeam.Build(player, database, Teams.Campaign);
			var encounter = database.Stage(3).Encounter;
			for (var seed = 1; seed <= 20; seed++)
			{
				var auto = AutoBattle.Run(BattleFactory.Create(database, team, encounter, seed));
				var replay = BattleReplay.Run(BattleFactory.Create(database, team, encounter, seed), null, false);
				Assert.True(auto == replay, $"semente {seed}: sem decisões, a repetição decide como o automático");
			}
		}

		[Test]
		private static void PlayedBattleReplaysToTheSameResult()
		{
			var database = TestData.LoadReal();
			var team = PlayerTeam.Build(Strong(database), database, Teams.Campaign);
			foreach (var encounter in new[] { database.Stage(3).Encounter, database.Stage(12).Encounter, database.Dungeons.First().Floor(1).Encounter })
			{
				for (var seed = 1; seed <= 12; seed++)
				{
					var live = BattleFactory.Create(database, team, encounter, seed);
					var log = new List<LoggedAction>();
					live.Start();
					var turns = 0;
					while (!live.IsOver)
					{
						var turn = live.BeginTurn();
						if (!turn.NeedsDecision)
							continue;

						var actor = turn.Actor;
						UnitAction action;
						if (actor.Side == Side.Allies)
						{
							var ready = Enumerable.Range(0, actor.Skills.Count).Where(i => actor.IsReady(i) && (!actor.Skill(i).NeedsAlly || live.ChoosableAllies(actor).Count > 0)).ToList();
							var skill = ready[turns++ % ready.Count];
							var allies = live.ChoosableAllies(actor);
							var foes = live.ChoosableTargets(actor);
							var target = actor.Skill(skill).NeedsAlly ? allies[^1] : actor.Skill(skill).NeedsTarget && foes.Count > 0 ? foes[^1] : null;
							action = new UnitAction(skill, target);
							log.Add(BattleReplay.Log(live, action));
						}
						else
							action = AutoPilot.ForEnemy(live, actor);

						live.Act(action);
					}

					var again = BattleFactory.Create(database, team, encounter, seed);
					var won = BattleReplay.Run(again, log, false);
					var tag = $"semente {seed}";
					Assert.True(won == (live.Victory == true), $"{tag}: mesmo resultado");
					Assert.Equal(live.Round, again.Round, $"{tag}: mesma rodada");
					Assert.Equal(string.Join(",", live.Allies.Concat(live.Enemies).Select(u => (int)u.Health)), string.Join(",", again.Allies.Concat(again.Enemies).Select(u => (int)u.Health)), $"{tag}: mesma Vida de todos");
				}
			}
		}

		[Test]
		private static void ForgedDecisionsStillPlayALegalBattle()
		{
			var database = TestData.LoadReal();
			var team = PlayerTeam.Build(Strong(database), database, Teams.Campaign);
			var encounter = database.Stage(3).Encounter;
			var junk = new List<LoggedAction> { new(99, 500), new(-4, -9), new(1, 0), new(0, 3) };
			var session = BattleFactory.Create(database, team, encounter, 7);
			BattleReplay.Run(session, junk, false);
			Assert.True(session.IsOver, "a luta termina mesmo com decisões sem sentido");
		}

		[Test]
		private static void AutoBattlePaysOnlyAtTheEndAndThePatchRebuildsTheState()
		{
			var database = TestData.LoadReal();
			var clock = new Clock();
			var server = Strong(database);
			var device = Copy(server);

			var start = clock.Tickets.Start(Owner, new Random(5), database, server, Stage(1, auto: true))!;
			Assert.True(start.Ticket != null && start.Won && start.Duration > 0, "o começo luta no automático e dá o bilhete");
			Assert.Equal(0, start.Patch.Count, "o começo não muda a conta");
			Assert.True(start.Reward == null && server.HighestStage == 0 && server.Mana == 100, "nada pago no começo");

			var early = clock.Tickets.Finish(Owner, new Random(6), database, server, Stage(1, start.Ticket), out var missing)!;
			Assert.True(!missing && early.ReadyIn > 0, "antes do tempo da luta na tela, só diz quanto falta");
			Assert.True(early.Patch.Count == 0 && server.HighestStage == 0, "e nada muda");

			clock.Now = clock.Now.AddSeconds(start.Duration);
			var report = clock.Tickets.Finish(Owner, new Random(6), database, server, Stage(1, start.Ticket), out missing)!;
			Assert.True(!missing && report.ReadyIn == 0 && report.Won && report.Reward != null, "depois do tempo, paga");
			Assert.Equal(1, server.HighestStage, "o servidor registrou a fase");
			Assert.True(server.Mana < 100, "o servidor cobrou a Mana");

			StatePatch.Apply(device, report.Patch);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(device), "o patch leva o aparelho ao estado do servidor");

			Assert.True(clock.Tickets.Finish(Owner, new Random(6), database, server, Stage(1, start.Ticket), out missing) == null && missing, "o bilhete foi gasto: não paga de novo");
		}

		[Test]
		private static void TicketMustBeTheLastOneOfThisBattleAndStillValid()
		{
			var database = TestData.LoadReal();
			var clock = new Clock();
			var player = Strong(database);
			player.Mana = 1000;

			var first = clock.Tickets.Start(Owner, new Random(1), database, player, Stage(1, auto: true))!;
			clock.Now = clock.Now.AddMinutes(5);
			Assert.True(clock.Tickets.Finish(Owner, new Random(1), database, player, Stage(1), out var missing) == null && missing, "sem bilhete");
			Assert.True(clock.Tickets.Finish(Owner, new Random(1), database, player, Stage(2, first.Ticket), out missing) == null && missing, "bilhete de outra luta");
			Assert.True(clock.Tickets.Finish(Guid.NewGuid(), new Random(1), database, player, Stage(1, first.Ticket), out missing) == null && missing, "bilhete de outra conta");

			var second = clock.Tickets.Start(Owner, new Random(2), database, player, Stage(1, auto: true))!;
			clock.Now = clock.Now.AddMinutes(5);
			Assert.True(clock.Tickets.Finish(Owner, new Random(1), database, player, Stage(1, first.Ticket), out missing) == null && missing, "começar outra luta troca o bilhete");
			Assert.Equal(0, player.HighestStage, "nada pago");

			clock.Now = clock.Now.Add(BattleTickets.Life);
			Assert.True(clock.Tickets.Finish(Owner, new Random(1), database, player, Stage(1, second.Ticket), out missing) == null && missing, "bilhete vencido");
			Assert.Equal(0, player.HighestStage, "nada pago");
		}

		[Test]
		private static void ExperienceGoesToTheTeamOfTheStart()
		{
			var database = TestData.LoadReal();
			var clock = new Clock();
			var player = Strong(database, 30);
			var team = Teams.Of(player, Teams.Campaign).ToList();
			var bench = Roster.Add(player, database.Summons.First(s => s.Rarity == 3));
			bench.Stars = 6;
			bench.Level = 30;

			var start = clock.Tickets.Start(Owner, new Random(5), database, player, Stage(1, auto: true))!;
			Assert.True(start.Won, "a equipe vence a fase 1");
			Assert.True(Teams.Replace(player, Teams.Campaign, team[2], bench.Id), "troca alguém no meio da luta");

			clock.Now = clock.Now.AddSeconds(start.Duration);
			var report = clock.Tickets.Finish(Owner, new Random(6), database, player, Stage(1, start.Ticket), out _)!;
			Assert.True(report.Reward is { Experience: > 0 }, "a vitória rende experiência");
			Assert.True(player.Monster(team[2])!.Experience > 0 || player.Monster(team[2])!.Level > 30, "quem lutou ganha");
			Assert.True(bench.Experience == 0 && bench.Level == 30, "quem entrou depois não ganha");
		}

		[Test]
		private static void PlayedBattleNeedsTheSameTeamAtTheEnd()
		{
			var database = TestData.LoadReal();
			var clock = new Clock();
			var player = Strong(database);
			var team = Teams.Of(player, Teams.Campaign).ToList();
			var bench = Roster.Add(player, database.Summons.First(s => s.Rarity == 3));

			var start = clock.Tickets.Start(Owner, new Random(5), database, player, Stage(1))!;
			Assert.True(start.Ticket != null && start.Duration == 0, "a luta jogada só leva a semente e o bilhete");
			Teams.Replace(player, Teams.Campaign, team[0], bench.Id);
			clock.Now = clock.Now.AddHours(1);
			var settled = Stage(1, start.Ticket);
			settled.Actions = new List<LoggedAction>();
			Assert.True(clock.Tickets.Finish(Owner, new Random(6), database, player, settled, out var missing) == null && !missing, "outra equipe no fim é recusada");
			Assert.Equal(0, player.HighestStage, "nada pago");

			Teams.Replace(player, Teams.Campaign, bench.Id, team[0]);
			start = clock.Tickets.Start(Owner, new Random(5), database, player, Stage(1))!;
			clock.Now = clock.Now.AddHours(1);
			settled.Ticket = start.Ticket;
			var report = clock.Tickets.Finish(Owner, new Random(6), database, player, settled, out _)!;
			Assert.True(report.Won && report.Duration > 0 && report.ReadyIn == 0, "a mesma equipe repete a luta e vence");
			Assert.Equal(1, player.HighestStage, "pagou");
		}

		[Test]
		private static void ServerBattleRefusesClosedAndBrokeEntriesWithoutATicket()
		{
			var database = TestData.LoadReal();
			var clock = new Clock();
			var player = Strong(database);
			var locked = clock.Tickets.Start(Owner, new Random(1), database, player, Stage(30))!;
			Assert.True(locked.Problem == EntryProblem.Locked && locked.Ticket == null, "fase fechada, sem bilhete");
			player.Mana = 0;
			var broke = clock.Tickets.Start(Owner, new Random(1), database, player, Stage(1))!;
			Assert.True(broke.Problem == EntryProblem.NoMana && broke.Ticket == null, "sem Mana, sem bilhete");
			Assert.True(clock.Tickets.Start(Owner, new Random(1), database, player, Stage(9999)) == null, "fase que não existe");
			Assert.Equal(0, player.HighestStage, "nada mudou");
		}

		[Test]
		private static void ArenaStartSpendsTheInviteAndTheEndSettles()
		{
			var database = TestData.LoadReal();
			var clock = new Clock();
			var server = TestData.PlayerWith(TestData.TypicalTeam);
			var device = Copy(server);
			var invites = server.Arena.Invites;
			var key = Arena.ListKey(0);

			var start = clock.Tickets.Start(Owner, new Random(3), database, server, new BattleRequest { Kind = BattleKind.Arena, Key = key })!;
			Assert.True(start.Ticket != null && start.Patch.Count > 0, "o começo da Arena já muda a conta");
			Assert.Equal(invites - 1, server.Arena.Invites, "gastou o Convite");
			Assert.Equal(key, server.Arena.Pending, "a luta ficou pendente");
			StatePatch.Apply(device, start.Patch);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(device), "o patch do começo chega ao aparelho");

			var bonus = Arena.StreakBonus(server, database.Arena, start.Clock);
			var session = BattleFactory.CreateDuel(database, PlayerTeam.Build(server, database, Teams.Arena), Arena.Opponents(server, database)[0].Team, start.Seed, bonus.Health, bonus.Attack);
			var won = BattleReplay.Run(session, null, server.FocusBoss);

			clock.Now = clock.Now.AddHours(1);
			var finish = new BattleRequest { Kind = BattleKind.Arena, Key = key, Ticket = start.Ticket, Actions = new List<LoggedAction>() };
			var report = clock.Tickets.Finish(Owner, new Random(4), database, server, finish, out _)!;
			Assert.True(report.Outcome != null && report.Won == won, "o fim fecha a luta com o resultado que o aparelho viu");
			Assert.True(server.Arena.Pending == null, "a pendência saiu");
			StatePatch.Apply(device, report.Patch);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(device), "o patch do fim chega ao aparelho");
		}

		[Test]
		private static void ConstellationKeepsTheMonthOfTheStart()
		{
			var database = TestData.LoadReal();
			var exploration = database.Exploration;
			var clock = new Clock { Now = new DateTime(2026, 10, 31, 23, 59, 50, DateTimeKind.Utc) };
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), database);
			player.Mana = 100;
			var summon = database.Summons.MaxBy(s => s.Rarity)!;
			for (var i = 0; i < PlayerState.MapTeamSize; i++)
			{
				var monster = Roster.Add(player, summon);
				monster.Stars = 6;
				monster.Level = 40;
				Teams.Toggle(player, Teams.Exploration, monster.Id);
			}

			player.HighestStage = exploration.UnlockStage;
			var october = Exploration.VariationOf(clock.Now);

			var request = new BattleRequest { Kind = BattleKind.Constellation, Number = 1, Auto = true };
			var start = clock.Tickets.Start(Owner, new Random(5), database, player, request)!;
			Assert.True(start.Ticket != null && start.Won, "a equipe forte vence a constelação 1");
			Assert.Equal(clock.Now, start.Clock, "o começo diz a hora do servidor");

			clock.Now = clock.Now.AddMinutes(10);
			var november = Exploration.VariationOf(clock.Now);
			Assert.True(october != november, "o mês virou e a Exploração do mês também");
			request.Ticket = start.Ticket;
			var report = clock.Tickets.Finish(Owner, new Random(6), database, player, request, out _)!;
			Assert.True(report.Reward is { FirstClear: true }, "a constelação 1 está aberta no mês novo e paga");
			Assert.Equal("2026-11", player.ExplorationMonth, "o percurso é do mês do fim");
			var id = exploration.Constellation(1).Id;
			Assert.True(Records.Best(player, Records.ConstellationKey(id, october)) != null, "o recorde é o da luta que começou em outubro");
			Assert.True(Records.Best(player, Records.ConstellationKey(id, november)) == null, "e não o da Exploração de novembro");
		}

		[Test]
		private static void ServerFightCostIsReported()
		{
			var database = TestData.LoadReal();
			var clock = new Clock();
			var player = Strong(database);
			player.Mana = 100000;
			var watch = System.Diagnostics.Stopwatch.StartNew();
			var runs = 20;
			for (var i = 0; i < runs; i++)
			{
				var start = clock.Tickets.Start(Owner, new Random(i), database, player, Stage(1, auto: true))!;
				clock.Now = clock.Now.AddSeconds(start.Duration);
				clock.Tickets.Finish(Owner, new Random(i), database, player, Stage(1, start.Ticket), out _);
			}

			Console.WriteLine($"TEMPO servidor por luta (Campanha 1, equipe de 3, começo e fim, com diff do estado): {watch.ElapsedMilliseconds / (double)runs:F1} ms");
			Assert.True(watch.ElapsedMilliseconds / runs < 2000, "uma luta do servidor cabe em 2 s");
		}
	}
}
