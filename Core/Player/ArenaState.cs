using System;
using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O que a conta guarda da Arena Astral. Tudo com valor inicial: um save de antes do campo existir volta
	/// com a Arena zerada (1000 pontos) sem subir a versão do save. As regras estão em
	/// <see cref="Progression.Arena"/>.
	/// </summary>
	public sealed class ArenaState
	{
		/// <summary>Os pontos de Arena de quem nunca lutou, e para onde voltam a cada ciclo.</summary>
		public const int StartPoints = 1000;

		public int Points { get; set; } = StartPoints;

		/// <summary>Moeda da Arena: paga a Loja Cósmica.</summary>
		public int Astrarium { get; set; }

		public int Invites { get; set; } = 10;

		/// <summary>Desde quando <see cref="Invites"/> está contado; nulo na primeira vez que se abre.</summary>
		public DateTime? InviteClock { get; set; }

		/// <summary>Vitórias seguidas, e até quando valem.</summary>
		public int Streak { get; set; }

		public DateTime? StreakUntil { get; set; }

		/// <summary>O ciclo semanal em que a conta está (<see cref="Progression.Arena.CycleOf"/>); 0 = nunca abriu.</summary>
		public int Cycle { get; set; }

		/// <summary>Vitórias e derrotas do ciclo: sem lutas, o fim do ciclo não paga prêmio.</summary>
		public int CycleWins { get; set; }

		public int CycleLosses { get; set; }

		/// <summary>A semente da lista de adversários e os pontos de quando ela foi feita; 0 = sem lista.</summary>
		public int ListSeed { get; set; }

		public int ListPoints { get; set; }

		/// <summary>
		/// As defesas de outros jogadores que ocupam vagas da lista (no lugar de adversários locais), e de que lista
		/// elas são (<see cref="OnlineSeed"/> igual a <see cref="ListSeed"/>); sem conta, a lista é toda local.
		/// </summary>
		public List<Progression.ArenaOnlineOpponent> Online { get; set; } = new();

		public int OnlineSeed { get; set; }

		/// <summary>Os índices da lista já vencidos.</summary>
		public List<int> Beaten { get; set; } = new();

		/// <summary>Quando cada Rival volta a lutar.</summary>
		public Dictionary<string, DateTime> RivalReady { get; set; } = new();

		/// <summary>Compras da Loja Cósmica no período, pela oferta, e o dia do contador diário.</summary>
		public Dictionary<string, int> Bought { get; set; } = new();

		public string? BoughtDay { get; set; }

		/// <summary>
		/// Uma luta começou e não terminou (fechou o jogo no meio): o Convite já saiu, e contar como derrota
		/// impede abandonar para tentar de novo de graça. "list:3" ou "rival:id".
		/// </summary>
		public string? Pending { get; set; }

		/// <summary>O último pagamento semanal, para mostrar uma vez ao abrir.</summary>
		public string? LastPayoutGrade { get; set; }

		public int LastPayoutGold { get; set; }
		public int LastPayoutLegendary { get; set; }
		public bool PayoutUnseen { get; set; }
	}
}
