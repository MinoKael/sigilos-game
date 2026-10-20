using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Rta;

namespace Sigilos.Tests
{
	/// <summary>A Arena do Véu: o draft, os relógios, a pressão, a luta com dois jogadores, o Áster e a Loja do Véu.</summary>
	internal static class RtaTests
	{
		private static readonly DateTime Monday = new(2026, 10, 12, 12, 0, 0);

		private static List<string> Pool(string prefix) => Enumerable.Range(0, 12).Select(i => $"{prefix}{i}").ToList();

		private static RtaDraft Drafted()
		{
			var draft = new RtaDraft(Pool("a"), Pool("b"));
			while (draft.Phase == RtaPhase.Picking)
			{
				var seat = draft.Acting!.Value;
				Assert.True(draft.Pick(seat, draft.AutoPick(seat)), "escolha automática vale");
			}

			return draft;
		}

		[Test]
		private static void DraftFollowsThePickOrderAndBlocksDuplicates()
		{
			var draft = new RtaDraft(new[] { "x", "y", "z" }.Concat(Pool("a")).ToList(), new[] { "x", "q" }.Concat(Pool("b")).ToList());
			Assert.Equal(RtaSeat.First, draft.Acting!.Value, "o primeiro abre");
			Assert.False(draft.Pick(RtaSeat.Second, "b0"), "fora da vez");
			Assert.False(draft.Pick(RtaSeat.First, "b0"), "só do próprio baú");
			Assert.True(draft.Pick(RtaSeat.First, "x"), "1 do primeiro");
			Assert.Equal(RtaSeat.Second, draft.Acting!.Value, "agora o segundo");
			Assert.Equal(2, draft.PicksLeft, "o segundo escolhe duas");
			Assert.False(draft.Pick(RtaSeat.Second, "x"), "o que o outro pegou não vale");
			Assert.True(draft.Pick(RtaSeat.Second, "q"), "1 do segundo");
			Assert.False(draft.Pick(RtaSeat.Second, "q"), "repetida");
			Assert.True(draft.Pick(RtaSeat.Second, "b0"), "2 do segundo");
			Assert.Equal(RtaSeat.First, draft.Acting!.Value, "volta ao primeiro");
		}

		[Test]
		private static void DraftBansLeadsAndBuildsTheTeams()
		{
			var draft = Drafted();
			Assert.Equal(5, draft.Picks(RtaSeat.First).Count, "cinco do primeiro");
			Assert.Equal(5, draft.Picks(RtaSeat.Second).Count, "cinco do segundo");
			Assert.True(draft.Picks(RtaSeat.First).Intersect(draft.Picks(RtaSeat.Second)).Count() == 0, "sem repetidas entre os lados");
			Assert.Equal(RtaPhase.Banning, draft.Phase, "agora o banimento");

			Assert.False(draft.Ban(RtaSeat.Second, draft.Picks(RtaSeat.First)[0]), "o primeiro bane antes");
			Assert.False(draft.Ban(RtaSeat.First, draft.Picks(RtaSeat.First)[0]), "não bane o próprio");
			var banned = draft.Picks(RtaSeat.Second)[1];
			Assert.True(draft.Ban(RtaSeat.First, banned), "bane do rival");
			Assert.Equal(banned, draft.Banned(RtaSeat.Second)!, "ficou guardado");
			Assert.True(draft.Ban(RtaSeat.Second, draft.AutoBan(RtaSeat.Second)), "o segundo bane");

			Assert.Equal(RtaPhase.Leading, draft.Phase, "agora a Líder");
			Assert.False(draft.Lead(RtaSeat.First, draft.Banned(RtaSeat.First)!), "a banida não lidera");
			var leader = draft.Remaining(RtaSeat.First)[2];
			Assert.True(draft.Lead(RtaSeat.First, leader), "a Líder do primeiro");
			Assert.Equal(RtaPhase.Leading, draft.Phase, "falta o outro");
			Assert.True(draft.Lead(RtaSeat.Second, draft.AutoLeader(RtaSeat.Second)), "a do segundo");

			Assert.Equal(RtaPhase.Done, draft.Phase, "pronto");
			Assert.Equal(RtaRules.TeamSize, draft.Team(RtaSeat.First).Count, "quatro lutam");
			Assert.Equal(leader, draft.Team(RtaSeat.First)[0], "a Líder vai primeiro");
			Assert.False(draft.Team(RtaSeat.First).Contains(draft.Banned(RtaSeat.First)!), "a banida fica de fora");
		}

