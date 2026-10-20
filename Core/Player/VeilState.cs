using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O que a conta guarda da Arena do Véu: o Áster (cópia do que o servidor tem; ele é quem manda), os Ecos do
	/// Véu que pagam a loja e as compras do período. Tudo com valor inicial: um save antigo volta com a Arena
	/// do Véu zerada. As regras estão em <see cref="Progression.Veil"/>.
	/// </summary>
	public sealed class VeilState
	{
		/// <summary>O Áster de quem nunca lutou: o mesmo ponto de partida da Arena Astral.</summary>
		public const int StartAster = ArenaState.StartPoints;

		public int Aster { get; set; } = StartAster;

		public int Ecos { get; set; }

		/// <summary>
		/// O total de Ecos do Véu que o servidor diz ter dado a esta conta e que a conta já recebeu. Ao conectar, o que o
		/// servidor tem a mais (uma partida que acabou com a conexão caída) entra de uma vez.
		/// </summary>
		public int EcosSeen { get; set; }

		public int Wins { get; set; }

		public int Losses { get; set; }

		/// <summary>Vitórias seguidas na Arena do Véu.</summary>
		public int Streak { get; set; }

		/// <summary>O ciclo semanal das compras (<see cref="Progression.Arena.CycleOf"/>); o limite semanal volta com ele.</summary>
		public int Cycle { get; set; }

		/// <summary>Compras da Loja do Véu no período, pela oferta, e o dia do contador diário.</summary>
		public Dictionary<string, int> Bought { get; set; } = new();

		public string? BoughtDay { get; set; }
	}
}
