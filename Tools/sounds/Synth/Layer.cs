using System;
using System.Collections.Generic;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// Uma camada de um efeito: a fonte (<see cref="LayerSource"/>), quando entra (<see cref="At"/>), o
	/// envelope e o que se faz com ela, encadeado: <c>p.Tone(Wave.Sine, 880, env).Glide(1320).Lowpass(3000).Gain(0.4)</c>.
	/// Antes do envelope, uma camada contínua (oscilador, ruído) sai com o mesmo volume médio, e uma de golpe
	/// (corpo, corda, estalos) com o mesmo pico: o <see cref="Gain"/> é sempre relativo às outras camadas.
	/// </summary>
	public sealed class Layer
	{
		internal Layer(LayerSource source, double at, Envelope envelope, ulong seed)
		{
			Source = source;
			At = Math.Max(0, at);
			Envelope = envelope;
			Seed = seed;
		}

		internal LayerSource Source { get; }
		internal double At { get; }
		internal Envelope Envelope { get; }
		internal ulong Seed { get; }

		internal Wave Wave { get; set; }
		internal NoiseColor Color { get; set; }
		internal Modes? Modes { get; set; }
		internal double Hz { get; set; }
		internal double HzEnd { get; private set; }
		internal double GlideCurve { get; private set; } = 1;
		internal double GlideTime { get; private set; }
		internal double SweepRatio { get; private set; } = 1;
		internal double SweepTime { get; private set; } = 0.02;
		internal double VibratoRate { get; private set; }
		internal double VibratoSemitones { get; private set; }
		internal double VibratoDelay { get; private set; }
		internal double FmRatio { get; private set; }
		internal double FmIndex { get; private set; }
		internal double FmIndexEnd { get; private set; }
		internal int Voices { get; private set; } = 1;
		internal double DetuneCents { get; private set; }
		internal double TremoloRate { get; private set; }
		internal double TremoloRateEnd { get; private set; }
		internal double TremoloDepth { get; private set; }
		internal List<FilterSpec> Filters { get; } = new();
		internal Vowel? VowelFrom { get; private set; }
		internal Vowel? VowelTo { get; private set; }
		internal double DriveAmount { get; private set; }
		internal double Volume { get; private set; } = 1;
		internal double Brightness { get; private set; } = 1;
		internal double Density { get; set; }
		internal double DensityEnd { get; private set; }
		internal double Grain { get; set; }
		internal bool Reversed { get; private set; }

		/// <summary>A duração da camada, em segundos.</summary>
		internal double Length => Envelope.Length;

		/// <summary>Volume relativo às outras camadas do efeito.</summary>
		public Layer Gain(double gain)
		{
			Volume = gain;
			return this;
		}

		/// <summary>Glissando até <paramref name="toHz"/>, em <paramref name="time"/> segundos (0: a camada inteira). <paramref name="curve"/> abaixo de 1 corre no começo.</summary>
		public Layer Glide(double toHz, double curve = 1, double time = 0)
		{
			HzEnd = toHz;
			GlideCurve = curve;
			GlideTime = time;
			return this;
		}

		/// <summary>Ataque que começa <paramref name="ratio"/> vezes mais agudo e cai para a nota em ~<paramref name="time"/> segundos: o corpo de um impacto.</summary>
		public Layer Sweep(double ratio, double time)
		{
			SweepRatio = ratio;
			SweepTime = time;
			return this;
		}

		public Layer Vibrato(double rate, double semitones, double delay = 0)
		{
			VibratoRate = rate;
			VibratoSemitones = semitones;
			VibratoDelay = delay;
			return this;
		}

		/// <summary>Modulação de fase por um seno na razão <paramref name="ratio"/> da nota; o índice vai de <paramref name="index"/> a <paramref name="indexEnd"/> (vidro, sinos, brilhos).</summary>
		public Layer Fm(double ratio, double index, double indexEnd = double.NaN)
		{
			FmRatio = ratio;
			FmIndex = index;
			FmIndexEnd = double.IsNaN(indexEnd) ? index : indexEnd;
			return this;
		}

		/// <summary>Várias vozes espalhadas em ± <paramref name="cents"/>: tapetes e coros mais largos.</summary>
		public Layer Unison(int voices, double cents)
		{
			Voices = Math.Max(1, voices);
			DetuneCents = cents;
			return this;
		}

		/// <summary>Tremor de volume; a velocidade pode ir de <paramref name="rate"/> a <paramref name="rateEnd"/> (energia acelerando).</summary>
		public Layer Tremolo(double rate, double depth, double rateEnd = double.NaN)
		{
			TremoloRate = rate;
			TremoloRateEnd = double.IsNaN(rateEnd) ? rate : rateEnd;
			TremoloDepth = Math.Clamp(depth, 0, 1);
			return this;
		}

		public Layer Lowpass(double hz, double toHz = 0, double q = 0.707) => Filter(new FilterSpec(FilterKind.Lowpass, hz, toHz, q));

		public Layer Highpass(double hz, double toHz = 0, double q = 0.707) => Filter(new FilterSpec(FilterKind.Highpass, hz, toHz, q));

		public Layer Bandpass(double hz, double q, double toHz = 0) => Filter(new FilterSpec(FilterKind.Bandpass, hz, toHz, q));

		public Layer Peak(double hz, double db, double q = 1) => Filter(new FilterSpec(FilterKind.Peak, hz, 0, q, db));

		public Layer Filter(FilterSpec spec)
		{
			Filters.Add(spec);
			return this;
		}

		/// <summary>O último filtro anda com a curva <paramref name="curve"/> (abaixo de 1 corre no começo).</summary>
		public Layer Curve(double curve)
		{
			Last().Curve = curve;
			return this;
		}

		/// <summary>O último filtro balança <paramref name="octaves"/> oitavas a <paramref name="rate"/> Hz.</summary>
		public Layer Wobble(double rate, double octaves, double phase = 0)
		{
			var last = Last();
			last.LfoRate = rate;
			last.LfoOctaves = octaves;
			last.LfoPhase = phase;
			return this;
		}

		/// <summary>Filtro de formantes: a camada "canta" a vogal, indo de <paramref name="from"/> a <paramref name="to"/>.</summary>
		public Layer Vowel(Vowel from, Vowel? to = null)
		{
			VowelFrom = from;
			VowelTo = to ?? from;
			return this;
		}

		/// <summary>Saturação suave (tanh); 1 mal se nota, 4 já engrossa bastante.</summary>
		public Layer Drive(double amount)
		{
			DriveAmount = amount;
			return this;
		}

		/// <summary>Corpo e corda: abaixo de 1 escurece os parciais de cima; na corda, o quanto o dedo é duro.</summary>
		public Layer Bright(double brightness)
		{
			Brightness = brightness;
			return this;
		}

		/// <summary>Estalos: a densidade vai até <paramref name="perSecond"/> no fim da camada.</summary>
		public Layer DensityTo(double perSecond)
		{
			DensityEnd = perSecond;
			return this;
		}

		/// <summary>Toca a camada de trás para frente (o sino que cresce até o golpe).</summary>
		public Layer Reverse()
		{
			Reversed = true;
			return this;
		}

		/// <summary>A frequência no segundo <paramref name="t"/>, no ponto <paramref name="progress"/> da camada.</summary>
		internal double FrequencyAt(double t, double progress)
		{
			var hz = Hz;
			if (HzEnd > 0 && HzEnd != Hz)
			{
				var x = GlideTime > 0 ? Math.Min(1, t / GlideTime) : progress;
				hz = Hz * Math.Pow(HzEnd / Hz, Math.Pow(x, GlideCurve));
			}
			if (SweepRatio != 1)
				hz *= 1 + (SweepRatio - 1) * Math.Exp(-t / SweepTime);
			if (VibratoSemitones > 0)
			{
				var ramp = VibratoDelay > 0 ? Math.Clamp((t - VibratoDelay) / 0.15, 0, 1) : 1;
				hz *= Math.Pow(2, VibratoSemitones / 12 * ramp * Math.Sin(2 * Math.PI * VibratoRate * t));
			}
			return hz;
		}

		private FilterSpec Last()
		{
			if (Filters.Count == 0)
				throw new InvalidOperationException("Curve e Wobble mexem no último filtro: ponha um filtro antes.");
			return Filters[^1];
		}
	}
}
