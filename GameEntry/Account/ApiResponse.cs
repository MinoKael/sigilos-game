using System;
using System.Text.Json;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// Uma resposta do servidor de contas, já lida: o código HTTP, o erro que o servidor nomeou
	/// (<c>{"error": "session_taken"}</c>) e o corpo em JSON.
	///
	/// <see cref="Status"/> 0 quer dizer que a pergunta nem chegou (sem rede, servidor fora do ar, tempo
	/// esgotado). O jogo trata isso como "sem internet": continua jogando e tenta de novo depois.
	/// </summary>
	public sealed record ApiResponse(int Status, string? Error, JsonElement Body)
	{
		public static readonly ApiResponse Unreachable = new(0, null, default);

		public bool Ok => Status is >= 200 and < 300;

		public bool Unreached => Status == 0;

		public bool Unauthorized => Status == 401;

		/// <summary>409 com este erro (<c>session_taken</c>, <c>save_conflict</c>...).</summary>
		public bool Conflict(string error) => Status == 409 && Error == error;

		/// <summary>Um texto do corpo; nulo se não houver.</summary>
		public string? Text(string property) =>
			Property(property) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

		/// <summary>Uma data do corpo; nula se não houver.</summary>
		public DateTimeOffset? Time(string property) =>
			Property(property) is { ValueKind: JsonValueKind.String } value && value.TryGetDateTimeOffset(out var time) ? time : null;

		/// <summary>Um número inteiro do corpo; nulo se não houver.</summary>
		public long? Number(string property) =>
			Property(property) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number) ? number : null;

		private JsonElement? Property(string property) =>
			Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty(property, out var value) ? value : null;
	}
}
