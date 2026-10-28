using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	/// <summary>A cópia do save na nuvem: a revisão (sobe a cada mudança do servidor), quando foi gravada e o JSON do save.</summary>
	public sealed record CloudCopy(long Revision, DateTimeOffset SavedAt, string Json);

	/// <summary>O download: <see cref="Copy"/> nula com <see cref="Ok"/> é conta sem save na nuvem (404).</summary>
	public sealed record CloudDownload(ApiResponse Response, CloudCopy? Copy)
	{
		public bool Ok => Copy != null || Response.Status == 404;
	}

	/// <summary>
	/// As rotas do save da conta. O save é do servidor: o aparelho só baixa a cópia e pede ações, lutas e a troca de nome,
	/// e cada resposta traz o que mudou. O aparelho nunca envia o save.
	///
	/// Cada pedido leva a sessão deste aparelho e a revisão de que ele partiu (<c>baseRevision</c>). O servidor recusa
	/// (409) se a sessão é de outro aparelho (<c>session_taken</c>) ou se a cópia dele já é outra (<c>save_conflict</c>):
	/// o aparelho então baixa de novo antes de pedir mais.
	/// </summary>
	public sealed class CloudSave
	{
		private readonly AuthClient _auth;

		public CloudSave(AuthClient auth) => _auth = auth;

		public async Task<CloudDownload> Download()
		{
			var response = await _auth.Call(HttpMethod.Get, "save?gz=1");
			if (!response.Ok || response.Text("gz") is null && response.Text("data") is null)
				return new CloudDownload(response, null);

			try
			{
				var json = response.Text("gz") is { } packed
					? Encoding.UTF8.GetString(Sigilos.Core.Player.SaveCodec.Decompress(Convert.FromBase64String(packed)) ?? throw new FormatException())
					: Encoding.UTF8.GetString(Convert.FromBase64String(response.Text("data")!));
				return new CloudDownload(response, new CloudCopy(response.Number("revision") ?? 0, response.Time("savedAt") ?? DateTimeOffset.UtcNow, json));
			}
			catch (FormatException)
			{
				return new CloudDownload(response with { Status = 502, Error = "invalid_save_data" }, null);
			}
		}

		/// <summary>Uma ação do servidor sobre o save dele, a partir da revisão <paramref name="baseRevision"/>; o pedido vai como texto, no mesmo JSON do save.</summary>
		public Task<ApiResponse> Action(Guid sessionId, long baseRevision, Sigilos.Core.Progression.ActionRequest request) =>
			_auth.Call(HttpMethod.Post, "game/action", new { sessionId, baseRevision, request = request.ToJson() });

		/// <summary>Pede ao servidor que comece o jogo desta conta (a conta nova, ou a de antes de a versão zerar as contas): quem cria é ele.</summary>
		public Task<ApiResponse> Init(Guid sessionId) =>
			_auth.Call(HttpMethod.Post, "game/init", new { sessionId });

		/// <summary>
		/// O começo (<paramref name="begin"/>) ou o fim de uma luta do servidor, a partir da revisão <paramref name="baseRevision"/>.
		/// O começo devolve o bilhete; o fim leva o bilhete e paga.
		/// </summary>
		public Task<ApiResponse> Battle(Guid sessionId, long baseRevision, Sigilos.Core.Progression.BattleRequest request, bool begin) =>
			_auth.Call(HttpMethod.Post, begin ? "game/battle/start" : "game/battle", new { sessionId, baseRevision, request = request.ToJson() });

		/// <summary>
		/// Dá ou troca o nome da conta. O servidor confere o nome e, se a conta já tinha um, cobra a troca da Loja; a
		/// resposta traz o nome aceito e o que mudou no save.
		/// </summary>
		public Task<ApiResponse> Rename(Guid sessionId, long baseRevision, string name) =>
			_auth.Call(HttpMethod.Post, "game/rename", new { sessionId, baseRevision, name });
	}
}
