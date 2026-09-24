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
	}
}
