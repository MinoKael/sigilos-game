using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// O Conjurador durante a luta: fora de campo, sem Vida, na barra de Ímpeto como qualquer unidade.
	/// No turno dele, lança uma página que possa pagar ou canaliza Éter.
	/// </summary>
	public sealed class ConjurerSeat : ITurnTaker
	{
		public ConjurerSeat(ConjurerDefinition definition, double power, IReadOnlyList<PageSlot> pages)
		{
			Definition = definition;
			Power = power;
			Pages = pages;
		}

		public ConjurerDefinition Definition { get; }
		public string Name => Definition.Name;

		/// <summary>Faz nas páginas o papel que o Ataque faz nas habilidades.</summary>
		public double Power { get; }

		public IReadOnlyList<PageSlot> Pages { get; }

		public double Impeto { get; set; }
		public double TurnSpeed => Definition.Speed;
		public bool CanTakeTurn => true;
	}
}
