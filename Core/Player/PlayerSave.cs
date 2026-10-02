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
