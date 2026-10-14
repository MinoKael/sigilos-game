using System.Text.Json;
using System.Text.Json.Nodes;
using Sigilos.Core.Content;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Converte o <see cref="PlayerState"/> em texto e de volta. Não abre arquivo nenhum: quem lê e
	/// grava em user:// é o GameEntry, e os testes usam strings.
	/// </summary>
	public static class PlayerSave
	{
		private static readonly JsonSerializerOptions Options = new(GameDatabase.JsonOptions) { WriteIndented = true };

		public static string ToJson(PlayerState player) => JsonSerializer.Serialize(player, Options);

		public static PlayerState? FromJson(string json)
		{
			if (JsonNode.Parse(json) is not JsonObject node)
				return null;

			var player = node.Deserialize<PlayerState>(Options);
			return player is { Version: PlayerState.CurrentVersion } ? player : null;
		}

		/// <summary>
		/// Quando a conta começou, lido de um save de qualquer versão (inclusive uma anterior, que não se lê
		/// inteiro). Nulo se o texto não é um save ou não traz a data.
		/// </summary>
		public static System.DateTime? StartedOf(string? json)
		{
			if (string.IsNullOrWhiteSpace(json))
				return null;

			try
			{
				if (JsonNode.Parse(json) is not JsonObject node)
					return null;

				foreach (var (name, value) in node)
				{
					if (string.Equals(name, nameof(PlayerState.Started), System.StringComparison.OrdinalIgnoreCase))
						return value is JsonValue text && text.TryGetValue<System.DateTime>(out var started) ? started : null;
				}

				return null;
			}
			catch (System.Exception exception) when (exception is JsonException or System.FormatException or System.InvalidOperationException)
			{
				return null;
			}
		}

		/// <summary>
		/// A conta nova que veio no lugar de um save antigo guarda a data de começo do antigo: a mais velha
		/// vale. Sem data herdada, fica como está.
		/// </summary>
		public static void InheritStarted(PlayerState player, System.DateTime? inherited)
		{
			if (inherited is { } date && (player.Started == null || date < player.Started))
				player.Started = date;
		}

		/// <summary>
		/// O save é de uma versão anterior à atual: subir <see cref="PlayerState.CurrentVersion"/> zera as
		/// contas, e um save assim vira backup e dá lugar a um jogo novo, no aparelho e na nuvem. Um save de
		/// versão mais nova (o jogo está desatualizado) ou um texto que nem é save não conta.
		/// </summary>
		public static bool IsObsolete(string json)
		{
			try
			{
				if (JsonNode.Parse(json) is not JsonObject node)
					return false;

				foreach (var (name, value) in node)
				{
					if (string.Equals(name, nameof(PlayerState.Version), System.StringComparison.OrdinalIgnoreCase))
						return value is JsonValue number && number.TryGetValue<int>(out var version) && version < PlayerState.CurrentVersion;
				}

				// Sem o campo: anterior a ele existir.
				return true;
			}
			catch (JsonException)
			{
				return false;
			}
		}
	}
}
