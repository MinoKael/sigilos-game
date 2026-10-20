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
		public const int MaxBytes = Sigilos.Core.Player.SaveCodec.MaxBytes;

		private readonly AuthClient _auth;
		private readonly Func<int> _format;

		/// <param name="format">O formato de envio que o servidor disse aceitar ao tomar a sessão (0: só o JSON puro, 1: gzip).</param>
		public CloudSave(AuthClient auth, Func<int>? format = null)
		{
			_auth = auth;
			_format = format ?? (() => 0);
		}

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

		/// <summary>Quanto tempo o envio do que mudou fica desligado depois de falhar duas vezes seguidas (servidor de outra versão, ou sem a rota).</summary>
		private const long DeltaPauseMilliseconds = 600_000;

		private int _deltaFailures;
		private long _deltaPausedUntil;

		/// <summary>O servidor aceita só o que mudou (<see cref="UploadDelta"/>) e o envio não está em pausa.</summary>
		public bool CanDelta => _format() >= 2 && Environment.TickCount64 >= _deltaPausedUntil;

		/// <summary>
		/// Envia <paramref name="json"/> sobre a revisão <paramref name="baseRevision"/>: só o que mudou desde
		/// <paramref name="baseline"/> (o JSON que a nuvem tem naquela revisão) quando o servidor aceita e o delta compensa; o
		/// save inteiro no resto. Se o servidor não aplica o delta (versão diferente, hash que não bate, rota que não existe), o
		/// mesmo envio segue com o save inteiro, e duas falhas seguidas desligam o delta por <see cref="DeltaPauseMilliseconds"/>.
		/// </summary>
		public async Task<ApiResponse> Send(Guid sessionId, long baseRevision, string json, string? baseline)
		{
			if (baseline != null && CanDelta)
			{
				var delta = await Task.Run(() => Sigilos.Core.Player.SaveDelta.Diff(baseline, json));
				if (delta != null && delta.Length < json.Length / 2)
				{
					var response = await UploadDelta(sessionId, baseRevision, baseRevision + 1, delta, Sigilos.Core.Player.SaveCodec.Hash(json));
					if (response.Ok)
					{
						_deltaFailures = 0;
						return response;
					}

					if (!response.Conflict("delta_mismatch") && !response.Conflict("delta_failed") && response.Status is not (404 or 405))
						return response;

					if (++_deltaFailures >= 2)
					{
						_deltaFailures = 0;
						_deltaPausedUntil = Environment.TickCount64 + DeltaPauseMilliseconds;
					}
				}
			}

			return await Upload(sessionId, baseRevision, baseRevision + 1, json);
		}

		/// <summary>
		/// Envia só o que mudou desde a revisão <paramref name="baseRevision"/>, com o hash do save inteiro que o servidor deve
		/// obter. <c>delta_mismatch</c> ou <c>delta_failed</c> (409) pedem o save inteiro.
		/// </summary>
		public Task<ApiResponse> UploadDelta(Guid sessionId, long baseRevision, long revision, string delta, string hash) =>
			_auth.Call(HttpMethod.Put, "save/delta", new { sessionId, baseRevision, revision, delta = Convert.ToBase64String(Sigilos.Core.Player.SaveCodec.Compress(Encoding.UTF8.GetBytes(delta))), hash });

		public async Task<ApiResponse> Upload(Guid sessionId, long baseRevision, long revision, string json)
		{
			var bytes = Encoding.UTF8.GetBytes(json);
			if (bytes.Length > MaxBytes)
				return new ApiResponse(400, "save_too_large", default);
			if (_format() >= 1)
			{
				var gz = await Task.Run(() => Convert.ToBase64String(Sigilos.Core.Player.SaveCodec.Compress(bytes)));
				return await _auth.Call(HttpMethod.Put, "save", new { sessionId, baseRevision, revision, gz });
			}

			return await _auth.Call(HttpMethod.Put, "save", new { sessionId, baseRevision, revision, data = Convert.ToBase64String(bytes) });
		}
	}
}