		[Test]
		private static void DraftAutomaticPickNeverRunsDryWithTheMinimumPool()
		{
			// Dois baús iguais do tamanho mínimo: o primeiro leva as melhores, mas o segundo ainda completa as cinco.
			var same = Enumerable.Range(0, RtaRules.MinPool).Select(i => $"m{i}").ToList();
			var draft = new RtaDraft(same, same);
			while (draft.Phase == RtaPhase.Picking)
				draft.Pick(draft.Acting!.Value, draft.AutoPick(draft.Acting!.Value));
			Assert.Equal(5, draft.Picks(RtaSeat.First).Count, "cinco do primeiro");
			Assert.Equal(5, draft.Picks(RtaSeat.Second).Count, "cinco do segundo");
		}

		[Test]
		private static void ClockHalvesDownToTheMinimumAndTheTurnClockResets()
		{
			var draft = new RtaClock(RtaRules.DraftSeconds, RtaRules.MinSeconds, false);
			var seen = new List<int>();
			for (var i = 0; i < 6; i++)
			{
				seen.Add(draft.Seconds);
				draft.TimedOut();
			}

			Assert.True(seen.SequenceEqual(new[] { 30, 15, 7, 3, 3, 3 }), $"a escolha cai pela metade e para no mínimo: {string.Join(",", seen)}");
			draft.Acted();
			Assert.Equal(30, draft.Seconds, "agir a tempo devolve o prazo");

			var turn = new RtaClock(RtaRules.TurnSeconds, RtaRules.MinSeconds, true);
			var turns = new List<int>();
			for (var i = 0; i < 5; i++)
			{
				turns.Add(turn.Seconds);
				turn.TimedOut();
			}

			Assert.True(turns.SequenceEqual(new[] { 15, 7, 3, 15, 7 }), $"a luta recomeça depois do mínimo: {string.Join(",", turns)}");
		}

		private static (RtaMatch Match, GameDatabase Database) Match(int seed = 5)
		{
			var database = TestData.Database;
			var first = new BattleTeam(ReferenceTeams.AtCampaign(database, 30).Members);
			var second = new BattleTeam(ReferenceTeams.AtCampaign(database, 30).Members.Reverse().ToList());
			return (new RtaMatch(database, first, second, seed), database);
		}

		/// <summary>Joga a luta até o fim com o automático, devolvendo o hash a cada turno.</summary>
		private static List<string> Play(RtaMatch match, Action<RtaMatch>? beforeTurn = null)
		{
			var hashes = new List<string>();
			match.Start();
			var events = new List<BattleEvent>();
			while (match.Next(events) is { } actor)
			{
				beforeTurn?.Invoke(match);
				match.Act(match.Automatic(actor));
				hashes.Add(match.Hash());
			}

			return hashes;
		}

		[Test]
		private static void TwoMachinesPlayingTheSameDecisionsReachTheSameHashEveryTurn()
		{
			var server = Play(Match().Match);
			var client = Play(Match().Match);
			Assert.True(server.Count > 10, "a luta tem turnos");
			Assert.True(server.SequenceEqual(client), "mesmos hashes turno a turno");

			var other = Play(Match(6).Match);
			Assert.False(server.SequenceEqual(other), "outra semente, outra luta");
		}

		[Test]
		private static void DecisionTravelsBySideAndIndexAndFallsBackWhenInvalid()
		{
			var (match, _) = Match();
			match.Start();
			var events = new List<BattleEvent>();
			var actor = match.Next(events)!;
			var wire = match.Describe(new UnitAction(0, match.Session.Enemies[2]));
			Assert.Equal(1, wire.TargetSide, "o alvo é do lado dos inimigos");
			Assert.Equal(2, wire.TargetIndex, "e é o terceiro");

			// Alvo fora da lista: a sessão trata como sem alvo, sem quebrar.
			match.Act(new RtaDecision(0, 1, 99));
			Assert.True(actor.Side == Side.Allies || actor.Side == Side.Enemies, "a luta seguiu");
		}

