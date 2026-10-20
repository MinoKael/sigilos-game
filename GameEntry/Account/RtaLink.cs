using Sigilos.Core.Rta;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// A conversa ao vivo da Arena do Véu: um WebSocket com o servidor (<c>/rta</c>, docs/SERVIDOR_PROPRIO.md) aberto
	/// enquanto o jogador está nela. Fila, montagem do time e luta passam por aqui como <see cref="RtaMessage"/>.
	/// Os avisos chegam na linha de execução de quem chamou <see cref="Open"/> (no jogo, a principal). Caída a
	/// conexão no meio de uma partida, o servidor joga pelo jogador no automático até o fim.
	/// </summary>
	public sealed class RtaLink : IRtaChannel
	{
		private const int MaxFrame = 1024 * 1024;

		private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

		private readonly Uri _address;
		private readonly Func<Task<string?>> _token;
		private readonly SemaphoreSlim _sending = new(1, 1);
		private ClientWebSocket? _socket;
		private CancellationTokenSource? _stop;

		/// <param name="server">O endereço do servidor de contas (terminado em /).</param>
		/// <param name="token">Um token de acesso válido; nulo fora da conta.</param>
		public RtaLink(Uri server, Func<Task<string?>> token)
		{
			_address = new UriBuilder(new Uri(server, "rta")) { Scheme = server.Scheme == Uri.UriSchemeHttps ? "wss" : "ws" }.Uri;
			_token = token;
		}

		/// <summary>Conectado: dá para falar e as mensagens chegam.</summary>
		public bool Live => _socket?.State == WebSocketState.Open;

		/// <summary>O servidor aceitou a conexão.</summary>
		public event Action? Opened;

		/// <summary>A conexão fechou (por <see cref="Close"/>, queda ou recusa). O valor é o motivo, se o servidor deu um.</summary>
		public event Action<string?>? Closed;

		/// <summary>Chegou uma mensagem do servidor.</summary>
		public event Action<RtaMessage>? Received;

		/// <summary>Conecta e fica ouvindo até <see cref="Close"/> ou a conexão cair. Já aberto ou abrindo, não faz nada.</summary>
		public async void Open()
		{
			if (_socket != null)
				return;

			var socket = new ClientWebSocket();
			var stop = new CancellationTokenSource();
			var live = false;
			string? reason = null;
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
				Opened?.Invoke();
				await Listen(socket, stop.Token);
			}
			catch (WebSocketException exception)
			{
				reason = exception.Message;
			}
			catch (HttpRequestException exception)
			{
				reason = exception.Message;
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

				// Quem pediu para fechar (Close) já sabe: só a queda e a recusa avisam.
				var asked = stop.IsCancellationRequested;
				socket.Dispose();
				stop.Dispose();
				if (!asked)
					Closed?.Invoke(live ? null : reason ?? "connect");
			}
		}

		/// <summary>Desliga (sair da Arena do Véu, sair da conta, jogo fechando).</summary>
		public void Close()
		{
			if (_stop is not { } stop)
				return;
			_socket = null;
			_stop = null;
			stop.Cancel();
		}

		/// <summary>Manda uma mensagem. Falso se não estava conectado ou a conexão caiu no envio.</summary>
		public async Task<bool> Send(RtaMessage message)
		{
			if (_socket is not { State: WebSocketState.Open } socket || _stop is not { } stop)
				return false;

			await _sending.WaitAsync();
			try
			{
				await socket.SendAsync(Encoding.UTF8.GetBytes(message.ToJson()), WebSocketMessageType.Text, true, stop.Token);
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
			var buffer = new byte[16 * 1024];
			while (true)
			{
				using var frame = new MemoryStream();
				ValueWebSocketReceiveResult result;
				do
				{
					result = await socket.ReceiveAsync(buffer.AsMemory(), stop);
					if (result.MessageType == WebSocketMessageType.Close)
						return;
					frame.Write(buffer, 0, result.Count);
					if (frame.Length > MaxFrame)
						return;
				}
				while (!result.EndOfMessage);

				if (result.MessageType != WebSocketMessageType.Text)
					continue;

				if (RtaMessage.Read(Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length)) is { } message)
					Received?.Invoke(message);
			}
		}
	}
}
