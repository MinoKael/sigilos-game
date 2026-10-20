using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Rta
{
	/// <summary>O que uma partida rendeu a um dos lados.</summary>
	public sealed record RtaSeatResult(int Delta, int NewAster, int Ecos);

	/// <summary>Como a partida acabou. <see cref="Winner"/> nulo: empate. <see cref="Forfeit"/>: alguém desistiu.</summary>
	public sealed record RtaOutcome(RtaSeat? Winner, bool Forfeit, RtaSeatResult First, RtaSeatResult Second)
	{
		public RtaSeatResult Of(RtaSeat seat) => seat == RtaSeat.First ? First : Second;
	}

	/// <summary>
	/// Uma partida da Arena do Véu do começo ao fim: a montagem do time (<see cref="RtaDraft"/>), a luta
	/// (<see cref="RtaMatch"/>) e o resultado. Não conhece rede: quem usa entrega as mensagens dos jogadores em
	/// <see cref="Post"/> e recebe as respostas por <see cref="RtaPlayer.Send"/>. É a sala que manda: confere cada
	/// pedido, vigia os prazos (<see cref="RtaClock"/>) e decide pelo jogador que estourou ou saiu.
	/// </summary>
	public sealed class RtaRoom
	{
		private readonly GameDatabase _database;
		private readonly RtaPlayer[] _players;
		private readonly bool _ranked;
		private readonly int _seed;
		private readonly double _scale;
		private readonly Channel<(RtaSeat Seat, RtaMessage Message)> _inbox = Channel.CreateUnbounded<(RtaSeat, RtaMessage)>();
		private readonly RtaClock[] _draftClocks = { Draft(), Draft() };
		private readonly RtaClock[] _turnClocks = { Turn(), Turn() };
		private readonly DateTime?[] _since = new DateTime?[2];

		/// <param name="ranked">Vale Áster e Ecos do Véu; o treino contra o fantasma não.</param>
		/// <param name="timeScale">Multiplica todos os prazos: 1 no jogo, bem menos nos testes.</param>
		public RtaRoom(GameDatabase database, RtaPlayer first, RtaPlayer second, int seed, bool ranked, double timeScale = 1)
		{
			_database = database;
			_players = new[] { first, second };
			_seed = seed;
			_ranked = ranked;
			_scale = timeScale;
		}

		private static RtaClock Draft() => new(RtaRules.DraftSeconds, RtaRules.MinSeconds, false);

		private static RtaClock Turn() => new(RtaRules.TurnSeconds, RtaRules.MinSeconds, true);

		public RtaPlayer Player(RtaSeat seat) => _players[(int)seat];

		/// <summary>Uma mensagem de um jogador chegou. Pode vir de qualquer linha de execução.</summary>
		public void Post(RtaSeat seat, RtaMessage message) => _inbox.Writer.TryWrite((seat, message));

		private sealed class Forfeited(RtaSeat seat) : Exception
		{
			public RtaSeat Seat { get; } = seat;
		}

		public async Task<RtaOutcome> Run(CancellationToken cancel)
		{
			foreach (var seat in new[] { RtaSeat.First, RtaSeat.Second })
			{
				var me = Player(seat);
				var other = Player(RtaDraft.Other(seat));
				Send(seat, new RtaMessage { Type = "matched", Seat = seat, Opponent = other.Name, Aster = me.Aster, OpponentAster = other.Aster, Ranked = _ranked, Seed = _seed });
			}

			RtaSeat? winner;
			var forfeit = false;
			try
			{
				var draft = await Pick(cancel);
				winner = await Fight(draft, cancel);
			}
			catch (Forfeited f)
			{
				winner = RtaDraft.Other(f.Seat);
				forfeit = true;
			}

			var outcome = new RtaOutcome(winner, forfeit, Result(RtaSeat.First, winner), Result(RtaSeat.Second, winner));
			foreach (var seat in new[] { RtaSeat.First, RtaSeat.Second })
			{
				var result = outcome.Of(seat);
				Send(seat, new RtaMessage { Type = "end", Winner = winner?.ToString() ?? "Draw", Delta = result.Delta, NewAster = result.NewAster, Ecos = result.Ecos, Ranked = _ranked, Forfeit = forfeit });
			}

			return outcome;
		}

		private RtaSeatResult Result(RtaSeat seat, RtaSeat? winner)
		{
			var mine = Player(seat).Aster;
			if (!_ranked || winner == null)
				return new RtaSeatResult(0, mine, 0);

			var won = winner == seat;
			var delta = Veil.Delta(_database.Arena, mine, Player(RtaDraft.Other(seat)).Aster, won);
			return new RtaSeatResult(delta, Veil.Apply(mine, delta), won ? Veil.EcosFor(_database.Arena, mine) : 0);
		}

		private void Send(RtaSeat seat, RtaMessage message) => Player(seat).Send(message);

		private void Both(Func<RtaSeat, RtaMessage> make)
		{
			Send(RtaSeat.First, make(RtaSeat.First));
			Send(RtaSeat.Second, make(RtaSeat.Second));
		}

		private TimeSpan Span(int seconds) => TimeSpan.FromSeconds(seconds * _scale);

		/// <summary>Espera uma mensagem até <paramref name="deadline"/>; nula se o prazo chegou antes.</summary>
		private async Task<(RtaSeat Seat, RtaMessage Message)?> Read(DateTime deadline, CancellationToken cancel)
		{
			var wait = deadline - DateTime.UtcNow;
			if (wait <= TimeSpan.Zero)
				return _inbox.Reader.TryRead(out var ready) ? ready : null;

			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
			timeout.CancelAfter(wait);
			try
			{
				return await _inbox.Reader.ReadAsync(timeout.Token);
			}
			catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
			{
				return null;
			}
		}

		// Montagem do time ----------------------------------------------------------------------------

		private async Task<RtaDraft> Pick(CancellationToken cancel)
		{
			var draft = new RtaDraft(Ids(RtaSeat.First), Ids(RtaSeat.Second));
			Touch(draft);
			while (draft.Phase != RtaPhase.Done)
			{
				SendDraft(draft);

				var automatic = new[] { RtaSeat.First, RtaSeat.Second }.FirstOrDefault(seat => draft.Waiting(seat) && Player(seat).Automatic, (RtaSeat)(-1));
				if ((int)automatic >= 0)
				{
					AutoStep(draft, automatic);
					Touch(draft);
					continue;
				}

				var waiting = new[] { RtaSeat.First, RtaSeat.Second }.Where(draft.Waiting).ToList();
				var deadline = waiting.Min(seat => _since[(int)seat]!.Value + Span(_draftClocks[(int)seat].Seconds));
				if (await Read(deadline, cancel) is not { } read)
				{
					var now = DateTime.UtcNow;
					foreach (var seat in waiting.Where(seat => _since[(int)seat]!.Value + Span(_draftClocks[(int)seat].Seconds) <= now))
					{
						AutoStep(draft, seat);
						_draftClocks[(int)seat].TimedOut();
					}
				}
				else
				{
					if (read.Message.Type == "leave")
						throw new Forfeited(read.Seat);
					if (Apply(draft, read.Seat, read.Message))
					{
						_draftClocks[(int)read.Seat].Acted();
						_since[(int)read.Seat] = DateTime.UtcNow;
					}
				}

				Touch(draft);
			}

			SendDraft(draft);
			return draft;
		}

		private IReadOnlyList<string> Ids(RtaSeat seat) => Player(seat).Pool.Select(m => m.Summon.Id).ToList();

		/// <summary>O relógio de quem espera começa quando passa a esperar; quem não espera mais fica sem.</summary>
		private void Touch(RtaDraft draft)
		{
			foreach (var seat in new[] { RtaSeat.First, RtaSeat.Second })
				_since[(int)seat] = draft.Waiting(seat) ? _since[(int)seat] ?? DateTime.UtcNow : null;
		}

		private static bool Apply(RtaDraft draft, RtaSeat seat, RtaMessage message) => message.Type switch
		{
			"pick" => draft.Pick(seat, message.Summon ?? ""),
			"ban" => draft.Ban(seat, message.Summon ?? ""),
			"lead" => draft.Lead(seat, message.Summon ?? ""),
			_ => false,
		};

		private void AutoStep(RtaDraft draft, RtaSeat seat)
		{
			switch (draft.Phase)
			{
				case RtaPhase.Picking:
					draft.Pick(seat, draft.AutoPick(seat));
					break;
				case RtaPhase.Banning:
					draft.Ban(seat, draft.AutoBan(seat));
					break;
				case RtaPhase.Leading:
					draft.Lead(seat, draft.AutoLeader(seat));
					break;
			}

			_since[(int)seat] = DateTime.UtcNow;
		}

		private void SendDraft(RtaDraft draft) => Both(seat => new RtaMessage
		{
			Type = "draft",
			Phase = draft.Phase,
			Acting = draft.Acting,
			PicksLeft = draft.PicksLeft,
			PicksFirst = draft.Picks(RtaSeat.First).ToList(),
			PicksSecond = draft.Picks(RtaSeat.Second).ToList(),
			BannedFirst = draft.Banned(RtaSeat.First),
			BannedSecond = draft.Banned(RtaSeat.Second),
			Waiting = draft.Waiting(seat),
			Led = draft.Leader(seat) != null,
			Seconds = _draftClocks[(int)(draft.Waiting(seat) ? seat : draft.Acting ?? seat)].Seconds,
		});

		// Luta --------------------------------------------------------------------------------------

		private BattleTeam TeamOf(RtaDraft draft, RtaSeat seat) =>
			new(draft.Team(seat).Select(Player(seat).Member).ToList());

		private async Task<RtaSeat?> Fight(RtaDraft draft, CancellationToken cancel)
		{
			var first = TeamOf(draft, RtaSeat.First);
			var second = TeamOf(draft, RtaSeat.Second);
			var match = new RtaMatch(_database, first, second, _seed);
			match.Start();
			Both(_ => new RtaMessage
			{
				Type = "battle",
				Seed = _seed,
				TeamFirst = first.Members.Select(ArenaWire.ToWire).ToList(),
				TeamSecond = second.Members.Select(ArenaWire.ToWire).ToList(),
			});

			var events = new List<BattleEvent>();
			while (true)
			{
				events.Clear();
				if (match.Next(events) is not { } actor)
					break;

				var seat = RtaMatch.SeatOf(actor);
				var list = actor.Side == Side.Allies ? match.Session.Allies : match.Session.Enemies;
				var clock = _turnClocks[(int)seat];
				Both(_ => new RtaMessage
				{
					Type = "turn",
					Turn = match.Turns,
					Actor = seat,
					Side = actor.Side == Side.Allies ? 0 : 1,
					ActorIndex = list.ToList().IndexOf(actor),
					Seconds = clock.Seconds,
				});

				var (decision, auto) = await Decide(match, actor, seat, cancel);
				match.Act(decision);
				Both(_ => new RtaMessage
				{
					Type = "acted",
					Turn = match.Turns - 1,
					Actor = seat,
					Skill = decision.Skill,
					Side = decision.TargetSide,
					Index = decision.TargetIndex,
					Hash = match.Hash(),
					Auto = auto,
				});
			}

			return match.Winner;
		}

		private async Task<(RtaDecision Decision, bool Auto)> Decide(RtaMatch match, BattleUnit actor, RtaSeat seat, CancellationToken cancel)
		{
			if (Player(seat).Automatic)
				return (match.Automatic(actor), true);

			var clock = _turnClocks[(int)seat];
			var deadline = DateTime.UtcNow + Span(clock.Seconds);
			while (true)
			{
				if (await Read(deadline, cancel) is not { } read)
				{
					clock.TimedOut();
					return (match.Automatic(actor), true);
				}

				if (read.Message.Type == "leave")
					throw new Forfeited(read.Seat);

				if (read.Seat == seat && read.Message.Type == "act" && read.Message.Turn == match.Turns)
				{
					clock.Acted();
					return (read.Message.Decision, false);
				}
			}
		}
	}
}