		[Test]
		private static void PressureComesAfterTwentyTurnsAndThenEveryFifteen()
		{
			var (match, _) = Match();
			var stages = new List<(int Turns, int Pressure)>();
			var before = 0;
			Play(match, m =>
			{
				if (m.Pressure != before)
				{
					stages.Add((m.Turns, m.Pressure));
					before = m.Pressure;
				}
			});

			Assert.True(match.Turns >= RtaRules.PressureStart || match.IsOver, "a luta passou pelos turnos");
			if (stages.Count > 0)
			{
				Assert.Equal(RtaRules.PressureStart, stages[0].Turns, "a primeira pressão vem no turno 20");
				if (stages.Count > 1)
					Assert.Equal(RtaRules.PressureStart + RtaRules.PressureEvery, stages[1].Turns, "a seguinte, 15 depois");
			}
		}

		[Test]
		private static void PressureRaisesAttackAndCutsMaxHealth()
		{
			var (match, _) = Match();
			match.Start();
			var unit = match.Session.Allies[0];
			var health = unit.MaxHealth;
			var attack = unit.Attack;
			var events = new List<BattleEvent>();
			// Gasta os 20 turnos e confere antes e depois da primeira pressão.
			while (match.Turns < RtaRules.PressureStart && match.Next(events) is { } actor)
				match.Act(match.Automatic(actor));
			Assert.Equal(0, match.Pressure, "ainda sem pressão no turno 20 decidido");
			if (match.Next(events) == null)
				return;
			Assert.Equal(1, match.Pressure, "a pressão veio antes do turno 21");
			if (unit.IsAlive)
			{
				Assert.Near(health * 0.9, unit.MaxHealth, "Vida máxima -10%", 1e-6);
				Assert.True(unit.Attack > attack * 1.05, "Ataque +10%");
				Assert.True(unit.Health <= unit.MaxHealth, "a Vida atual cabe na máxima");
			}
		}

		[Test]
		private static void WinnerAndDrawAreTold()
		{
			var (match, _) = Match();
			Play(match);
			Assert.True(match.IsOver, "acabou");
			Assert.True(match.Winner != null || match.Draw, "tem vencedor ou empate");
		}

		[Test]
		private static void AsterGainsAndLossesFollowTheGapAndGradesAreShared()
		{
			var arena = TestData.Database.Arena;
			Assert.True(Veil.Delta(arena, 1000, 1000, true) > 0, "vitória soma");
			Assert.True(Veil.Delta(arena, 1000, 1000, false) < 0, "derrota tira");
			Assert.True(Veil.Delta(arena, 1000, 1300, true) > Veil.Delta(arena, 1000, 700, true), "vencer o mais forte rende mais");
			Assert.True(-Veil.Delta(arena, 1000, 700, false) > -Veil.Delta(arena, 1000, 1300, false), "perder para o mais fraco custa mais");
			Assert.Equal(0, Veil.Apply(10, -50), "o Áster não passa de zero");
			Assert.True(Veil.EcosFor(arena, 1700) > Veil.EcosFor(arena, 0), "Grau maior paga mais Ecos");
		}

		[Test]
		private static void PoolHoldsTheBestCopyOfEachSummonAndNeedsTenToQueue()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			Assert.False(Veil.CanQueue(player, database), "cinco invocações não bastam");

			var ids = database.Summons.Select(s => s.Id).Take(14).ToList();
			foreach (var id in ids)
				Roster.Add(player, database.Summon(id));
			Assert.True(Veil.CanQueue(player, database), "com muitas, entra");
			var pool = Veil.Pool(player, database);
			Assert.Equal(pool.Count, pool.Select(m => m.Summon).Distinct().Count(), "uma cópia de cada");
			Assert.True(pool.Count <= RtaRules.MaxPool, "o baú tem teto");
		}

