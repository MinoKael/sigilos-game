using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.Core.Progression
{
	public enum BattleKind
	{
		Stage,
		Floor,
		Constellation,

		/// <summary>Arena Astral: <see cref="BattleRequest.Key"/> diz o adversário (<see cref="Arena.ListKey"/> ou <see cref="Arena.RivalKey"/>).</summary>
		Arena,
	}

	/// <summary>O pedido de uma luta: só qual conteúdo, nunca o resultado, a equipe, a semente nem a hora.</summary>
	public sealed class BattleRequest
	{
		public BattleKind Kind { get; set; }

		/// <summary>Campanha: o número da fase. Constelação: o número dela.</summary>
		public int Number { get; set; }

		/// <summary>Masmorra: o id.</summary>
		public string? Dungeon { get; set; }

		/// <summary>Masmorra: o andar.</summary>
		public int Floor { get; set; }

		/// <summary>Arena: o adversário.</summary>
		public string? Key { get; set; }

		/// <summary>
		/// Ao começar: a luta é da Batalha automática. O servidor já luta no automático e guarda o resultado no bilhete; o fim só
		/// paga, e só depois do tempo que a luta leva na tela (<see cref="BattleReport.Duration"/>).
		/// </summary>
		public bool Auto { get; set; }

		/// <summary>Ao terminar: o bilhete que o servidor deu ao começar (<see cref="BattleReport.Ticket"/>). Sem ele, nada paga.</summary>
		public Guid? Ticket { get; set; }

		/// <summary>Luta jogada, ao terminar: as decisões do jogador, na ordem.</summary>
		public List<LoggedAction>? Actions { get; set; }

		public string ToJson() => JsonSerializer.Serialize(this, GameDatabase.JsonOptions);

		public static BattleRequest? FromJson(string json)
		{
			try
			{
				return JsonSerializer.Deserialize<BattleRequest>(json, GameDatabase.JsonOptions);
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}

	/// <summary>A recompensa como dados simples (sem as definições do conteúdo), para viajar do servidor ao jogo.</summary>
	public sealed class BattleRewardData
	{
		public int Mana { get; set; }
		public int Scrolls { get; set; }
		public int Gold { get; set; }
		public int Essence { get; set; }
		public int Experience { get; set; }
		public bool FirstClear { get; set; }
		public Rune? Rune { get; set; }
		public OwnedSummon? Monster { get; set; }
		public bool FirstCopy { get; set; }
		public List<RuneTool> Tools { get; set; } = new();
		public List<int> LevelUps { get; set; } = new();
		public int AccountLevels { get; set; }
		public Prize? Prize { get; set; }
		public List<Rune> BonusRunes { get; set; } = new();

		public static BattleRewardData Of(VictoryReward reward) => new()
		{
			Mana = reward.Mana,
			Scrolls = reward.Scrolls,
			Gold = reward.Gold,
			Essence = reward.Essence,
			Experience = reward.Experience,
			FirstClear = reward.FirstClear,
			Rune = reward.Rune,
			Monster = reward.SummonResult?.Monster,
			FirstCopy = reward.SummonResult?.FirstCopy ?? false,
			Tools = reward.Tools.ToList(),
			LevelUps = reward.LevelUps.ToList(),
			AccountLevels = reward.AccountLevels,
			Prize = reward.Prize,
			BonusRunes = reward.BonusRunes?.ToList() ?? new(),
		};

		public VictoryReward ToReward(GameDatabase database) => new(
			Mana,
			Scrolls,
			Gold,
			Essence,
			Experience,
			FirstClear,
			Rune,
			Monster != null && database.HasSummon(Monster.SummonId) ? new SummonResult(database.Summon(Monster.SummonId), Monster, FirstCopy) : null,
			Tools,
			LevelUps,
			AccountLevels,
			Prize,
			BonusRunes);
	}

	/// <summary>
	/// O que o servidor responde a uma luta. Ao começar: a semente, o bilhete e a hora (e, na Batalha automática, o resultado e o
	/// tempo dela). Ao terminar: quem venceu, a recompensa e o que mudou na conta.
	/// </summary>
	public sealed class BattleReport
	{
		public bool Won { get; set; }

		/// <summary>Quanto a luta levaria na tela na velocidade 1× (<see cref="BattlePace"/>): o tempo que entra no recorde.</summary>
		public double Seconds { get; set; }

		/// <summary>Quanto a luta leva na tela na velocidade do automático: o fim não paga antes disso.</summary>
		public double Duration { get; set; }

		/// <summary>A vitória bateu o recorde da equipe.</summary>
		public bool NewBest { get; set; }

		public int Seed { get; set; }

		/// <summary>Ao começar: o bilhete que o fim da luta devolve.</summary>
		public Guid? Ticket { get; set; }

		/// <summary>A hora do servidor (UTC) quando a luta começou: o encontro da Exploração do mês vem dela.</summary>
		public DateTime Clock { get; set; }

		/// <summary>O fim chegou antes de a luta acabar na tela: faltam estes segundos, nada mudou e o bilhete continua valendo.</summary>
		public double ReadyIn { get; set; }

		public EntryProblem Problem { get; set; }

		public BattleRewardData? Reward { get; set; }

		/// <summary>Arena: o que a luta mudou (pontos, sequência, Astrarium), para o resultado mostrar.</summary>
		public ArenaOutcome? Outcome { get; set; }

		/// <summary>O que a luta mudou na conta; vazio, nada muda e o servidor não grava (a Arena já muda no começo: o Convite e a luta pendente).</summary>
		public JsonObject Patch { get; set; } = new();

		public string ToJson() => JsonSerializer.Serialize(this, GameDatabase.JsonOptions);

		public static BattleReport? FromJson(string json)
		{
			try
			{
				return JsonSerializer.Deserialize<BattleReport>(json, GameDatabase.JsonOptions);
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}

	/// <summary>
	/// O que o servidor guarda de uma luta começada, até o fim dela: qual luta, a semente, a hora, quem lutou e, na Batalha
	/// automática, o resultado já decidido. Não entra no save: fica no livro de bilhetes (<see cref="BattleTickets"/>).
	/// </summary>
	public sealed record BattleTicket
	{
		public Guid Id { get; init; }

		/// <summary>Qual luta (<see cref="ServerBattles.KeyOf"/>): o fim tem de pedir a mesma.</summary>
		public string Key { get; init; } = "";

		public int Seed { get; init; }

		/// <summary>A hora do servidor no começo: o encontro e o recorde da Exploração são os do mês em que a luta começou.</summary>
		public DateTime Clock { get; init; }

		/// <summary>Quando o livro deu o bilhete: o tempo da luta na tela conta daqui.</summary>
		public DateTime IssuedAt { get; init; }

		/// <summary>Quem começou a luta, na ordem: a experiência vai para eles, e a luta jogada exige a mesma equipe no fim.</summary>
		public IReadOnlyList<int> Team { get; init; } = Array.Empty<int>();

		/// <summary>Batalha automática: o resultado abaixo já foi decidido no começo.</summary>
		public bool Auto { get; init; }

		public bool Won { get; init; }

		public double Seconds { get; init; }

		public double Duration { get; init; }

		/// <summary>Quem entra no recorde se a luta bater o tempo, como estavam no começo.</summary>
		public IReadOnlyList<RecordMember> Members { get; init; } = Array.Empty<RecordMember>();
	}

	/// <summary>
	/// O livro dos bilhetes das lutas começadas: um por conta (começar outra luta troca o bilhete, então não dá para guardar lutas
	/// prontas e pular o tempo delas) e válido por <see cref="Life"/>. O servidor guarda um livro; o jogo sem servidor (save de
	/// teste) guarda o dele, com as mesmas regras. O relógio é o do livro, e os testes trocam por um de mentira.
	/// </summary>
	public sealed class BattleTickets
	{
		public static readonly TimeSpan Life = TimeSpan.FromHours(2);

		private readonly Func<DateTime> _clock;
		private readonly ConcurrentDictionary<Guid, BattleTicket> _pending = new();

		public BattleTickets(Func<DateTime>? clock = null) => _clock = clock ?? (() => DateTime.UtcNow);

		/// <summary>A hora do livro, em UTC: a de toda regra de tempo da luta.</summary>
		public DateTime Now => _clock();

		/// <summary>Começa a luta (<see cref="ServerBattles.Start"/>): o bilhete entra no lugar do anterior e vai no relatório.</summary>
		public BattleReport? Start(Guid owner, Random random, GameDatabase database, PlayerState player, BattleRequest request)
		{
			var now = Now;
			var report = ServerBattles.Start(random, database, player, request, now, out var ticket);
			if (report != null && ticket != null)
			{
				var issued = ticket with { Id = Guid.NewGuid(), IssuedAt = now };
				_pending[owner] = issued;
				report.Ticket = issued.Id;
			}

			return report;
		}

		/// <summary>
		/// Termina a luta do bilhete (<see cref="ServerBattles.Finish"/>). Antes da hora, o relatório traz quanto falta
		/// (<see cref="BattleReport.ReadyIn"/>) e o bilhete fica; depois, o bilhete é gasto, pague ou não. Nulo com
		/// <paramref name="missing"/> sem um bilhete desta luta; nulo sem ele, se a luta não confere com a que começou.
		/// </summary>
		public BattleReport? Finish(Guid owner, Random random, GameDatabase database, PlayerState player, BattleRequest request, out bool missing)
		{
			var now = Now;
			if (request.Ticket is not { } id
				|| !_pending.TryGetValue(owner, out var ticket)
				|| ticket.Id != id
				|| ticket.Key != ServerBattles.KeyOf(request)
				|| now - ticket.IssuedAt >= Life)
			{
				missing = true;
				return null;
			}

			missing = false;
			var report = ServerBattles.Finish(random, database, player, request, ticket, now);
			if (report is { ReadyIn: > 0 })
				return report;

			_pending.TryRemove(new KeyValuePair<Guid, BattleTicket>(owner, ticket));
			return report;
		}
	}

	/// <summary>
	/// A luta decidida pelo servidor, em dois pedidos. O começo confere a porta (aberta, Mana, vaga de runa) e a equipe pelo save
	/// que o servidor guarda e sorteia a semente; na Batalha automática, já luta e guarda o resultado. O fim confere o bilhete e o
	/// tempo da luta na tela, repete as decisões da luta jogada e, na vitória, paga com o próprio sorteio. O jogo só pede e mostra.
	/// </summary>
	public static class ServerBattles
	{
		/// <summary>A parte do tempo da luta na tela que o fim espera, no mínimo, e a folga em segundos: o relógio do aparelho e a rede.</summary>
		public const double ReadyShare = 0.9;

		public const double ReadySlack = 1;

		/// <summary>Qual luta o pedido é: o fim tem de ser da mesma que começou.</summary>
		public static string KeyOf(BattleRequest request) => $"{request.Kind}|{request.Number}|{request.Dungeon}|{request.Floor}|{request.Key}";

		/// <summary>
		/// Começa a luta. Nulo se o pedido não existe (fase, andar ou constelação inválidos, adversário que não está na lista) ou a
		/// equipe do conteúdo está vazia; com <see cref="BattleReport.Problem"/> se a porta está fechada (e sem bilhete). Não paga nada.
		/// </summary>
		public static BattleReport? Start(Random random, GameDatabase database, PlayerState player, BattleRequest request, DateTime now, out BattleTicket? ticket)
		{
			ticket = null;
			if (request.Kind == BattleKind.Arena)
				return StartArena(random, database, player, request, now, out ticket);

			if (Resolve(database, player, request, now, now) is not { } door)
				return null;

			if (door.Problem != EntryProblem.None)
				return new BattleReport { Problem = door.Problem, Clock = now };

			var team = PlayerTeam.Build(player, database, door.Content);
			if (team.Members.Count == 0)
				return null;

			var seed = random.Next();
			var report = new BattleReport { Seed = seed, Clock = now };
			ticket = new BattleTicket { Key = KeyOf(request), Seed = seed, Clock = now, Team = Teams.Of(player, door.Content).ToList() };
			if (!request.Auto)
				return report;

			var log = new List<BattleEvent>();
			report.Won = AutoBattle.Run(BattleFactory.Create(database, team, door.Encounter, seed), log, player.FocusBoss);
			report.Seconds = RecordSeconds(log);
			report.Duration = ScreenSeconds(log);
			ticket = ticket with { Auto = true, Won = report.Won, Seconds = report.Seconds, Duration = report.Duration, Members = MembersOf(database, player, ticket.Team) };
			return report;
		}

		/// <summary>
		/// Termina a luta do bilhete. A automática usa o resultado do começo; a jogada repete as decisões na semente do bilhete, com a
		/// mesma equipe. Antes do tempo da luta na tela, só diz quanto falta. Na vitória, a porta é conferida de novo (a hora agora) e a
		/// recompensa sai com o sorteio do servidor. Nulo se a luta não confere com a que começou.
		/// </summary>
		public static BattleReport? Finish(Random random, GameDatabase database, PlayerState player, BattleRequest request, BattleTicket ticket, DateTime now)
		{
			if (request.Kind == BattleKind.Arena)
				return FinishArena(random, database, player, request, ticket, now);

			if (Resolve(database, player, request, now, ticket.Clock) is not { } door)
				return null;

			var report = new BattleReport { Won = ticket.Won, Seconds = ticket.Seconds, Duration = ticket.Duration, Seed = ticket.Seed, Clock = ticket.Clock };
			var members = ticket.Members;
			if (!ticket.Auto)
			{
				if (!Teams.Of(player, door.Content).SequenceEqual(ticket.Team))
					return null;

				var team = PlayerTeam.Build(player, database, door.Content);
				if (team.Members.Count == 0)
					return null;

				var log = new List<BattleEvent>();
				report.Won = BattleReplay.Run(BattleFactory.Create(database, team, door.Encounter, ticket.Seed), request.Actions, player.FocusBoss, log);
				report.Seconds = RecordSeconds(log);
				report.Duration = ScreenSeconds(log);
				members = MembersOf(database, player, ticket.Team);
			}

			report.ReadyIn = ReadyIn(ticket, report.Duration, now);
			if (report.ReadyIn > 0 || !report.Won)
				return report;

			if (door.Problem != EntryProblem.None)
			{
				report.Problem = door.Problem;
				return report;
			}

			var before = StatePatch.Snapshot(player);
			report.Reward = BattleRewardData.Of(door.Victory(random, ticket.Team));
			report.NewBest = Records.Submit(player, door.Record, report.Seconds, members);
			report.Patch = StatePatch.Diff(before, player);
			return report;
		}

		/// <summary>Quantos segundos faltam para o fim valer: o tempo da luta na tela (com folga) contado de quando o bilhete saiu.</summary>
		public static double ReadyIn(BattleTicket ticket, double duration, DateTime now) =>
			Math.Max(0, (ticket.IssuedAt.AddSeconds(duration * ReadyShare - ReadySlack) - now).TotalSeconds);

		/// <summary>O tempo do recorde: a luta na velocidade 1× da tela.</summary>
		private static double RecordSeconds(IEnumerable<BattleEvent> log) => BattlePace.Seconds(log, BattlePace.Speeds[0].Factor);

		/// <summary>O tempo da luta na tela, na velocidade do automático (a mais rápida).</summary>
		private static double ScreenSeconds(IEnumerable<BattleEvent> log) => BattlePace.Seconds(log, BattlePace.AutoBattleFactor);

		private static List<RecordMember> MembersOf(GameDatabase database, PlayerState player, IEnumerable<int> team) =>
			team.Select(player.Monster)
				.OfType<OwnedSummon>()
				.Where(m => !m.Stored && database.HasSummon(m.SummonId))
				.Select(m => new RecordMember(m.SummonId, m.Awakened))
				.ToList();

		private static BattleTeam? DefenseOf(GameDatabase database, PlayerState player, string? key, DateTime utc, out bool canFight)
		{
			canFight = false;
			if (key != null && key.StartsWith("list:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(5), out var index))
			{
				canFight = Arena.CanFight(player, database.Arena, index);
				var opponents = Arena.Opponents(player, database);
				return index >= 0 && index < opponents.Count ? opponents[index].Team : null;
			}

			if (key != null && key.StartsWith("rival:", StringComparison.Ordinal) && database.Arena.Rivals.FirstOrDefault(r => Arena.RivalKey(r.Id) == key) is { } rival)
			{
				canFight = Arena.CanFightRival(player, rival, utc);
				return ArenaTeams.Rival(database, rival);
			}

			return null;
		}

		/// <summary>A Arena gasta o Convite no começo e marca a luta pendente (abandonar conta como derrota): o começo já muda a conta.</summary>
		private static BattleReport? StartArena(Random random, GameDatabase database, PlayerState player, BattleRequest request, DateTime now, out BattleTicket? ticket)
		{
			ticket = null;
			var before = StatePatch.Snapshot(player);
			Teams.SeedFromCampaign(player, Teams.Arena);
			if (PlayerTeam.Build(player, database, Teams.Arena).Members.Count == 0)
				return null;

			Arena.Renew(player, database.Arena, now, random);
			if (DefenseOf(database, player, request.Key, now, out var can) == null)
				return null;

			if (!can || !Arena.Begin(player, request.Key!))
				return new BattleReport { Problem = EntryProblem.NoInvites, Clock = now, Patch = StatePatch.Diff(before, player) };

			var seed = random.Next();
			ticket = new BattleTicket { Key = KeyOf(request), Seed = seed, Clock = now, Team = Teams.Of(player, Teams.Arena).ToList() };
			return new BattleReport { Seed = seed, Clock = now, Patch = StatePatch.Diff(before, player) };
		}

		private static BattleReport? FinishArena(Random random, GameDatabase database, PlayerState player, BattleRequest request, BattleTicket ticket, DateTime now)
		{
			if (player.Arena.Pending != request.Key || DefenseOf(database, player, request.Key, now, out _) is not { } defense)
				return null;

			if (!Teams.Of(player, Teams.Arena).SequenceEqual(ticket.Team))
				return null;

			var team = PlayerTeam.Build(player, database, Teams.Arena);
			if (team.Members.Count == 0)
				return null;

			// O bônus da sequência é o do começo: é com ele que o jogo montou a luta que o jogador viu.
			var (health, attack) = Arena.StreakBonus(player, database.Arena, ticket.Clock);
			var session = BattleFactory.CreateDuel(database, team, defense, ticket.Seed, health, attack);
			var log = new List<BattleEvent>();
			var won = BattleReplay.Run(session, request.Actions, player.FocusBoss, log);
			var report = new BattleReport { Won = won, Seed = ticket.Seed, Clock = ticket.Clock, Duration = ScreenSeconds(log) };
			report.ReadyIn = ReadyIn(ticket, report.Duration, now);
			if (report.ReadyIn > 0)
				return report;

			var before = StatePatch.Snapshot(player);
			var result = won ? ArenaResult.Win : session.TimedOut ? ArenaResult.Draw : ArenaResult.Loss;
			report.Outcome = Arena.Finish(player, database.Arena, database, result, now, random);
			report.Patch = StatePatch.Diff(before, player);
			return report;
		}

		/// <summary>A porta da luta: o problema de entrar, a equipe, o encontro, o que a vitória paga (a quem lutou) e a chave do recorde.</summary>
		private sealed record Door(EntryProblem Problem, string Content, Encounter Encounter, Func<Random, IReadOnlyList<int>, VictoryReward> Victory, string Record);

		/// <summary>A porta do pedido: <paramref name="now"/> confere e paga; <paramref name="started"/> diz o encontro e o recorde (o mês em que a luta começou).</summary>
		private static Door? Resolve(GameDatabase database, PlayerState player, BattleRequest request, DateTime now, DateTime started)
		{
			switch (request.Kind)
			{
				case BattleKind.Stage:
					var stage = database.Stages.FirstOrDefault(s => s.Number == request.Number);
					return stage == null ? null : new Door(Campaign.Check(player, stage), Teams.Campaign, stage.Encounter, (random, team) => Campaign.ApplyVictory(random, player, stage, database, team), Records.StageKey(stage.Number));

				case BattleKind.Floor:
					var dungeon = database.Dungeons.FirstOrDefault(d => d.Id == request.Dungeon);
					if (dungeon == null || request.Floor < 1 || request.Floor > dungeon.Floors.Count)
						return null;
					return new Door(Dungeons.Check(player, dungeon, request.Floor), dungeon.Id, dungeon.Floor(request.Floor).Encounter, (random, team) => Dungeons.ApplyVictory(random, player, dungeon, request.Floor, team), Records.FloorKey(dungeon.Id, request.Floor));

				case BattleKind.Constellation:
					var exploration = database.Exploration;
					if (request.Number < 1 || request.Number > exploration.Constellations.Count)
						return null;
					var constellation = exploration.Constellation(request.Number);
					var variation = Exploration.VariationOf(started);
					return new Door(Exploration.Check(player, exploration, request.Number, now), Teams.Exploration, constellation.Encounter(variation), (_, team) => Exploration.ApplyVictory(player, exploration, request.Number, now, team), Records.ConstellationKey(constellation.Id, variation));

				default:
					return null;
			}
		}
	}
}
