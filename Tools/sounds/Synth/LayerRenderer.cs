using System;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// Sintetiza uma camada: fonte → filtros → formantes → nível padrão → envelope e tremor → saturação →
	/// volume → (invertida).
	/// </summary>
	public static class LayerRenderer
	{
		/// <summary>O volume médio (RMS) de uma camada contínua antes do envelope.</summary>
		private const double ContinuousRms = 0.5;

		private const double Tau = 2 * Math.PI;

		public static double[] Render(Layer layer, int sampleRate)
		{
			var samples = new double[Math.Max(1, (int)Math.Ceiling(layer.Length * sampleRate))];
			var rng = new Rng(layer.Seed);
			switch (layer.Source)
			{
				case LayerSource.Tone: Tone(samples, layer, sampleRate, rng); break;
				case LayerSource.Noise: Noise(samples, layer.Color, rng); break;
				case LayerSource.Modal: Modal(samples, layer, sampleRate, rng); break;
				case LayerSource.Pluck: Pluck(samples, layer, sampleRate, rng); break;
				default: Crackle(samples, layer, sampleRate, rng); break;
			}
			foreach (var spec in layer.Filters)
				Filter(samples, spec, sampleRate);
			if (layer.VowelFrom is { } from)
				Formant(samples, from, layer.VowelTo ?? from, sampleRate);
			if (layer.Source is LayerSource.Tone or LayerSource.Noise)
				ScaleRms(samples, ContinuousRms);
			else
				ScalePeak(samples, 1);
			Shape(samples, layer, sampleRate);
			if (layer.DriveAmount > 0)
			{
				var norm = Math.Tanh(layer.DriveAmount);
				for (var i = 0; i < samples.Length; i++)
					samples[i] = Math.Tanh(layer.DriveAmount * samples[i]) / norm;
			}
			for (var i = 0; i < samples.Length; i++)
				samples[i] *= layer.Volume;
			if (layer.Reversed)
				Array.Reverse(samples);
			return samples;
		}

		/// <summary>Um filtro que anda: o corte é refeito a cada 16 amostras.</summary>
		public static void Filter(double[] samples, FilterSpec spec, int sampleRate)
		{
			var biquad = new Biquad();
			var last = Math.Max(1, samples.Length - 1);
			for (var i = 0; i < samples.Length; i++)
			{
				if ((i & 15) == 0)
					biquad.Set(spec.Kind, spec.Cutoff((double)i / last, (double)i / sampleRate), spec.Q, spec.GainDb, sampleRate);
				samples[i] = biquad.Process(samples[i]);
			}
		}

		private static void Tone(double[] samples, Layer layer, int sampleRate, Rng rng)
		{
			var voices = layer.Voices;
			var phases = new double[voices];
			var spread = new double[voices];
			for (var v = 0; v < voices; v++)
			{
				phases[v] = voices > 1 ? rng.Next() : 0;
				var cents = voices > 1 ? layer.DetuneCents * (2.0 * v / (voices - 1) - 1) : 0;
				spread[v] = Math.Pow(2, cents / 1200);
			}
			var modPhase = 0.0;
			var last = Math.Max(1, samples.Length - 1);
			var gain = 1 / Math.Sqrt(voices);
			for (var i = 0; i < samples.Length; i++)
			{
				var t = (double)i / sampleRate;
				var progress = (double)i / last;
				var hz = layer.FrequencyAt(t, progress);
				var offset = 0.0;
				if (layer.FmRatio > 0)
				{
					modPhase += hz * layer.FmRatio / sampleRate;
					modPhase -= Math.Floor(modPhase);
					var index = layer.FmIndex + (layer.FmIndexEnd - layer.FmIndex) * progress;
					offset = index * Math.Sin(Tau * modPhase) / Tau;
				}
				var sum = 0.0;
				for (var v = 0; v < voices; v++)
				{
					var step = hz * spread[v] / sampleRate;
					phases[v] += step;
					phases[v] -= Math.Floor(phases[v]);
					var phase = phases[v] + offset;
					phase -= Math.Floor(phase);
					sum += Oscillator(layer.Wave, phase, step);
				}
				samples[i] = sum * gain;
			}
		}

		private static double Oscillator(Wave wave, double phase, double step) => wave switch
		{
			Wave.Sine => Math.Sin(Tau * phase),
			Wave.Triangle => 1 - 4 * Math.Abs(phase - 0.5),
			Wave.Saw => 2 * phase - 1 - PolyBlep(phase, step),
			_ => (phase < 0.5 ? 1 : -1) + PolyBlep(phase, step) - PolyBlep((phase + 0.5) % 1, step),
		};

		private static double PolyBlep(double t, double dt)
		{
			if (dt <= 0)
				return 0;
			if (t < dt)
			{
				t /= dt;
				return t + t - t * t - 1;
			}
			if (t > 1 - dt)
			{
				t = (t - 1) / dt;
				return t * t + t + t + 1;
			}
			return 0;
		}

		private static void Noise(double[] samples, NoiseColor color, Rng rng)
		{
			double b0 = 0, b1 = 0, b2 = 0, brown = 0;
			for (var i = 0; i < samples.Length; i++)
			{
				var white = rng.Signed();
				switch (color)
				{
					case NoiseColor.White:
						samples[i] = white;
						break;
					case NoiseColor.Pink:
						b0 = 0.99765 * b0 + white * 0.0990460;
						b1 = 0.96300 * b1 + white * 0.2965164;
						b2 = 0.57000 * b2 + white * 1.0526913;
						samples[i] = b0 + b1 + b2 + white * 0.1848;
						break;
					default:
						brown = (brown + 0.02 * white) / 1.02;
						samples[i] = brown;
						break;
				}
			}
			if (color == NoiseColor.Brown)
				RemoveMean(samples);
		}

		private static void Modal(double[] samples, Layer layer, int sampleRate, Rng rng)
		{
			var modes = layer.Modes!;
			var count = modes.Ratios.Count;
			var amp = new double[count];
			var fall = new double[count];
			var detune = new double[count];
			var phase = new double[count];
			var total = 0.0;
			for (var k = 0; k < count; k++)
			{
				amp[k] = modes.Gains[k] * Math.Pow(layer.Brightness, k);
				total += amp[k];
			}
			var decay = Math.Max(0.005, layer.Length);
			for (var k = 0; k < count; k++)
			{
				amp[k] /= total;
				fall[k] = Math.Exp(-6.9 / (modes.Decays[k] * decay * sampleRate));
				detune[k] = 1 + (k == 0 ? 0 : rng.Signed() * 0.002);
			}
			var last = Math.Max(1, samples.Length - 1);
			var nyquist = sampleRate * 0.45;
			for (var i = 0; i < samples.Length; i++)
			{
				var hz = layer.FrequencyAt((double)i / sampleRate, (double)i / last);
				var sum = 0.0;
				for (var k = 0; k < count; k++)
				{
					var partial = hz * modes.Ratios[k] * detune[k];
					if (partial < nyquist)
					{
						phase[k] += partial / sampleRate;
						phase[k] -= Math.Floor(phase[k]);
						sum += amp[k] * Math.Sin(Tau * phase[k]);
					}
					amp[k] *= fall[k];
				}
				samples[i] = sum;
			}
		}

		/// <summary>Karplus-Strong: um sopro de ruído circulando numa linha do tamanho de um período, perdendo brilho a cada volta.</summary>
		private static void Pluck(double[] samples, Layer layer, int sampleRate, Rng rng)
		{
			var size = Math.Max(2, (int)Math.Round(sampleRate / layer.Hz - 0.5));
			var line = new double[size];
			var smooth = 0.0;
			var pick = Math.Clamp(0.15 + 0.8 * layer.Brightness * 0.6, 0.05, 1);
			for (var k = 0; k < size; k++)
			{
				smooth += pick * (rng.Signed() - smooth);
				line[k] = smooth;
			}
			RemoveMean(line);
			var loss = Math.Exp(-6.9 / (Math.Max(0.02, layer.Length) * layer.Hz));
			var index = 0;
			for (var i = 0; i < samples.Length; i++)
			{
				var current = line[index];
				var next = line[(index + 1) % size];
				line[index] = loss * (0.5 * current + 0.5 * next);
				samples[i] = current;
				index = (index + 1) % size;
			}
		}

		/// <summary>Estalos de ruído que nascem ao acaso (<see cref="Layer.Density"/> por segundo) e somem em <see cref="Layer.Grain"/> segundos.</summary>
		private static void Crackle(double[] samples, Layer layer, int sampleRate, Rng rng)
		{
			var keep = Math.Exp(-1 / (Math.Max(0.0002, layer.Grain) * sampleRate));
			var last = Math.Max(1, samples.Length - 1);
			var level = 0.0;
			for (var i = 0; i < samples.Length; i++)
			{
				var density = layer.DensityEnd > 0 ? layer.Density + (layer.DensityEnd - layer.Density) * i / last : layer.Density;
				if (rng.Next() < density / sampleRate)
				{
					var strength = rng.Range(0.2, 1);
					level += strength * strength;
				}
				level *= keep;
				samples[i] = level * rng.Signed();
			}
		}

		/// <summary>Três passa-faixas em paralelo nas formantes da vogal, indo de uma vogal à outra; o volume médio fica o mesmo.</summary>
		private static void Formant(double[] samples, Vowel from, Vowel to, int sampleRate)
		{
			var before = Rms(samples);
			var a = Formants.Of(from);
			var b = Formants.Of(to);
			var bands = new[] { new Biquad(), new Biquad(), new Biquad() };
			var last = Math.Max(1, samples.Length - 1);
			for (var i = 0; i < samples.Length; i++)
			{
				if ((i & 31) == 0)
				{
					var x = (double)i / last;
					for (var k = 0; k < 3; k++)
					{
						var hz = a[k] * Math.Pow(b[k] / a[k], x);
						bands[k].Set(FilterKind.Bandpass, hz, hz / Formants.Bandwidths[k], 0, sampleRate);
					}
				}
				var input = samples[i];
				var sum = 0.0;
				for (var k = 0; k < 3; k++)
					sum += Formants.Gains[k] * bands[k].Process(input);
				samples[i] = sum;
			}
			var after = Rms(samples);
			if (after > 1e-12)
				for (var i = 0; i < samples.Length; i++)
					samples[i] *= before / after;
		}

		/// <summary>Envelope e tremor (o tremor começa no volume cheio).</summary>
		private static void Shape(double[] samples, Layer layer, int sampleRate)
		{
			var tremolo = 0.0;
			var last = Math.Max(1, samples.Length - 1);
			for (var i = 0; i < samples.Length; i++)
			{
				var t = (double)i / sampleRate;
				var amp = layer.Envelope.At(t);
				if (layer.TremoloDepth > 0)
				{
					var rate = layer.TremoloRate + (layer.TremoloRateEnd - layer.TremoloRate) * i / last;
					tremolo += rate / sampleRate;
					amp *= 1 - layer.TremoloDepth * (0.5 - 0.5 * Math.Cos(Tau * tremolo));
				}
				samples[i] *= amp;
			}
		}

		public static double Rms(double[] samples)
		{
			var sum = 0.0;
			foreach (var s in samples)
				sum += s * s;
			return Math.Sqrt(sum / Math.Max(1, samples.Length));
		}

		public static double Peak(double[] samples)
		{
			var peak = 0.0;
			foreach (var s in samples)
				peak = Math.Max(peak, Math.Abs(s));
			return peak;
		}

		private static void ScaleRms(double[] samples, double target)
		{
			var rms = Rms(samples);
			if (rms > 1e-12)
				for (var i = 0; i < samples.Length; i++)
					samples[i] *= target / rms;
		}

		private static void ScalePeak(double[] samples, double target)
		{
			var peak = Peak(samples);
			if (peak > 1e-12)
				for (var i = 0; i < samples.Length; i++)
					samples[i] *= target / peak;
		}

		private static void RemoveMean(double[] samples)
		{
			var mean = 0.0;
			foreach (var s in samples)
				mean += s;
			mean /= Math.Max(1, samples.Length);
			for (var i = 0; i < samples.Length; i++)
				samples[i] -= mean;
		}
	}
}
