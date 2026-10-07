using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sigilos.Core.Social;

namespace Sigilos.UI
{
	/// <summary>
	/// O Chat global do jogo aberto: as últimas <see cref="MaxLines"/> linhas que chegaram desde que a conta
	/// conectou, só na memória (sair da conta ou fechar o jogo apaga), e se a conexão está viva. Quem alimenta
	/// é o GameRoot, com a conexão da conta; quem mostra são o balão (<see cref="Components.ChatBubble"/>, com a
	/// última linha ao lado) e a janela (<see cref="Screens.ChatDialog"/>), que fala por <see cref="Say"/>.
	/// </summary>
	public sealed class ChatFeed
	{
		public const int MaxLines = 100;

		private readonly List<ChatLine> _lines = new();

		/// <summary>Da mais velha para a mais nova.</summary>
		public IReadOnlyList<ChatLine> Lines => _lines;

		public bool Live { get; private set; }

		/// <summary>Conectando: a conta está conectada e a conexão do chat ainda não abriu.</summary>
		public bool Connecting { get; private set; }

		/// <summary>O nome desta conta (as falas dela saem noutra cor); nulo numa conta sem nome.</summary>
		public string? Me { get; set; }

		/// <summary>A janela do chat está aberta: o balão não abre outra nem mostra a última linha por cima dela.</summary>
		public bool Reading { get; set; }

		/// <summary>Manda uma fala; falso se não saiu (sem conexão).</summary>
		public Func<string, Task<bool>>? Say { get; set; }

		/// <summary>Uma linha chegou (a mais velha pode ter saído da lista).</summary>
		public event Action<ChatLine>? Added;

		/// <summary>Mudou a conexão: <see cref="Live"/> ou <see cref="Connecting"/>.</summary>
		public event Action? Changed;

		/// <summary>O servidor recusou o que este jogo mandou (o erro, como <c>rate_limited</c>).</summary>
		public event Action<string>? Refused;

		public void Add(ChatLine line)
		{
			_lines.Add(line);
			if (_lines.Count > MaxLines)
				_lines.RemoveAt(0);
			Added?.Invoke(line);
		}

		public void SetState(bool live, bool connecting)
		{
			if (Live == live && Connecting == connecting)
				return;
			Live = live;
			Connecting = connecting;
			Changed?.Invoke();
		}

		public void Refuse(string error) => Refused?.Invoke(error);

		/// <summary>Esquece tudo (saiu da conta).</summary>
		public void Clear()
		{
			_lines.Clear();
			Live = false;
			Connecting = false;
			Me = null;
			Changed?.Invoke();
		}
	}
}
