using System;

namespace Sigilos.Core.Social
{
	/// <summary>
	/// Uma linha do Chat global, como chegou ao vivo: quem disse (o nome da conta), quando e o quê, uma fala
	/// (<see cref="Text"/>) ou um feito (<see cref="Feat"/>). Fica só na memória do jogo aberto: nem o
	/// servidor nem o aparelho guardam o chat.
	/// </summary>
	public sealed record ChatLine(string From, DateTimeOffset At, string? Text, Feat? Feat)
	{
		/// <summary>O tamanho máximo de uma fala (o servidor recusa acima disso).</summary>
		public const int MaxText = 200;
	}
}
