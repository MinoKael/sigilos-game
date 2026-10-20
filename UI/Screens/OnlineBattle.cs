using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Sigilos.Core.Rta;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Uma luta da Arena do Véu vista da tela: a cópia local da luta (<see cref="RtaReplica"/>), de que lado o jogador
	/// está, por onde manda a decisão ao servidor e a fila das mensagens que ele manda de volta. A fila existe
	/// porque o servidor não espera a tela: as jogadas chegam enquanto a anterior ainda é animada.
	/// </summary>
	public sealed class OnlineBattle
	{
		private readonly Channel<RtaMessage> _inbox = Channel.CreateUnbounded<RtaMessage>();
		private readonly Func<RtaMessage, Task<bool>> _send;

		public OnlineBattle(RtaReplica replica, RtaSeat seat, string me, string rival, Func<RtaMessage, Task<bool>> send)
		{
			Replica = replica;
			Seat = seat;
			Me = me;
			Rival = rival;
			_send = send;
		}

		public RtaReplica Replica { get; }

		public RtaSeat Seat { get; }

		public string Me { get; }

		public string Rival { get; }

		public Task<bool> Send(RtaMessage message) => _send(message);

		/// <summary>Chegou uma mensagem do servidor (<c>turn</c>, <c>acted</c> ou <c>end</c>).</summary>
		public void Post(RtaMessage message) => _inbox.Writer.TryWrite(message);

		public ValueTask<RtaMessage> Next() => _inbox.Reader.ReadAsync();
	}
}
