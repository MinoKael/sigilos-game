using System;
using System.Collections.Generic;

namespace Sigilos.Core.Progression
{
	/// <summary>Monta a <see cref="Mail"/> a partir das recompensas como o servidor as guarda: moeda pelo nome ("gold"), presente pela chave com dois-pontos ("monster:knight_fire").</summary>
	public static class MailFactory
	{
		public static Mail Of(string id, string title, string text, IEnumerable<KeyValuePair<string, int>> rewards, DateTimeOffset sentAt, DateTimeOffset? expiresAt)
		{
			var coins = new Dictionary<MailItem, int>();
			var gifts = new List<MailGift>();
			foreach (var (name, amount) in rewards)
			{
				if (amount <= 0)
					continue;

				if (!name.Contains(':') && Enum.TryParse<MailItem>(name, true, out var kind))
					coins[kind] = amount;
				else if (MailGift.Parse(name, amount) is { } gift)
					gifts.Add(gift);
			}

			return new Mail(id, title, text, coins, sentAt, expiresAt) { Gifts = gifts };
		}
	}
}
