using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Rta;

namespace Sigilos.Tests
{
	/// <summary>A sala da Arena do Véu inteira, sem rede: montagem, luta, prazos, desistência e a cópia da luta de cada jogador.</summary>
	internal static class RtaRoomTests
	{
		private static readonly ArenaTeamSpec Spec = new() { Stars = 6, Level = 30, RuneGrade = 6, RuneLevel = 6 };

		private static IReadOnlyList<TeamMember> Bench(int seed) => ArenaTeams.Bench(TestData.Database, Spec, new Random(seed), 14);

		/// <summary>Um jogador que responde como o jogo faria, e confere a luta com a cópia local.</summary>
		private sealed class Driver
		{
			private RtaRoom? _room;
			private RtaReplica? _replica;
			private readonly RtaSeat _seat;

			public Driver(RtaSeat seat) => _seat = seat;

			public bool Silent { get; init; }

			public bool LeaveAtStart { get; init; }

			public ConcurrentQueue<RtaMessage> Seen { get; } = new();

			public RtaReplica? Replica => _replica;

			public int Mismatches { get; private set; }

			public void Attach(RtaRoom room) => _room = room;

			public void Receive(RtaMessage message)
			{
				Seen.Enqueue(message);
				if (_room == null)
					return;

				switch (message.Type)
				{
					case "matched" when LeaveAtStart:
						_room.Post(_seat, new RtaMessage { Type = "leave" });
						break;
					case "draft" when message.Waiting && !Silent:
						Draft(message);
						break;
					case "battle":
						_replica = RtaReplica.From(TestData.Database, message);
						_replica!.Start();
						_replica.Advance();
						break;
					case "turn" when message.Actor == _seat && !Silent:
						_room.Post(_seat, new RtaMessage { Type = "act", Turn = message.Turn, Skill = 0, Side = 1 - message.Side, Index = 0 });
						break;
					case "acted" when _replica != null:
						_replica.Apply(message);
						if (!_replica.InSync)
							Mismatches++;
						_replica.Advance();
						break;
				}
			}

			private void Draft(RtaMessage draft)
			{
				var taken = (draft.PicksFirst ?? new()).Concat(draft.PicksSecond ?? new()).ToHashSet();
				var mine = (_seat == RtaSeat.First ? draft.PicksFirst : draft.PicksSecond) ?? new();
				var rival = (_seat == RtaSeat.First ? draft.PicksSecond : draft.PicksFirst) ?? new();
				switch (draft.Phase)
				{
					case RtaPhase.Picking:
						var pool = _room!.Player(_seat).Pool.Select(m => m.Summon.Id).First(id => !taken.Contains(id));
						_room.Post(_seat, new RtaMessage { Type = "pick", Summon = pool });
						break;
					case RtaPhase.Banning:
						_room!.Post(_seat, new RtaMessage { Type = "ban", Summon = rival[^1] });
						break;
					case RtaPhase.Leading when !draft.Led:
						var banned = _seat == RtaSeat.First ? draft.BannedFirst : draft.BannedSecond;
						_room!.Post(_seat, new RtaMessage { Type = "lead", Summon = mine.First(id => id != banned) });
						break;
				}
			}
		}

		private static (RtaRoom Room, Driver First, Driver Second) Make(int firstAster, int secondAster, bool ranked, double scale, Driver? first = null, Driver? second = null, bool firstGone = false, bool secondGone = false)
		{
			first ??= new Driver(RtaSeat.First);
			second ??= new Driver(RtaSeat.Second);
			var a = new RtaPlayer("Alfa", firstAster, Bench(1), first.Receive) { Gone = firstGone };
			var b = new RtaPlayer("Beta", secondAster, Bench(2), second.Receive) { Gone = secondGone };
			var room = new RtaRoom(TestData.Database, a, b, 77, ranked, scale);
			first.Attach(room);
			second.Attach(room);
			return (room, first, second);
		}

		private static RtaOutcome Run(RtaRoom room)
		{
			var run = room.Run(CancellationToken.None);
			try
			{
				Assert.True(run.Wait(TimeSpan.FromSeconds(60)), "a partida terminou a tempo");
			}
			catch (AggregateException e)
			{
				throw new Exception(e.InnerException?.ToString() ?? e.Message);
			}

			return run.Result;
		}

