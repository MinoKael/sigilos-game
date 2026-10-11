using System;

namespace Sigilos.Sounds.Synth
{
	/// <summary>Filtro de segunda ordem (forma direta II transposta), que aceita trocar o corte no meio do som.</summary>
	public sealed class Biquad
	{
		private double _b0, _b1, _b2, _a1, _a2;
		private double _z1, _z2;

		public static Biquad Of(FilterKind kind, double hz, double q, int sampleRate, double gainDb = 0)
		{
			var filter = new Biquad();
			filter.Set(kind, hz, q, gainDb, sampleRate);
			return filter;
		}

		public void Set(FilterKind kind, double hz, double q, double gainDb, int sampleRate)
		{
			hz = Math.Clamp(hz, 10, sampleRate * 0.45);
			q = Math.Max(0.05, q);
			var w0 = 2 * Math.PI * hz / sampleRate;
			var cos = Math.Cos(w0);
			var alpha = Math.Sin(w0) / (2 * q);
			var a = Math.Pow(10, gainDb / 40);
			double b0, b1, b2, a0, a1, a2;
			switch (kind)
			{
				case FilterKind.Lowpass:
					b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = b0;
					a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
					break;
				case FilterKind.Highpass:
					b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = b0;
					a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
					break;
				case FilterKind.Bandpass:
					b0 = alpha; b1 = 0; b2 = -alpha;
					a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
					break;
				case FilterKind.Notch:
					b0 = 1; b1 = -2 * cos; b2 = 1;
					a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
					break;
				case FilterKind.Peak:
					b0 = 1 + alpha * a; b1 = -2 * cos; b2 = 1 - alpha * a;
					a0 = 1 + alpha / a; a1 = -2 * cos; a2 = 1 - alpha / a;
					break;
				case FilterKind.LowShelf:
				{
					var s = 2 * Math.Sqrt(a) * alpha;
					b0 = a * ((a + 1) - (a - 1) * cos + s); b1 = 2 * a * ((a - 1) - (a + 1) * cos); b2 = a * ((a + 1) - (a - 1) * cos - s);
					a0 = (a + 1) + (a - 1) * cos + s; a1 = -2 * ((a - 1) + (a + 1) * cos); a2 = (a + 1) + (a - 1) * cos - s;
					break;
				}
				default:
				{
					var s = 2 * Math.Sqrt(a) * alpha;
					b0 = a * ((a + 1) + (a - 1) * cos + s); b1 = -2 * a * ((a - 1) + (a + 1) * cos); b2 = a * ((a + 1) + (a - 1) * cos - s);
					a0 = (a + 1) - (a - 1) * cos + s; a1 = 2 * ((a - 1) - (a + 1) * cos); a2 = (a + 1) - (a - 1) * cos - s;
					break;
				}
			}
			_b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
		}

		public double Process(double x)
		{
			var y = _b0 * x + _z1;
			_z1 = _b1 * x - _a1 * y + _z2;
			_z2 = _b2 * x - _a2 * y;
			return y;
		}

		public void Run(double[] samples)
		{
			for (var i = 0; i < samples.Length; i++)
				samples[i] = Process(samples[i]);
		}
	}
}
