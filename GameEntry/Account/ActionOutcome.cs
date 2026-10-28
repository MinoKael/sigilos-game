using Sigilos.Core.Progression;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// A resposta de uma ação do servidor (<see cref="ServerActions"/>): o que mudou, ou por que não deu.
	/// <see cref="Error"/>: <c>offline</c> (sem conexão: a ação não foi feita), <c>conflict</c> (a cópia do aparelho
	/// não é mais a do servidor; o jogo baixa de novo) ou <c>refused</c> (o servidor não aceitou o pedido).
	/// </summary>
	public sealed record ActionOutcome(ActionDelta? Delta, string? Error)
	{
		public bool Ok => Delta != null;
	}
}
