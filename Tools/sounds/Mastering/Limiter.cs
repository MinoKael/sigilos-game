using System;

namespace Sigilos.Sounds.Mastering
{
	/// <summary>
	/// Limitador de pico com antecipação: o ganho baixa um pouco antes do pico (2 ms, sem estalo) e volta em
	/// 80 ms. Nenhuma amostra passa do teto.
	/// </summary>
	public static class Limiter
	{
		private const double Lookahead = 0.002;
		private const double Release = 0.08;

		public static void Apply(double[] samples, double ceiling, int sampleRate)
		{
			var n = samples.Length;
			var gain = new double[n];
			for (var i = 0; i < n; i++)
			{
				var level = Math.Abs(samples[i]);
				gain[i] = level > ceiling ? ceiling / level : 1;
			}
			var ahead = Math.Max(1, (int)(Lookahead * sampleRate));
			var attack = 1.0 / ahead;
			for (var i = n - 2; i >= 0; i--)
				gain[i] = Math.Min(gain[i], gain[i + 1] + attack);
			var recover = 1 - Math.Exp(-1 / (Release * sampleRate));
			for (var i = 1; i < n; i++)
				gain[i] = Math.Min(gain[i], gain[i - 1] + (1 - gain[i - 1]) * recover);
			for (var i = 0; i < n; i++)
				samples[i] = Math.Clamp(samples[i] * gain[i], -ceiling, ceiling);
		}
	}
}
