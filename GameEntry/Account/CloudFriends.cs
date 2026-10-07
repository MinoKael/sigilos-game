using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Sigilos.Core.Social;

namespace Sigilos.GameEntry.Account
{
	/// <summary>A busca dos amigos: a lista, ou nula se o servidor não respondeu certo.</summary>
	public sealed record FriendsFetch(ApiResponse Response, FriendList? List);

	/// <summary>
	/// Os amigos no servidor (docs/SERVIDOR_PROPRIO.md): buscar a lista, convidar pelo nome, aceitar e desfazer.
	/// Quem guarda tudo é o servidor; o jogo só mostra a última busca.
	/// </summary>
	public sealed class CloudFriends
	{
		private readonly AuthClient _auth;

		public CloudFriends(AuthClient auth) => _auth = auth;

		public async Task<FriendsFetch> Fetch()
		{
			var response = await _auth.Call(HttpMethod.Get, "friends");
			return new FriendsFetch(response, response.Ok ? Read(response.Body) : null);
		}

		/// <summary>Convida pelo nome. Se o outro já tinha convidado esta conta, viram amigos na hora (<c>status</c> <c>friends</c>).</summary>
		public Task<ApiResponse> Invite(string name) => _auth.Call(HttpMethod.Post, "friends/requests", new { name });

		/// <summary>Aceita o convite que a conta <paramref name="id"/> mandou.</summary>
		public Task<ApiResponse> Accept(string id) => _auth.Call(HttpMethod.Post, $"friends/{Uri.EscapeDataString(id)}/accept");

		/// <summary>Desfaz o que houver com a conta <paramref name="id"/>: recusa o convite dela, cancela o desta ou desfaz a amizade.</summary>
		public Task<ApiResponse> Remove(string id) => _auth.Call(HttpMethod.Delete, $"friends/{Uri.EscapeDataString(id)}");

		/// <summary>A lista como o servidor manda; quem vem sem id ou sem nome fica de fora.</summary>
		public static FriendList? Read(JsonElement body)
		{
			if (body.ValueKind != JsonValueKind.Object)
				return null;

			var max = body.TryGetProperty("max", out var limit) && limit.ValueKind == JsonValueKind.Number && limit.TryGetInt32(out var value) && value > 0 ? value : FriendList.Limit;
			return new FriendList(People(body, "friends"), People(body, "incoming"), People(body, "outgoing"), max);
		}

		private static List<Friend> People(JsonElement body, string property)
		{
			var people = new List<Friend>();
			if (!body.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array)
				return people;

			foreach (var item in list.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.Object && Text(item, "id") is { Length: > 0 } id && Text(item, "name") is { Length: > 0 } name)
					people.Add(new Friend(id, name, item.TryGetProperty("online", out var online) && online.ValueKind == JsonValueKind.True));
			}

			return people;
		}

		private static string? Text(JsonElement item, string property) =>
			item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	}
}
