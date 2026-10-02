using System;
using System.Collections.Generic;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Uma carta do correio, enviada pelo servidor (para uma conta ou para todas): o título, o texto, o que
	/// ela traz e até quando dá para coletar (nulo: sem prazo).
	/// </summary>
	public sealed record Mail(string Id, string Title, string Text, IReadOnlyDictionary<MailItem, int> Rewards, DateTimeOffset SentAt, DateTimeOffset? ExpiresAt);
}
