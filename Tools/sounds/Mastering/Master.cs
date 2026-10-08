using System;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Mastering
{
	/// <summary>
	/// A última etapa de todo efeito: corta grave e agudo da classe, apara a cauda, suaviza as pontas e leva o
	/// volume percebido ao alvo da classe (mais <c>levelDb</c> do efeito), com o pico sempre no teto ou abaixo.
	/// </summary>
	public static class Master
	{
		private const double FadeIn = 0.0006;
		private const double FadeOutMax = 0.03;

		public static double[] Finish(double[] samples, int sampleRate, MixProfile profile, double levelDb)
		{
			var x = (double[])samples.Clone();
			Biquad.Of(FilterKind.Highpass, profile.LowCut, 0.707, sampleRate).Run(x);
			Biquad.Of(FilterKind.Lowpass, profile.HighCut, 0.707, sampleRate).Run(x);
			x = Trim(x, sampleRate, profile.TrimDb);
			Fade(x, sampleRate);

			var loudness = Loudness.Of(x, sampleRate);
			var peak = Loudness.Db(LayerRenderer.Peak(x));
			var gainDb = profile.LoudnessDb + levelDb - loudness;
			var over = peak + gainDb - profile.CeilingDb;
			if (over > profile.LimitDb)
				gainDb -= over - profile.LimitDb;
			var gain = Loudness.Linear(gainDb);
			for (var i = 0; i < x.Length; i++)
				x[i] *= gain;
			Limiter.Apply(x, Loudness.Linear(profile.CeilingDb), sampleRate);
			return x;
		}

		/// <summary>Corta o que sobra depois do último trecho acima de <paramref name="trimDb"/> do pico.</summary>
		private static double[] Trim(double[] x, int sampleRate, double trimDb)
		{
			var threshold = LayerRenderer.Peak(x) * Loudness.Linear(trimDb);
			var last = x.Length - 1;
			while (last > 0 && Math.Abs(x[last]) < threshold)
				last--;
			var end = Math.Min(x.Length, last + 1 + (int)(0.01 * sampleRate));
			return x[..end];
		}

		private static void Fade(double[] x, int sampleRate)
		{
			var fadeIn = Math.Min(x.Length, Math.Max(1, (int)(FadeIn * sampleRate)));
			for (var i = 0; i < fadeIn; i++)
				x[i] *= 0.5 - 0.5 * Math.Cos(Math.PI * i / fadeIn);
			var fadeOut = Math.Min(x.Length, Math.Max(1, (int)Math.Min(FadeOutMax * sampleRate, x.Length * 0.15)));
			for (var i = 0; i < fadeOut; i++)
				x[x.Length - 1 - i] *= 0.5 - 0.5 * Math.Cos(Math.PI * i / fadeOut);
		}
	}
}
