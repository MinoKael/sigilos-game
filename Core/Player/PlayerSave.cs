using System.Text.Json;
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

		/// <summary>Nulo quando o save é de um formato antigo (<see cref="PlayerState.CurrentVersion"/>).</summary>
		public static PlayerState? FromJson(string json)
		{
			var player = JsonSerializer.Deserialize<PlayerState>(json, Options);
			return player is { Version: PlayerState.CurrentVersion } ? player : null;
		}
	}
}
