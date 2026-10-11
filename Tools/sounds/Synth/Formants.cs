namespace Sigilos.Sounds.Synth
{
	/// <summary>As três primeiras formantes de cada vogal (Hz), a largura de cada faixa e o volume relativo.</summary>
	public static class Formants
	{
		public static readonly double[] Bandwidths = { 90, 110, 160 };

		public static readonly double[] Gains = { 1, 0.6, 0.25 };

		public static double[] Of(Vowel vowel) => vowel switch
		{
			Vowel.A => new double[] { 800, 1150, 2800 },
			Vowel.E => new double[] { 400, 2000, 2550 },
			Vowel.I => new double[] { 280, 2250, 3000 },
			Vowel.O => new double[] { 450, 800, 2830 },
			_ => new double[] { 325, 700, 2530 },
		};
	}
}
