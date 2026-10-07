using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// A Exploração Estelar (Data/exploration.json; GDD, seção 11): o modo de onde vêm os recursos. Um
	/// percurso pelas 88 constelações do céu — as Boreais, as Equatoriais e as Austrais —, em que cada
	/// uma é um andar com a sua mecânica. O progresso recomeça todo mês, e a cada mês uma das três
	/// Explorações (<see cref="Explorations"/>) troca os desafios; a recompensa de cada constelação é
	/// sempre a mesma.
	/// </summary>
	public sealed record ExplorationDefinition
	{
		/// <summary>Abre depois de vencer esta fase da Campanha.</summary>
		public int UnlockStage { get; init; }

		/// <summary>A largura do mapa em que <see cref="ConstellationDefinition.Chart"/> foi desenhado.</summary>
		public double ChartWidth { get; init; } = 560;

		/// <summary>As três Explorações, na ordem em que se revezam.</summary>
		public IReadOnlyList<ExplorationVariation> Explorations { get; init; } = new List<ExplorationVariation>();

		/// <summary>Na ordem do percurso: a primeira é o andar 1.</summary>
		public IReadOnlyList<ConstellationDefinition> Constellations { get; init; } = new List<ConstellationDefinition>();

		/// <summary>A constelação do andar <paramref name="number"/> (de 1 em diante).</summary>
		public ConstellationDefinition Constellation(int number) => Constellations[number - 1];

		/// <summary>O andar da constelação no percurso (de 1 em diante); 0 se ela não existe.</summary>
		public int NumberOf(ConstellationDefinition constellation)
		{
			for (var i = 0; i < Constellations.Count; i++)
			{
				if (Constellations[i].Id == constellation.Id)
					return i + 1;
			}

			return 0;
		}

		/// <summary>O primeiro e o último andar da faixa; (0, -1) se ela não tem constelação.</summary>
		public (int First, int Last) Range(Hemisphere hemisphere)
		{
			var numbers = Constellations.Select((c, i) => (c, Number: i + 1)).Where(x => x.c.Hemisphere == hemisphere).Select(x => x.Number).ToList();
			return numbers.Count == 0 ? (0, -1) : (numbers.Min(), numbers.Max());
		}
	}
}
