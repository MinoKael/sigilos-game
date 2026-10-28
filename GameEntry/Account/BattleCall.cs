using Sigilos.Core.Progression;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// O que o servidor respondeu ao começo ou ao fim de uma luta: o relatório, ou o motivo de não ter feito (<c>offline</c>,
	/// <c>conflict</c>, <c>refused</c>, ou <c>ticket</c>: o bilhete da luta não vale mais e ela não paga).
	/// </summary>
	public sealed record BattleCall(BattleReport? Report, string? Error)
	{
		public bool Ok => Report != null;
	}
}
