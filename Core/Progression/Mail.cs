using System;
using System.Collections.Generic;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Uma carta do correio, enviada pelo servidor (para uma conta ou para todas) ou pelo próprio jogo
	/// (<see cref="Letters"/>): o título, o texto, o que ela traz (moedas em <see cref="Rewards"/>; monstros, runas e
	/// retratos em <see cref="Gifts"/>) e até quando dá para coletar (nulo: sem prazo).
	/// </summary>
	public sealed record Mail(string Id, string Title, string Text, IReadOnlyDictionary<MailItem, int> Rewards, DateTimeOffset SentAt, DateTimeOffset? ExpiresAt)
	{
		/// <summary>Os presentes que não são moeda; vazio na carta só de moedas.</summary>
		public IReadOnlyList<MailGift> Gifts { get; init; } = Array.Empty<MailGift>();

		/// <summary>De onde veio a carta do jogo (<see cref="Letters.ArenaSeason"/>...); nulo na do servidor.</summary>
		public string? Kind { get; init; }
	}
}