		[Test]
		private static void TwoPlayersDraftFightAndBothCopiesStayInSync()
		{
			var (room, first, second) = Make(1000, 1000, true, 1);
			var outcome = Run(room);

			Assert.True(outcome.Winner != null || !outcome.Forfeit, "a luta terminou");
			Assert.False(outcome.Forfeit, "ninguém desistiu");
			Assert.Equal(0, first.Mismatches, "a cópia do primeiro bate com o servidor");
			Assert.Equal(0, second.Mismatches, "a cópia do segundo bate com o servidor");

			var types = first.Seen.Select(m => m.Type).Distinct().ToList();
			foreach (var type in new[] { "matched", "draft", "battle", "turn", "acted", "end" })
				Assert.True(types.Contains(type), $"o primeiro recebeu '{type}'");

			var ends = new[] { first, second }.Select(d => d.Seen.Last(m => m.Type == "end")).ToList();
			Assert.Equal(ends[0].Winner, ends[1].Winner, "os dois ouvem o mesmo vencedor");
			if (outcome.Winner != null)
			{
				Assert.True(outcome.Of(outcome.Winner.Value).Delta > 0, "quem venceu ganha Áster");
				Assert.True(outcome.Of(RtaDraft.Other(outcome.Winner.Value)).Delta < 0, "quem perdeu perde Áster");
				Assert.True(outcome.Of(outcome.Winner.Value).Ecos > 0, "e leva Ecos do Véu");
			}

			var draft = first.Seen.Last(m => m.Type == "draft");
			Assert.Equal(RtaPhase.Done, draft.Phase!.Value, "a montagem terminou");
			Assert.Equal(5, draft.PicksFirst!.Count, "cinco do primeiro");
			Assert.False(draft.PicksFirst.Intersect(draft.PicksSecond!).Any(), "sem repetidas entre os lados");
		}

		[Test]
		private static void NobodyAnsweringIsPlayedByTheAutomaticOnTimeouts()
		{
			var (room, first, second) = Make(1000, 1000, true, 0.002, new Driver(RtaSeat.First) { Silent = true }, new Driver(RtaSeat.Second) { Silent = true });
			var outcome = Run(room);

			Assert.False(outcome.Forfeit, "estourar prazo não é desistir");
			Assert.Equal(0, first.Mismatches + second.Mismatches, "as cópias seguem iguais");
			var timed = first.Seen.Where(m => m.Type == "acted").ToList();
			Assert.True(timed.Count > 5 && timed.All(m => m.Auto), "todas as jogadas foram do automático");
			Assert.True(first.Seen.Any(m => m.Type == "draft" && m.Seconds is > 0 and < RtaRules.DraftSeconds), "o prazo da montagem caiu pela metade");
		}

		[Test]
		private static void LeavingForfeitsAndTheOtherWins()
		{
			var (room, _, second) = Make(1000, 1000, true, 1, new Driver(RtaSeat.First) { LeaveAtStart = true });
			var outcome = Run(room);

			Assert.True(outcome.Forfeit, "foi desistência");
			Assert.Equal(RtaSeat.Second, outcome.Winner!.Value, "o outro vence");
			Assert.True(outcome.First.Delta < 0 && outcome.Second.Delta > 0, "Áster muda");
			Assert.True(second.Seen.Last().Forfeit, "o vencedor é avisado");
		}

		[Test]
		private static void TrainingAgainstAGhostChangesNothing()
		{
			var human = new Driver(RtaSeat.First);
			var ghost = new Driver(RtaSeat.Second);
			var a = new RtaPlayer("Alfa", 1234, Bench(1), human.Receive);
			var b = new RtaPlayer("Fantasma", 1234, Bench(2), ghost.Receive, bot: true);
			var room = new RtaRoom(TestData.Database, a, b, 5, false, 1);
			human.Attach(room);
			ghost.Attach(room);
			var outcome = Run(room);

			Assert.Equal(0, outcome.First.Delta, "sem pontos");
			Assert.Equal(0, outcome.First.Ecos + outcome.Second.Ecos, "sem Ecos");
			Assert.Equal(1234, outcome.First.NewAster, "o Áster fica");
			Assert.Equal(0, human.Mismatches, "a cópia bate");
		}

		[Test]
		private static void ADisconnectedPlayerIsPlayedByTheAutomaticAtOnce()
		{
			var (room, first, _) = Make(1000, 1000, true, 1, firstGone: true);
			var watch = System.Diagnostics.Stopwatch.StartNew();
			var outcome = Run(room);

			Assert.False(outcome.Forfeit, "queda não é desistência");
			Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), "sem esperar prazo de quem saiu");
			Assert.True(first.Seen.Any(m => m.Type == "end"), "avisos seguem");
		}

		[Test]
		private static void PoolCheckDropsUnknownAndDuplicateSummonsAndNeedsTen()
		{
			var database = TestData.Database;
			var good = Veil.Pool(Rich(), database);
			Assert.True(RtaPlayer.Check(database, good) != null, "um baú de verdade vale");
			Assert.True(RtaPlayer.Check(database, good.Take(RtaRules.MinPool - 1).ToList()) == null, "menos de dez não vale");

			var bad = good.ToList();
			bad[0] = new ArenaWireMember { Summon = "não_existe", Stars = 6, Level = 1 };
			bad.Add(bad[1]);
			Assert.True(RtaPlayer.Check(database, bad)!.Count == good.Count - 1, "a que não existe sai e a repetida conta uma vez");
			Assert.True(RtaPlayer.Check(database, Enumerable.Repeat(good[0], RtaRules.MaxPool + 1).ToList()) == null, "acima do teto não vale");
		}

		private static Sigilos.Core.Player.PlayerState Rich()
		{
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			foreach (var summon in TestData.Database.Summons.Take(16))
				Sigilos.Core.Player.Roster.Add(player, summon);
			return player;
		}
	}
}
