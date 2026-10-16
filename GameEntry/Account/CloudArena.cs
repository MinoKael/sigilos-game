using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Sigilos.Core.Battle;
using Sigilos.Core.Progression;

namespace Sigilos.GameEntry.Account
{
	/// <summary>A busca de adversários: a lista, ou nula se o servidor não respondeu certo.</summary>
	public sealed record ArenaFetch(ApiResponse Response, List<ArenaOnlineOpponent>? Opponents);

	/// <summary>
	/// A Arena no servidor: mandar a defesa da conta (a equipe e os pontos de agora) e buscar as defesas de outras
	/// contas, as mais perto dos pontos, para ocuparem a lista no lugar dos adversários locais. A luta continua
	/// toda no aparelho; o servidor só guarda e entrega as defesas.
	/// </summary>
	public sealed class CloudArena
	{
		public const int Count = 10;

		private readonly AuthClient _auth;

		public CloudArena(AuthClient auth) => _auth = auth;

		public Task<ApiResponse> UploadDefense(int points, BattleTeam team) =>
			_auth.Call(HttpMethod.Put, "arena/defense", new { points, team = ArenaWire.ToJson(team) });

		public async Task<ArenaFetch> Opponents(int points)
		{
			var response = await _auth.Call(HttpMethod.Get, $"arena/opponents?points={points}&count={Count}");
			return new ArenaFetch(response, response.Ok ? Read(response.Body) : null);
		}

		/// <summary>A lista como o servidor manda; quem vem sem id, sem nome ou sem equipe válida fica de fora.</summary>
		public static List<ArenaOnlineOpponent>? Read(JsonElement body)
		{
			if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("opponents", out var list) || list.ValueKind != JsonValueKind.Array)
				return null;

			var found = new List<ArenaOnlineOpponent>();
			foreach (var item in list.EnumerateArray())
			{
				if (item.ValueKind != JsonValueKind.Object)
					continue;

				var id = Text(item, "id");
				var name = Text(item, "name");
				if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || !item.TryGetProperty("team", out var team) || ArenaWire.ReadTeam(team) is not { Count: > 0 } members)
					continue;

				var points = item.TryGetProperty("points", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
				found.Add(new ArenaOnlineOpponent { Id = id, Name = name, Points = points, Team = members });
			}

			return found;
		}

		private static string? Text(JsonElement item, string property) =>
			item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	}
}
