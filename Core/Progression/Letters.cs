using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// As cartas que o próprio jogo manda (<see cref="Letter"/>): o prêmio da temporada da Arena e os marcos de nível
	/// da conta vão para o correio em vez de cair direto na conta, e valem <see cref="Days"/> dias. O que cai numa luta
	/// continua direto. Ficam no save, então a coleta passa pela mesma ação das cartas do servidor
	/// (<see cref="ServerActionKind.ClaimMail"/>), com o id <see cref="Prefix"/> e o número da carta.
	/// </summary>
	public static class Letters
	{
		/// <summary>Quantos dias uma carta do jogo fica no correio.</summary>
		public const int Days = 7;

		/// <summary>O começo do id de uma carta do jogo no correio; o das cartas do servidor é um Guid.</summary>
		public const string Prefix = "auto:";

		/// <summary>O prêmio de fim de temporada da Arena Astral. Valores: o grau.</summary>
		public const string ArenaSeason = "arena_season";

		/// <summary>O marco de um nível da conta. Valores: o nível.</summary>
		public const string AccountLevel = "account_level";

		/// <summary>O prêmio do dia do check-in (<see cref="DailyCheckIn"/>). Valores: o dia do ciclo.</summary>
		public const string CheckIn = "checkin";

		/// <summary>O primeiro 6★ de um elemento (<see cref="SixStar"/>). Valores: o id do monstro e o elemento.</summary>
		public const string SixStarFirst = "six_star_first";

		/// <summary>Um 6★ depois do primeiro do elemento. Valores: o id do monstro.</summary>
		public const string SixStar = "six_star";

		/// <summary>Manda uma carta. Sem nada a entregar, não manda.</summary>
		public static Letter? Send(PlayerState player, string kind, DateTime now, IReadOnlyDictionary<string, int> rewards, params string[] args)
		{
			var paid = rewards.Where(r => r.Value > 0).ToDictionary(r => r.Key, r => r.Value);
			if (paid.Count == 0)
				return null;

			var letter = new Letter
			{
				Id = player.NextLetterId++,
				Kind = kind,
				Args = args.ToList(),
				Rewards = paid,
				SentAt = now,
				ExpiresAt = now.AddDays(Days),
			};
			player.Inbox.Add(letter);
			return letter;
		}

		/// <summary>Manda um prêmio de marco (<see cref="Prize"/>). Kits de sigilo não viajam pelo correio: entram direto, à espera da escolha.</summary>
		public static Letter? Send(PlayerState player, string kind, DateTime now, Prize prize, params string[] args)
		{
			player.PendingRuneKits += prize.RuneKits;
			return Send(player, kind, now, new Dictionary<string, int>
			{
				["legendaryScrolls"] = prize.LegendaryScrolls,
				["lightDarkScrolls"] = prize.LightDarkScrolls,
				["infusionCores"] = prize.InfusionCores,
				["fragments"] = prize.FragmentCores * RewardDrops.FragmentCoreValue,
			}, args);
		}

		/// <summary>A carta do id do correio, se é uma carta do jogo que ainda está no save.</summary>
		public static Letter? Find(PlayerState player, string? mailId) =>
			mailId != null && mailId.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(mailId[Prefix.Length..], out var id)
				? player.Inbox.FirstOrDefault(l => l.Id == id)
				: null;

		/// <summary>O id da carta no correio.</summary>
		public static string MailId(Letter letter) => Prefix + letter.Id;

		/// <summary>A carta como as do servidor, com o título e o texto já na língua do jogo.</summary>
		public static Mail ToMail(Letter letter, string title, string text) =>
			MailFactory.Of(MailId(letter), title, text, letter.Rewards, Utc(letter.SentAt), Utc(letter.ExpiresAt)) with { Kind = letter.Kind };

		/// <summary>As datas da carta são sempre UTC; marcar o tipo evita o erro de um DateTime local com fuso diferente de zero.</summary>
		private static DateTimeOffset Utc(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc), TimeSpan.Zero);

		/// <summary>Paga a carta e a tira do save. Falso, sem mudar nada, se o prazo dela já passou.</summary>
		public static bool Claim(Random random, GameDatabase database, PlayerState player, Letter letter, DateTime now)
		{
			if (now >= letter.ExpiresAt)
				return false;

			Mailbox.Pay(player, ToMail(letter, "", ""), database, random);
			player.Inbox.Remove(letter);
			return true;
		}

		/// <summary>Tira do save as cartas cujo prazo passou.</summary>
		public static void Prune(PlayerState player, DateTime now) => player.Inbox.RemoveAll(l => now >= l.ExpiresAt);
	}
}
