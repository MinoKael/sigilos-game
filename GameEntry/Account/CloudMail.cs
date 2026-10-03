using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Sigilos.Core.Progression;

namespace Sigilos.GameEntry.Account
{
	/// <summary>A busca do correio: as cartas que o servidor ainda não viu coletadas por esta conta.</summary>
	public sealed record MailFetch(ApiResponse Response, IReadOnlyList<Mail> Mail)
	{
		public bool Ok => Response.Ok;
	}

	/// <summary>
	/// O correio no servidor (docs/SERVIDOR_PROPRIO.md): buscar as cartas desta conta e avisar que uma foi
	/// coletada. Quem aplica a recompensa é o <see cref="Mailbox"/>, antes do aviso. Recompensa de um tipo
	/// que este jogo não conhece é deixada de fora da carta.
	/// </summary>
	public sealed class CloudMail
	{
		private readonly AuthClient _auth;

		public CloudMail(AuthClient auth) => _auth = auth;

		public async Task<MailFetch> Fetch()
		{
			var response = await _auth.Call(HttpMethod.Get, "mail");
			var mail = new List<Mail>();
			if (response.Ok && response.Body.ValueKind == JsonValueKind.Object && response.Body.TryGetProperty("mail", out var list) && list.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in list.EnumerateArray())
				{
					if (Read(item) is { } letter)
						mail.Add(letter);
				}
			}

			return new MailFetch(response, mail);
		}

		/// <summary>Avisa que a carta foi coletada. Repetir não faz mal: o servidor só anota de novo.</summary>
		public Task<ApiResponse> Claim(string id) => _auth.Call(HttpMethod.Post, $"mail/{Uri.EscapeDataString(id)}/claim");

		private static Mail? Read(JsonElement item)
		{
			if (item.ValueKind != JsonValueKind.Object || Text(item, "id") is not { } id || Time(item, "sentAt") is not { } sent)
				return null;

			var rewards = new Dictionary<MailItem, int>();
			var gifts = new List<MailGift>();
			if (item.TryGetProperty("rewards", out var list) && list.ValueKind == JsonValueKind.Object)
			{
				foreach (var reward in list.EnumerateObject())
				{
					if (reward.Value.ValueKind != JsonValueKind.Number || !reward.Value.TryGetInt32(out var amount) || amount <= 0)
						continue;
					// Moeda pelo nome ("gold"); presente pela chave com dois-pontos ("monster:knight_fire").
					if (!reward.Name.Contains(':') && Enum.TryParse<MailItem>(reward.Name, true, out var kind))
						rewards[kind] = amount;
					else if (MailGift.Parse(reward.Name, amount) is { } gift)
						gifts.Add(gift);
				}
			}

			return new Mail(id, Text(item, "title") ?? "", Text(item, "text") ?? "", rewards, sent, Time(item, "expiresAt")) { Gifts = gifts };
		}

		private static string? Text(JsonElement item, string property) =>
			item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

		private static DateTimeOffset? Time(JsonElement item, string property) =>
			item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var time) ? time : null;
	}
}