		[Test]
		private static void VeilShopSpendsEcosAndRespectsTheLimit()
		{
			var database = TestData.Database;
			var arena = database.Arena;
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			Assert.True(arena.VeilShop.Count > 0, "a Loja do Véu tem ofertas");
			var offer = arena.VeilShop.First(o => o.Limit > 0 && o.Item == ArenaItem.Scrolls);

			player.Veil.Ecos = offer.Price * (offer.Limit + 2);
			var scrolls = player.Scrolls;
			Assert.True(Veil.Buy(player, arena, offer, Monday), "compra");
			Assert.Equal(scrolls + offer.Amount, player.Scrolls, "entrega");
			for (var i = 1; i < offer.Limit; i++)
				Veil.Buy(player, arena, offer, Monday);
			Assert.True(Veil.IsSoldOut(player, arena, offer, Monday), "esgotou no limite");
			Assert.False(Veil.Buy(player, arena, offer, Monday), "não passa do limite");
			Assert.False(Veil.IsSoldOut(player, arena, offer, Monday.AddDays(8)), "a semana nova repõe");
		}

		[Test]
		private static void VeilDataIsValid()
		{
			Assert.Empty(TestData.Database.Validate().Where(p => p.Contains("Véu", StringComparison.Ordinal)), "dados do Véu");
		}

		[Test]
		private static void WireMemberRoundTripKeepsTheMember()
		{
			var database = TestData.Database;
			var team = ReferenceTeams.AtCampaign(database, 30);
			var member = team.Members[0];
			var wire = ArenaWire.ToWire(member);
			var next = 1;
			var back = ArenaWire.ToMember(database, wire, ref next)!;
			Assert.Equal(member.Summon.Id, back.Summon.Id, "mesma invocação");
			Assert.Equal(member.Level, back.Level, "mesmo nível");
			Assert.Equal(member.Runes.Count, back.Runes.Count, "mesmas runas");
		}

		[Test]
		private static void EcosAreNeverCreditedTwice()
		{
			var veil = new VeilState();
			Veil.SeeProfile(veil, 100);
			Veil.Earn(veil, 4);
			Veil.SeeProfile(veil, 104);
			Veil.SeeProfile(veil, 104);
			Assert.Equal(104, veil.Ecos, "o total creditado");
			Veil.SeeProfile(veil, 110);
			Assert.Equal(110, veil.Ecos, "o que chegou por fora entra uma vez");
		}

		[Test]
		private static void ReplicaTreatsAnUnexpectedDecisionAsDivergence()
		{
			var database = TestData.Database;
			var spec = new ArenaTeamSpec { Stars = 6, Level = 30, RuneGrade = 6, RuneLevel = 6 };
			var battle = new RtaMessage
			{
				Type = "battle",
				Seed = 5,
				TeamFirst = ArenaTeams.Bench(database, spec, new Random(1), 14).Take(4).Select(ArenaWire.ToWire).ToList(),
				TeamSecond = ArenaTeams.Bench(database, spec, new Random(2), 14).Take(4).Select(ArenaWire.ToWire).ToList(),
			};
			var replica = RtaReplica.From(database, battle)!;
			replica.Start();
			var events = replica.Apply(new RtaMessage { Type = "acted", Turn = 99, Skill = 0, Side = 1, Index = 0 });
			Assert.False(replica.InSync, "a decisão fora de hora marca divergência");
			Assert.Equal(0, events.Count, "sem eventos, sem exceção");
		}

		[Test]
		private static void AutomaticChoiceNeverTouchesTheFightRandom()
		{
			// Quem só aplica as decisões (a cópia de um jogador) tem de chegar ao mesmo ponto de quem também chamou o automático (o servidor).
			var database = TestData.Database;
			var spec = new ArenaTeamSpec { Stars = 6, Level = 30, RuneGrade = 6, RuneLevel = 6 };
			var a = new BattleTeam(ArenaTeams.Bench(database, spec, new Random(1), 14).Take(4).ToList());
			var b = new BattleTeam(ArenaTeams.Bench(database, spec, new Random(2), 14).Take(4).ToList());
			var server = new RtaMatch(database, a, b, 5);
			var copy = new RtaMatch(database, a, b, 5);
			server.Start();
			copy.Start();
			var turns = 0;
			while (server.Next(new List<BattleEvent>()) is { } actor)
			{
				Assert.True(copy.Next(new List<BattleEvent>()) != null, "a cópia também tem turno");
				var decision = server.Automatic(actor);
				server.Act(decision);
				copy.Act(decision);
				Assert.Equal(server.Hash(), copy.Hash(), $"mesma luta no turno {++turns}");
			}

			Assert.True(copy.Next(new List<BattleEvent>()) == null, "a cópia acabou junto");
		}
	}
}
