using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Como os atributos de base crescem com estrelas e nível. Os valores de Data/roles.json são os de
	/// uma 5★ natural em 6★ nível 40, sem Despertar e sem runas.
	///
	/// - Estrelas: todo monstro nasce nas estrelas naturais e evolui até 6★ (<see cref="Evolution"/>).
	///   Cada estrela tem o seu nível máximo (15, 20, 25, 30, 35 e 40) e a sua faixa de atributos, em
	///   fração do 6★ nível 40: 3★ vai de 22% a 40%, 4★ de 32% a 54%, 5★ de 43% a 74% e 6★ de 59% a
	///   100%. Evoluir volta ao nível 1, com atributos um pouco menores que no máximo da estrela
	///   anterior. Dentro da estrela, o crescimento é em linha reta. Velocidade não muda.
	/// - Estrelas naturais: no 6★ nível 40, as 5★ naturais usam 100% dos valores, as 4★ 92% e as 3★ 85%.
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

		public static double RarityFactor(int naturalStars) => naturalStars switch
		{
			>= 5 => 1.00,
			4 => 0.92,
			3 => 0.85,
			2 => 0.78,
			_ => 0.70,
		};

		/// <summary>Atributos de base: papel escalado pelas estrelas naturais, pelas estrelas de agora e pelo nível.</summary>
		public static StatBlock Stats(StatBlock roleBase, int naturalStars, int stars, int level)
		{
			var factor = RarityFactor(naturalStars) * Fraction(stars, level);
			return roleBase with
			{
				Health = Math.Round(roleBase.Health * factor),
				Attack = Math.Round(roleBase.Attack * factor),
				Defense = Math.Round(roleBase.Defense * factor),
			};
		}
	}
}
