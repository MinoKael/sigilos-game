using System;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Mastering
{
	/// <summary>
	/// Volume percebido de um efeito curto: o RMS da janela de 50 ms mais forte, depois de uma curva parecida
	/// com a K da EBU R128 (grave pesa menos, a presença acima de 1,5 kHz pesa mais). Em dBFS.
	/// </summary>
	public static class Loudness
	{
		private const double Window = 0.05;
		private const double Hop = 0.01;

		public static double Of(double[] samples, int sampleRate)
		{
			var weighted = (double[])samples.Clone();
			Biquad.Of(FilterKind.Highpass, 60, 0.5, sampleRate).Run(weighted);
			Biquad.Of(FilterKind.HighShelf, 1500, 0.707, sampleRate, 4).Run(weighted);
			var window = (int)(Window * sampleRate);
			var hop = (int)(Hop * sampleRate);
			var best = 0.0;
			for (var start = 0; start == 0 || start + window <= weighted.Length; start += hop)
			{
				var end = Math.Min(weighted.Length, start + window);
				var sum = 0.0;
				for (var i = start; i < end; i++)
					sum += weighted[i] * weighted[i];
				best = Math.Max(best, sum / window);
				if (end >= weighted.Length)
					break;
			}
			return Db(Math.Sqrt(best));
		}

		/// <summary>A parte da energia abaixo de <paramref name="hz"/> (para o relatório: grave demais).</summary>
		public static double ShareBelow(double[] samples, int sampleRate, double hz)
		{
			var low = (double[])samples.Clone();
			Biquad.Of(FilterKind.Lowpass, hz, 0.707, sampleRate).Run(low);
			return Energy(low) / Math.Max(1e-12, Energy(samples));
		}

		/// <summary>A parte da energia acima de <paramref name="hz"/> (para o relatório: agudo que cansa).</summary>
		public static double ShareAbove(double[] samples, int sampleRate, double hz)
		{
			var high = (double[])samples.Clone();
			Biquad.Of(FilterKind.Highpass, hz, 0.707, sampleRate).Run(high);
			return Energy(high) / Math.Max(1e-12, Energy(samples));
		}

		public static double Db(double linear) => 20 * Math.Log10(Math.Max(1e-9, linear));

		public static double Linear(double db) => Math.Pow(10, db / 20);

		private static double Energy(double[] samples)
		{
			var sum = 0.0;
			foreach (var s in samples)
				sum += s * s;
			return sum;
		}
	}
}
