using Sigilos.Core.Content;
using Sigilos.Core.Runes;
using Sigilos.Core.Social;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// O Chat global ao vivo: um WebSocket com o servidor (<c>/chat</c>, docs/SERVIDOR_PROPRIO.md) enquanto a
	/// conta está conectada. O servidor só repassa: não guarda nada nem manda o que foi dito antes de a conexão
	/// abrir. Quem abre e fecha é a <see cref="AccountSession"/>; caída, abre de novo no próximo batimento.
	///
	/// Os avisos chegam na linha de execução de quem chamou <see cref="Open"/> (no jogo, a principal).
	/// </summary>
	public sealed class ChatLink
	{
		private const int MaxFrame = 4096;

		private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

		private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

		private readonly Uri _address;
		private readonly Func<Task<string?>> _token;
		private readonly SemaphoreSlim _sending = new(1, 1);
		private ClientWebSocket? _socket;
		private CancellationTokenSource? _stop;

		/// <param name="server">O endereço do servidor de contas (terminado em /).</param>
		/// <param name="token">Um token de acesso válido; nulo fora da conta.</param>
		public ChatLink(Uri server, Func<Task<string?>> token)
		{
			_address = new UriBuilder(new Uri(server, "chat")) { Scheme = server.Scheme == Uri.UriSchemeHttps ? "wss" : "ws" }.Uri;
			_token = token;
		}

		/// <summary>Conectado: dá para falar e as linhas chegam.</summary>
		public bool Live => _socket?.State == WebSocketState.Open;

		/// <summary><see cref="Live"/> mudou.</summary>
		public event Action? LiveChanged;

		/// <summary>Chegou uma linha (de qualquer jogador, as suas também).</summary>
		public event Action<ChatLine>? Received;

		/// <summary>O servidor recusou o que este jogo mandou: <c>invalid_message</c>, <c>name_required</c> ou <c>rate_limited</c>.</summary>
		public event Action<string>? Refused;

		/// <summary>Conecta e fica ouvindo até <see cref="Close"/> ou a conexão cair. Já aberto ou abrindo, não faz nada.</summary>
		public async void Open()
		{
			if (_socket != null)
				return;

			var socket = new ClientWebSocket();
			var stop = new CancellationTokenSource();
			var live = false;
			_socket = socket;
			_stop = stop;
			try
			{
				if (await _token() is not { } token || stop.IsCancellationRequested)
					return;

				socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
				using (var connecting = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
				{
					connecting.CancelAfter(ConnectTimeout);
					await socket.ConnectAsync(_address, connecting.Token);
				}

				live = true;
				LiveChanged?.Invoke();
				await Listen(socket, stop.Token);
			}
			catch (WebSocketException)
			{
			}
			catch (HttpRequestException)
			{
			}
			catch (OperationCanceledException)
			{
			}
			catch (ObjectDisposedException)
			{
			}
			finally
			{
				if (_socket == socket)
				{
					_socket = null;
					_stop = null;
				}

				socket.Dispose();
				stop.Dispose();
				if (live)
					LiveChanged?.Invoke();
			}
		}

		/// <summary>Desliga (sair da conta, conexão perdida, jogo fechando).</summary>
		public void Close()
		{
			if (_stop is not { } stop)
				return;
			_socket = null;
			_stop = null;
			stop.Cancel();
		}

		/// <summary>Manda uma fala. Falso se não estava conectado ou a conexão caiu no envio.</summary>
		public Task<bool> Say(string text) => Send(JsonSerializer.Serialize(new { type = "say", text }, Json));

		/// <summary>Anuncia um feito deste jogador (só o que o mostra; o nome quem põe é o servidor).</summary>
		public Task<bool> Share(Feat feat) => Send(Write(feat));

		/// <summary>A mensagem de um feito, como o servidor espera.</summary>
		public static string Write(Feat feat) => feat switch
		{
			SummonFeat summon => JsonSerializer.Serialize(new { type = "feat", feat = "summon", summon = summon.SummonId }, Json),
			RuneFeat rune => JsonSerializer.Serialize(new { type = "feat", feat = "rune", set = rune.Set.ToString(), slot = rune.Slot, grade = rune.Grade, main = rune.Main.ToString(), rune.Substats }, Json),
			_ => throw new ArgumentOutOfRangeException(nameof(feat), feat, null),
		};

		/// <summary>
		/// Uma mensagem do servidor: a linha, ou nula com o <paramref name="error"/> da recusa. Linha de um tipo
		/// ou feito que este jogo não conhece (de uma versão mais nova) volta nula sem erro.
		/// </summary>
		public static ChatLine? Read(string json, out string? error)
		{
			error = null;
			JsonElement message;
			try
			{
				using var document = JsonDocument.Parse(json);
				message = document.RootElement.Clone();
			}
			catch (JsonException)
			{
				return null;
			}

			if (message.ValueKind != JsonValueKind.Object)
				return null;

			var type = Text(message, "type");
			if (type == "error")
			{
				error = Text(message, "error") ?? "invalid_message";
				return null;
			}

			if (Text(message, "from") is not { Length: > 0 } from || !message.TryGetProperty("at", out var at) || !at.TryGetDateTimeOffset(out var time))
				return null;

			return type switch
			{
				"say" when Text(message, "text") is { Length: > 0 } text => new ChatLine(from, time, text, null),
				"feat" when ReadFeat(message) is { } feat => new ChatLine(from, time, null, feat),
				_ => null,
			};
		}

		private static Feat? ReadFeat(JsonElement message) => Text(message, "feat") switch
		{
			"summon" when Text(message, "summon") is { Length: > 0 } summon => new SummonFeat(summon),
			"rune" when Name<RuneSet>(message, "set") is { } set && Name<RuneStat>(message, "main") is { } main &&
			            Number(message, "slot") is { } slot and >= 1 and <= RuneRules.Slots &&
                        Number(message, "grade") is { } grade and >= 1 and <= RuneRules.MaxGrade &&
			            List<RuneSubstat>(message, "substats") is { } substats  =>
				new RuneFeat(set, slot, grade, main, substats),
			_ => null,
		};

		private async Task<bool> Send(string json)
		{
			if (_socket is not { State: WebSocketState.Open } socket || _stop is not { } stop)
				return false;

			await _sending.WaitAsync();
			try
			{
				await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, stop.Token);
				return true;
			}
			catch (WebSocketException)
			{
				return false;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			catch (ObjectDisposedException)
			{
				return false;
			}
			finally
			{
				_sending.Release();
			}
		}

		private async Task Listen(ClientWebSocket socket, CancellationToken stop)
		{
			var buffer = new byte[MaxFrame];
			while (true)
			{
				var count = 0;
				ValueWebSocketReceiveResult result;
				do
				{
					// Maior que o servidor manda: vai até o fim e não serve (o JSON cortado não lê).
					if (count == buffer.Length)
						count = 0;
					result = await socket.ReceiveAsync(buffer.AsMemory(count), stop);
					if (result.MessageType == WebSocketMessageType.Close)
						return;
					count += result.Count;
				}
				while (!result.EndOfMessage);

				if (result.MessageType != WebSocketMessageType.Text)
					continue;

				var line = Read(Encoding.UTF8.GetString(buffer, 0, count), out var error);
				if (line != null)
					Received?.Invoke(line);
				else if (error != null)
					Refused?.Invoke(error);
			}
		}

		private static string? Text(JsonElement message, string property) =>
			message.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

		private static int? Number(JsonElement message, string property) =>
			message.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

		/// <summary>Um valor do enum pelo nome exato (número não vale).</summary>
		private static T? Name<T>(JsonElement message, string property) where T : struct, Enum =>
			Text(message, property) is { } name && Enum.TryParse<T>(name, false, out var value) && Enum.IsDefined(value) && value.ToString() == name ? value : null;

        private static List<T>? List<T>(JsonElement message, string property) =>
			message.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array ? value.Deserialize<List<T>>() : null;
    }
}
