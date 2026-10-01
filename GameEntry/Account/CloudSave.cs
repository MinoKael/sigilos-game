using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	/// <summary>A cópia do save na nuvem: a revisão (sobe a cada envio aceito), quando foi gravada e o JSON do save.</summary>
	public sealed record CloudCopy(long Revision, DateTimeOffset SavedAt, string Json);

	/// <summary>O download: <see cref="Copy"/> nula com <see cref="Ok"/> é conta sem save na nuvem (404).</summary>
	public sealed record CloudDownload(ApiResponse Response, CloudCopy? Copy)
	{
		public bool Ok => Copy != null || Response.Status == 404;
	}

	/// <summary>
	/// Baixa e envia o save. O servidor guarda o JSON como um bloco opaco (compactado lá) com a revisão ao
	/// lado: muda o formato do save, o servidor não muda. Quem decide o que fazer com as duas cópias é o
	/// <see cref="CloudSync"/>.
	///
	/// O envio leva a sessão deste aparelho e a revisão da nuvem de que ele partiu (<c>baseRevision</c>). O
	/// servidor recusa (409) se a sessão é de outro aparelho (<c>session_taken</c>) ou se a nuvem mudou
	/// desde aquela revisão (<c>save_conflict</c>): um aparelho nunca apaga, sem saber, o que outro enviou.
	/// </summary>
	public sealed class CloudSave
	{
		/// <summary>O limite do servidor para o save (o JSON, antes do Base64).</summary>
		public const int MaxBytes = 1_048_576;

		private readonly AuthClient _auth;

		public CloudSave(AuthClient auth) => _auth = auth;

		public async Task<CloudDownload> Download()
		{
			var response = await _auth.Call(HttpMethod.Get, "save");
			if (!response.Ok || response.Text("data") is not { } data)
				return new CloudDownload(response, null);

			try
			{
				var json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
				return new CloudDownload(response, new CloudCopy(response.Number("revision") ?? 0, response.Time("savedAt") ?? DateTimeOffset.UtcNow, json));
			}
			catch (FormatException)
			{
				return new CloudDownload(response with { Status = 502, Error = "invalid_save_data" }, null);
			}
		}

		public Task<ApiResponse> Upload(Guid sessionId, long baseRevision, long revision, string json)
		{
			var bytes = Encoding.UTF8.GetBytes(json);
			if (bytes.Length > MaxBytes)
				return Task.FromResult(new ApiResponse(400, "save_too_large", default));
			return _auth.Call(HttpMethod.Put, "save", new { sessionId, baseRevision, revision, data = Convert.ToBase64String(bytes) });
		}
	}
}
