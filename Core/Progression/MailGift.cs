using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Progression
{
	public enum MailGiftKind
	{
		Monster,
		Rune,
		Avatar,
	}

	/// <summary>
	/// Um presente da carta que não é moeda. No servidor, a chave da recompensa diz o que é e o valor diz
	/// quantos:
	/// - <c>monster:knight_fire</c>: cópias novas do monstro (nível 1, como as da invocação);
	/// - <c>rune:5</c>, <c>rune:5:Hero</c> ou <c>rune:5:Hero:Vigor</c>: runas sorteadas dessas estrelas, e da
	///   raridade e do conjunto, se a chave diz;
	/// - <c>avatar:imp_fire</c> ou <c>avatar:imp_fire:awakened</c>: o retrato da conta, mesmo sem ter o monstro;
	///   <c>avatar:astronaut_helmet</c>: um retrato especial (<see cref="SpecialAvatars"/>, os desenhos de Assets/Avatars).
	/// </summary>
	public sealed record MailGift(MailGiftKind Kind, string Id, int Count, int Grade = 0, RuneRarity? Rarity = null, RuneSet? Set = null, bool Awakened = false)
	{
		/// <summary>Lê a chave do servidor; nula quando ela não é um presente conhecido.</summary>
		public static MailGift? Parse(string key, int count)
		{
			var parts = key.Trim().Split(':');
			if (parts.Length < 2 || parts[1].Length == 0 || count < 1)
				return null;

			switch (parts[0].ToLowerInvariant())
			{
				case "monster" when parts.Length == 2:
					return new MailGift(MailGiftKind.Monster, parts[1], count);
				case "avatar" when parts.Length == 2:
					return new MailGift(MailGiftKind.Avatar, parts[1], 1);
				case "avatar" when parts.Length == 3 && parts[2].Equals("awakened", StringComparison.OrdinalIgnoreCase):
					return new MailGift(MailGiftKind.Avatar, parts[1], 1, Awakened: true);
				case "rune" when parts.Length <= 4 && int.TryParse(parts[1], out var grade) && grade is >= 1 and <= 6:
					RuneRarity? rarity = null;
					RuneSet? set = null;
					if (parts.Length >= 3)
					{
						if (!Enum.TryParse<RuneRarity>(parts[2], true, out var parsed) || !Enum.IsDefined(parsed))
							return null;
						rarity = parsed;
					}

					if (parts.Length == 4)
					{
						if (!Enum.TryParse<RuneSet>(parts[3], true, out var parsed) || !Enum.IsDefined(parsed))
							return null;
						set = parsed;
					}

					return new MailGift(MailGiftKind.Rune, "", count, grade, rarity, set);
				default:
					return null;
			}
		}
	}
}
