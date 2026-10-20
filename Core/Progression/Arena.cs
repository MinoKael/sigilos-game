using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>Como uma luta da Arena acabou. Estourar o limite de rodadas com gente de pé dos dois lados é empate.</summary>
	public enum ArenaResult
	{
		Win,
		Loss,
		Draw,
	}

	/// <summary>Um adversário da lista: a defesa de um alquimista (hoje montada pelo Grau dele; depois, a de um jogador).</summary>
	public sealed record ArenaOpponent(int Index, string Name, int Points, ArenaGrade Grade, BattleTeam Team, bool Beaten, bool Player = false);

	/// <summary>O prêmio do fim da semana, pelo Grau em que a conta terminou.</summary>
	public sealed record ArenaPayout(string GradeName, int Gold, int LegendaryScrolls);

	/// <summary>O que uma luta da Arena mudou, para o resultado mostrar.</summary>
	public sealed record ArenaOutcome(
		ArenaResult Result,
		bool Rival,
		int PointsDelta,
		int Points,
		int Streak,
		int Astrarium,
		int Gold,
		bool ListCleared,
		int BonusInvites,
		ArenaGrade GradeBefore,
		ArenaGrade GradeAfter);

	/// <summary>
	/// A Arena Astral: Convites (recarga por tempo), lista de dez adversários por pontos, sequência de vitórias com
	/// bônus que dura uma hora, Graus, Rivais com espera, ciclo semanal (domingo 22h) com prêmio pelo Grau e a
	/// Loja Cósmica. Tudo calculado pelo relógio que a tela passa; relógio que volta atrás não repete prêmio nem
	/// desfaz espera. A luta em si é <see cref="BattleFactory.CreateDuel"/>.
	/// </summary>
	public static class Arena
	{
		/// <summary>Um domingo às 22h: de onde se contam as semanas.</summary>
		private static readonly DateTime Anchor = new(2024, 1, 7, 22, 0, 0);

		/// <summary>O número da semana de <paramref name="now"/> (de 1 em diante): muda no domingo às 22h.</summary>
		public static int CycleOf(DateTime now) => Math.Max(1, (int)Math.Floor((now - Anchor).TotalDays / 7) + 1);

		/// <summary>Quando a semana de agora acaba.</summary>
		public static DateTime CycleEnds(DateTime now) => Anchor.AddDays(7.0 * CycleOf(now));

		/// <summary>
		/// Põe a conta em dia ao abrir a Arena (e ao abrir o jogo): luta abandonada vira derrota, os Convites
		/// recarregam, a sequência vencida some, a semana virada paga o prêmio e a lista nasce se faltar.
		/// Devolve o prêmio pago nesta chamada, se houve.
		/// </summary>
		public static ArenaPayout? Renew(PlayerState player, ArenaDefinition definition, DateTime now, Random random)
		{
			var arena = player.Arena;
			if (arena.Pending != null)
				Abandon(player, definition, now);

			Refill(arena, definition, now);

			if (arena.StreakUntil is { } until && until <= now)
			{
				arena.Streak = 0;
				arena.StreakUntil = null;
			}

			ArenaPayout? payout = null;
			var cycle = CycleOf(now);
			if (arena.Cycle == 0)
				arena.Cycle = cycle;
			else if (cycle > arena.Cycle)
			{
				if (arena.CycleWins + arena.CycleLosses > 0)
				{
					var grade = definition.GradeOf(arena.Points);
					payout = new ArenaPayout(grade.Name, grade.Gold, grade.LegendaryScrolls);
					player.Gold += grade.Gold;
					player.LegendaryScrolls += grade.LegendaryScrolls;
					arena.LastPayoutGrade = grade.Name;
					arena.LastPayoutGold = grade.Gold;
					arena.LastPayoutLegendary = grade.LegendaryScrolls;
					arena.PayoutUnseen = true;
				}

				arena.Cycle = cycle;
				arena.Points = ArenaState.StartPoints;
				arena.Streak = 0;
				arena.StreakUntil = null;
				arena.CycleWins = arena.CycleLosses = 0;
				arena.ListSeed = 0;
				arena.Bought.Clear();
				arena.RivalReady.Clear();
			}

			if (arena.ListSeed == 0)
				NewList(arena, random);

			return payout;
		}

		/// <summary>
		/// Recarrega os Convites pelo tempo. O relógio que voltou atrás não mexe na contagem (voltar e avançar não
		/// recarrega nada); só uma contagem mais de uma semana à frente do relógio, que não é de uso normal, recomeça.
		/// </summary>
		private static void Refill(ArenaState arena, ArenaDefinition definition, DateTime now)
		{
			if (arena.InviteClock is not { } clock)
			{
				arena.InviteClock = now;
				return;
			}

			if (now < clock)
			{
				if (clock - now > TimeSpan.FromDays(7))
					arena.InviteClock = now;
				return;
			}

			if (arena.Invites >= definition.MaxInvites)
			{
				arena.InviteClock = now;
				return;
			}

			var step = TimeSpan.FromMinutes(Math.Max(1, definition.InviteMinutes));
			var gained = (int)((now - clock).Ticks / step.Ticks);
			if (gained <= 0)
				return;

			arena.Invites = Math.Min(definition.MaxInvites, arena.Invites + gained);
			arena.InviteClock = arena.Invites >= definition.MaxInvites ? now : clock + step * gained;
		}

		/// <summary>Quando o próximo Convite chega; nulo com os Convites cheios.</summary>
		public static DateTime? NextInvite(PlayerState player, ArenaDefinition definition) =>
			player.Arena.Invites >= definition.MaxInvites || player.Arena.InviteClock is not { } clock
				? null
				: clock + TimeSpan.FromMinutes(Math.Max(1, definition.InviteMinutes));

		private static void NewList(ArenaState arena, Random random)
		{
			arena.ListSeed = random.Next(1, int.MaxValue);
			arena.ListPoints = arena.Points;
			arena.Beaten.Clear();
			arena.Online.Clear();
			arena.OnlineSeed = 0;
		}

		/// <summary>Uma lista nova de graça: a sequência acaba (como a referência), os Convites ficam.</summary>
		public static void RefreshList(PlayerState player, Random random)
		{
			NewList(player.Arena, random);
			player.Arena.Streak = 0;
			player.Arena.StreakUntil = null;
		}

		/// <summary>A lista de agora ainda não perguntou ao servidor por defesas de outros jogadores.</summary>
		public static bool NeedsOnline(PlayerState player) => player.Arena.ListSeed != 0 && player.Arena.OnlineSeed != player.Arena.ListSeed;

		/// <summary>
		/// Põe as defesas de outros jogadores na lista de agora, cada uma na vaga de pontos mais perto dos dela; as
		/// vagas que sobram seguem com adversários locais. Com dez jogadores, nenhum adversário local aparece.
		/// A lista vazia também conta: a pergunta foi feita, e não se repete até a lista renovar.
		/// </summary>
		public static void SetOnline(PlayerState player, ArenaDefinition definition, IEnumerable<ArenaOnlineOpponent> found)
		{
			var state = player.Arena;
			state.Online = new List<ArenaOnlineOpponent>();
			state.OnlineSeed = state.ListSeed;
			var free = Enumerable.Range(0, definition.ListOffsets.Count).ToList();
			foreach (var other in found.Where(o => o.Team.Count > 0).OrderBy(o => o.Points).ThenBy(o => o.Id, StringComparer.Ordinal))
			{
				if (free.Count == 0)
					break;

				var slot = free.OrderBy(i => Math.Abs(state.ListPoints + definition.ListOffsets[i] - other.Points)).ThenBy(i => i).First();
				free.Remove(slot);
				other.Slot = slot;
				state.Online.Add(other);
			}
		}

		/// <summary>Os adversários da lista de agora; a mesma lista até vencer todos ou renovar.</summary>
		public static IReadOnlyList<ArenaOpponent> Opponents(PlayerState player, GameDatabase database)
		{
			var arena = player.Arena;
			var definition = database.Arena;
			var list = new List<ArenaOpponent>();
			for (var i = 0; i < definition.ListOffsets.Count; i++)
			{
				if (arena.OnlineSeed == arena.ListSeed && arena.Online.FirstOrDefault(o => o.Slot == i) is { } other
					&& ArenaWire.ToBattle(database, other.Team) is { } theirs)
				{
					var theirPoints = Math.Max(0, other.Points);
					list.Add(new ArenaOpponent(i, other.Name, theirPoints, definition.GradeOf(theirPoints), theirs, arena.Beaten.Contains(i), true));
					continue;
				}

				var random = new Random(unchecked(arena.ListSeed * 31 + i * 7919));
				var points = Math.Max(0, arena.ListPoints + definition.ListOffsets[i]);
				var grade = definition.GradeOf(points);
				var name = definition.Names.Count > 0 ? definition.Names[random.Next(definition.Names.Count)] : $"#{i + 1}";
				var team = ArenaTeams.Defense(database, grade.Ghost, random);
				list.Add(new ArenaOpponent(i, name, points, grade, team, arena.Beaten.Contains(i)));
			}

			return list;
		}

		/// <summary>Vida e Ataque a mais (frações) que a sequência dá agora: soma dos degraus já alcançados.</summary>
		public static (double Health, double Attack) StreakBonus(PlayerState player, ArenaDefinition definition, DateTime now)
		{
			var arena = player.Arena;
			if (arena.Streak <= 0 || arena.StreakUntil is not { } until || until <= now)
				return (0, 0);

			var steps = definition.Streak.Where(s => arena.Streak >= s.Wins).ToList();
			return (steps.Sum(s => s.Health) / 100.0, steps.Sum(s => s.Attack) / 100.0);
		}

		public static int Gain(ArenaDefinition definition, int mine, int theirs) =>
			Math.Clamp((int)Math.Round(definition.PointsBase + (theirs - mine) / (double)definition.PointsSlope), definition.PointsMin, definition.PointsMax);

		public static int Loss(ArenaDefinition definition, int mine, int theirs) =>
			Math.Clamp((int)Math.Round(definition.PointsBase - (theirs - mine) / (double)definition.PointsSlope), definition.PointsMin, definition.PointsMax);

		/// <summary>Pode lutar com este da lista: há Convite, nada pendente, e ele ainda não foi vencido.</summary>
		public static bool CanFight(PlayerState player, ArenaDefinition definition, int index) =>
			player.Arena.Invites > 0 && player.Arena.Pending == null && index >= 0 && index < definition.ListOffsets.Count && !player.Arena.Beaten.Contains(index);

		/// <summary>Quando o Rival volta a lutar; nulo se já pode.</summary>
		public static DateTime? RivalReadyAt(PlayerState player, ArenaRival rival, DateTime now) =>
			player.Arena.RivalReady.TryGetValue(rival.Id, out var at) && at > now ? at : null;

		public static bool CanFightRival(PlayerState player, ArenaRival rival, DateTime now) =>
			player.Arena.Invites > 0 && player.Arena.Pending == null && RivalReadyAt(player, rival, now) == null;

		/// <summary>Gasta o Convite e marca a luta como em andamento: sair no meio conta como derrota.</summary>
		public static bool Begin(PlayerState player, string pending)
		{
			var arena = player.Arena;
			if (arena.Invites <= 0 || arena.Pending != null)
				return false;

			arena.Invites--;
			arena.Pending = pending;
			return true;
		}

		public static string ListKey(int index) => $"list:{index}";

		public static string RivalKey(string id) => $"rival:{id}";

		/// <summary>A luta pendente (o jogo fechou, ou o jogador desistiu) vale derrota.</summary>
		public static ArenaOutcome? Abandon(PlayerState player, ArenaDefinition definition, DateTime now) =>
			player.Arena.Pending == null ? null : Finish(player, definition, null, ArenaResult.Loss, now, new Random());

		/// <summary>
		/// Fecha a luta pendente com <paramref name="result"/>. Na lista: pontos, sequência, Astrarium e, vencida
		/// a lista toda, os Convites e a lista nova. No Rival: Astrarium e Ouro, e a espera dele; sem pontos.
		/// </summary>
		public static ArenaOutcome? Finish(PlayerState player, ArenaDefinition definition, GameDatabase? database, ArenaResult result, DateTime now, Random random)
		{
			var arena = player.Arena;
			if (arena.Pending is not { } pending)
				return null;

			arena.Pending = null;
			var before = definition.GradeOf(arena.Points);
			var points = arena.Points;
			var astrarium = 0;
			var gold = 0;
			var cleared = false;
			var bonus = 0;
			var delta = 0;
			var rival = pending.StartsWith("rival:", StringComparison.Ordinal);

			if (rival)
			{
				var id = pending["rival:".Length..];
				if (result == ArenaResult.Win && definition.Rivals.FirstOrDefault(r => r.Id == id) is { } found)
				{
					astrarium = before.Astrarium;
					gold = found.Gold;
					arena.Astrarium += astrarium;
					player.Gold += gold;
					arena.RivalReady[id] = now.AddHours(found.CooldownHours);
				}
			}
			else
			{
				var index = int.TryParse(pending["list:".Length..], out var parsed) ? parsed : -1;
				var theirs = Math.Max(0, arena.ListPoints + (index >= 0 && index < definition.ListOffsets.Count ? definition.ListOffsets[index] : 0));
				switch (result)
				{
					case ArenaResult.Win:
						delta = Gain(definition, arena.Points, theirs);
						arena.Points += delta;
						arena.Streak++;
						arena.StreakUntil = now.AddMinutes(definition.StreakMinutes);
						arena.CycleWins++;
						astrarium = before.Astrarium;
						arena.Astrarium += astrarium;
						if (index >= 0 && !arena.Beaten.Contains(index))
							arena.Beaten.Add(index);
						if (arena.Beaten.Count >= definition.ListOffsets.Count)
						{
							cleared = true;
							bonus = definition.ListBonusInvites;
							arena.Invites += bonus;
							NewList(arena, random);
						}

						break;
					case ArenaResult.Loss:
						delta = -Loss(definition, arena.Points, theirs);
						arena.Points = Math.Max(0, arena.Points + delta);
						delta = arena.Points - points;
						arena.Streak = 0;
						arena.StreakUntil = null;
						arena.CycleLosses++;
						break;
					default:
						arena.Streak = 0;
						arena.StreakUntil = null;
						break;
				}
			}

			return new ArenaOutcome(result, rival, delta, arena.Points, arena.Streak, astrarium, gold, cleared, bonus, before, definition.GradeOf(arena.Points));
		}

		// Loja Cósmica -------------------------------------------------------------------------------

		/// <summary>Zera o contador diário quando o dia virou.</summary>
		private static void RollDay(ArenaState arena, ArenaDefinition definition, DateTime now)
		{
			// Só vira para um dia depois do guardado: o relógio que volta atrás não zera os limites do dia.
			var day = now.ToString("yyyy-MM-dd");
			if (arena.BoughtDay != null && string.CompareOrdinal(day, arena.BoughtDay) <= 0)
				return;

			foreach (var offer in definition.Shop.Where(o => o.Period == ArenaPeriod.Day))
				arena.Bought.Remove(offer.Id);
			arena.BoughtDay = day;
		}

		public static int BoughtOf(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now)
		{
			RollDay(player.Arena, definition, now);
			return player.Arena.Bought.GetValueOrDefault(offer.Id);
		}

		public static bool IsSoldOut(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now) =>
			offer.Limit > 0 && BoughtOf(player, definition, offer, now) >= offer.Limit;

		public static bool CanBuy(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now) =>
			player.Arena.Astrarium >= offer.Price && !IsSoldOut(player, definition, offer, now);

		/// <summary>Compra da Loja Cósmica: paga o Astrarium, conta no limite e entrega (Núcleos pelo caminho dos marcos).</summary>
		public static bool Buy(PlayerState player, ArenaDefinition definition, ArenaOffer offer, DateTime now)
		{
			if (!CanBuy(player, definition, offer, now))
				return false;

			var arena = player.Arena;
			arena.Astrarium -= offer.Price;
			arena.Bought[offer.Id] = arena.Bought.GetValueOrDefault(offer.Id) + 1;
			Deliver(player, offer);

			return true;
		}

		/// <summary>Entrega a compra de uma oferta (Loja Cósmica ou Loja do Véu): Núcleos pelo caminho dos marcos.</summary>
		internal static void Deliver(PlayerState player, ArenaOffer offer)
		{
			switch (offer.Item)
			{
				case ArenaItem.Scrolls:
					player.Scrolls += offer.Amount;
					break;
				case ArenaItem.LightDarkScrolls:
					player.LightDarkScrolls += offer.Amount;
					break;
				case ArenaItem.InfusionCores:
					Milestones.Grant(player, new Prize(0, 0, offer.Amount));
					break;
				case ArenaItem.ReappraisalGems:
					player.ReappraisalGems += offer.Amount;
					break;
				case ArenaItem.Mana:
					player.Mana += offer.Amount;
					break;
			}
		}
	}
}
