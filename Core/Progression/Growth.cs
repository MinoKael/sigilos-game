using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Como os atributos de base crescem com estrelas e nível. Os atributos de cada variante em
	/// Data/summons ("stats" e "awakened_stats") são os do 6★ nível 40, sem runas: aqui eles encolhem
	/// para as estrelas e o nível de agora.
	///
	/// - Estrelas: todo monstro nasce nas estrelas naturais e evolui até 6★ (<see cref="Evolution"/>).
	///   Cada estrela tem o seu nível máximo (15, 20, 25, 30, 35 e 40) e a sua faixa de atributos, em
	///   fração do 6★ nível 40: 3★ vai de 22% a 40%, 4★ de 32% a 54%, 5★ de 43% a 74% e 6★ de 59% a
	///   100%. Dentro da estrela, o crescimento é em linha reta. Evoluir mantém o nível, então o
	///   monstro passa para a faixa da estrela nova no mesmo nível e os atributos sobem na hora (um
	///   3★ no 25 tem 40%; evoluído, o 4★ no 25 tem 50%). Velocidade não muda.
	/// - Estrelas naturais: não entram aqui. Quem nasce com mais estrelas já tem atributos maiores no
	///   arquivo, porque o orçamento dele é maior (Core/Content/StatModel.cs).
	/// </summary>
	public static class Growth
	{
		public const int MaxStars = 6;

		/// <summary>Fração do 6★ nível 40 no nível 1 e no nível máximo de cada estrela, do 1★ ao 6★.</summary>
		private static readonly (double Start, double End)[] Bands =
		{
			(0.114, 0.206),
			(0.159, 0.286),
			(0.221, 0.398),
			(0.318, 0.541),
			(0.433, 0.736),
			(0.587, 1.000),
		};

		/// <summary>Nível máximo das estrelas: 15 no 1★, +5 por estrela, 40 no 6★.</summary>
		public static int MaxLevel(int stars) => 10 + 5 * Math.Clamp(stars, 1, MaxStars);

		/// <summary>Fração dos atributos do 6★ nível 40 que estas estrelas e este nível têm.</summary>
		public static double Fraction(int stars, int level)
		{
			var clampedStars = Math.Clamp(stars, 1, MaxStars);
			var (start, end) = Bands[clampedStars - 1];
			var max = MaxLevel(clampedStars);
			var clampedLevel = Math.Clamp(level, 1, max);
			return start + (end - start) * (clampedLevel - 1) / (max - 1);
		}

		/// <summary>O nível mais alto de um inimigo: só eles passam do 6★ nível 40.</summary>
		public const int MaxFoeLevel = 60;

		/// <summary>
		/// A fração de um inimigo: a mesma do jogador até o 6★ nível 40; depois, a reta do 6★ continua até
		/// o nível <see cref="MaxFoeLevel"/> (cerca de 121% no 60). O jogador para no 40.
		/// </summary>
		public static double FoeFraction(int stars, int level)
		{
			if (stars < MaxStars || level <= MaxLevel(MaxStars))
				return Fraction(stars, level);

			var (start, end) = Bands[MaxStars - 1];
			var step = (end - start) / (MaxLevel(MaxStars) - 1);
			return end + step * (Math.Min(level, MaxFoeLevel) - MaxLevel(MaxStars));
		}

		/// <summary>Os atributos do 6★ nível 40 (<paramref name="full"/>) nas estrelas e no nível de agora.</summary>
		public static StatBlock Stats(StatBlock full, int stars, int level) => Scale(full, Fraction(stars, level));

		/// <summary>Os atributos de um inimigo: como <see cref="Stats"/>, mas passando do nível 40 (<see cref="FoeFraction"/>).</summary>
		public static StatBlock FoeStats(StatBlock full, int stars, int level) => Scale(full, FoeFraction(stars, level));

		private static StatBlock Scale(StatBlock full, double factor)
		{
			return full with
			{
				Health = Math.Round(full.Health * factor),
				Attack = Math.Round(full.Attack * factor),
				Defense = Math.Round(full.Defense * factor),
			};
		}
	}
}
