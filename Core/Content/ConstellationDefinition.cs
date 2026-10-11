using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma das 88 constelações da Exploração Estelar (Data/exploration.json), um andar do percurso. Ela
	/// tem a sua mecânica fixa (<see cref="Influence"/>), a força dos inimigos (estrelas, nível e
	/// escala, a mesma o ano todo) e a recompensa fixa (<see cref="Reward"/>); o que gira a cada mês é o
	/// desafio, um por Exploração (<see cref="Challenges"/>, na ordem de <see cref="ExplorationDefinition.Explorations"/>).
	/// </summary>
	public sealed record ConstellationDefinition
	{
		public string Id { get; init; } = "";

		/// <summary>O nome em português ("Ursa Menor").</summary>
		public string Name { get; init; } = "";

		/// <summary>O nome oficial da IAU, em latim ("Ursa Minor"): não se traduz.</summary>
		public string Latin { get; init; } = "";

		public Hemisphere Hemisphere { get; init; }

		/// <summary>O ícone da constelação, em Assets/Icons (sem o arquivo, o mapa mostra uma estrela).</summary>
		public string Icon { get; init; } = "";

		/// <summary>Ascensão reta do centro, em horas, e declinação, em graus: onde ela fica no céu de verdade.</summary>
		public double Ra { get; init; }

		public double Dec { get; init; }

		/// <summary>
		/// Onde o orbe fica no mapa da faixa dela, em pontos de um mapa de <see cref="ExplorationDefinition.ChartWidth"/>
		/// de largura: a projeção do céu de verdade, afastada só o bastante para os orbes não se tocarem.
		/// </summary>
		public IReadOnlyList<double> Chart { get; init; } = new List<double>();

		/// <summary>Estrelas e nível de todos os inimigos.</summary>
		public int Stars { get; init; } = 6;

		public int Level { get; init; }

		/// <summary>Multiplica Vida e Ataque de todos os inimigos (a força da constelação no percurso).</summary>
		public double Scale { get; init; } = 1;

		public InfluenceDefinition Influence { get; init; } = new();

		public ConstellationReward Reward { get; init; } = new();

		/// <summary>Um desafio por Exploração, na ordem delas.</summary>
		public IReadOnlyList<ConstellationChallenge> Challenges { get; init; } = new List<ConstellationChallenge>();

		/// <summary>O desafio da Exploração <paramref name="variation"/> (0 a 2).</summary>
		public ConstellationChallenge Challenge(int variation) => Challenges[variation % Challenges.Count];

		/// <summary>A luta da Exploração <paramref name="variation"/>: as ondas dela, com a Influência da constelação.</summary>
		public Encounter Encounter(int variation)
		{
			var challenge = Challenge(variation);
			return new Encounter(Stars, Level, challenge.Waves, Scale * challenge.Scale, Rules: Influence.Rules);
		}

		/// <summary>O guardião da Exploração <paramref name="variation"/>: o primeiro inimigo marcado como guardião, ou o primeiro chefe da última onda.</summary>
		public StageEnemy? Guardian(int variation) =>
			Challenge(variation).Waves.LastOrDefault()?.FirstOrDefault(slot => slot.Guardian || slot.Enemy != null);
	}
}
