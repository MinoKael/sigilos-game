using System;
using System.Collections.Generic;

namespace Sigilos.Sounds.Synth
{
	/// <summary>
	/// A mesa de uma variação de um efeito: a receita põe camadas (<see cref="Tone"/>, <see cref="Noise"/>,
	/// <see cref="Modal"/>, <see cref="Pluck"/>, <see cref="Crackle"/>) e o espaço do efeito inteiro
	/// (<see cref="Reverb"/>, <see cref="Echo"/>, filtros). <see cref="Rng"/> e <see cref="Vary"/> são da
	/// variação: cada uma sai um pouco diferente, e sempre igual para a mesma semente.
	/// </summary>
	public sealed class Patch
	{
		private readonly List<Layer> _layers = new();
		private readonly List<FilterSpec> _filters = new();
		private double _drive;
		private double _reverbMix, _reverbSeconds, _reverbDamp, _reverbPredelay;
		private double _echoTime, _echoFeedback, _echoMix;

		public Patch(Rng rng, int variation, int sampleRate)
		{
			Rng = rng;
			Variation = variation;
			SampleRate = sampleRate;
		}

		public Rng Rng { get; }

		/// <summary>Qual variação é esta (0, 1, 2...): para trocar a nota de propósito, e não só ao acaso.</summary>
		public int Variation { get; }

		public int SampleRate { get; }

		/// <summary><paramref name="value"/> mexido em até ± <paramref name="spread"/> nesta variação.</summary>
		public double Vary(double value, double spread) => Rng.Vary(value, spread);

		public Layer Tone(Wave wave, double hz, Envelope envelope, double at = 0)
		{
			var layer = Add(LayerSource.Tone, at, envelope);
			layer.Wave = wave;
			layer.Hz = hz;
			return layer;
		}

		public Layer Noise(NoiseColor color, Envelope envelope, double at = 0)
		{
			var layer = Add(LayerSource.Noise, at, envelope);
			layer.Color = color;
			return layer;
		}

		/// <summary>Um corpo que vibra na nota <paramref name="hz"/>; a fundamental cai 60 dB em <paramref name="decay"/> segundos.</summary>
		public Layer Modal(Modes modes, double hz, double decay, double at = 0, double attack = 0.0008)
		{
			var layer = Add(LayerSource.Modal, at, Envelope.Hit(attack, Math.Max(0, decay - attack), 0.004, 1));
			layer.Modes = modes;
			layer.Hz = hz;
			return layer;
		}

		/// <summary>Uma corda dedilhada em <paramref name="hz"/> que some em <paramref name="decay"/> segundos.</summary>
		public Layer Pluck(double hz, double decay, double at = 0)
		{
			var layer = Add(LayerSource.Pluck, at, Envelope.Hit(0.0005, decay, 0.004, 1));
			layer.Hz = hz;
			return layer;
		}

		/// <summary><paramref name="perSecond"/> estalos por segundo, cada um sumindo em <paramref name="grain"/> segundos.</summary>
		public Layer Crackle(double perSecond, double grain, Envelope envelope, double at = 0)
		{
			var layer = Add(LayerSource.Crackle, at, envelope);
			layer.Density = perSecond;
			layer.Grain = grain;
			return layer;
		}

		/// <summary>Sala pequena: <paramref name="mix"/> é o quanto do eco entra (0,2 já é bastante), <paramref name="seconds"/> o tempo da cauda.</summary>
		public Patch Reverb(double mix, double seconds, double damp = 0.45, double predelay = 0.012)
		{
			_reverbMix = mix;
			_reverbSeconds = seconds;
			_reverbDamp = damp;
			_reverbPredelay = predelay;
			return this;
		}

		public Patch Echo(double time, double feedback, double mix)
		{
			_echoTime = time;
			_echoFeedback = feedback;
			_echoMix = mix;
			return this;
		}

		public Patch Drive(double amount)
		{
			_drive = amount;
			return this;
		}

		public Patch Lowpass(double hz)
		{
			_filters.Add(new FilterSpec(FilterKind.Lowpass, hz, 0, 0.707));
			return this;
		}

		public Patch Highpass(double hz)
		{
			_filters.Add(new FilterSpec(FilterKind.Highpass, hz, 0, 0.707));
			return this;
		}

		/// <summary>Soma as camadas e passa pelo espaço do efeito.</summary>
		public double[] Render()
		{
			if (_layers.Count == 0)
				throw new InvalidOperationException("Efeito sem camadas.");
			var rendered = new List<(int Offset, double[] Samples)>();
			var length = 0;
			foreach (var layer in _layers)
			{
				var samples = LayerRenderer.Render(layer, SampleRate);
				var offset = (int)Math.Round(layer.At * SampleRate);
				rendered.Add((offset, samples));
				length = Math.Max(length, offset + samples.Length);
			}
			var mix = new double[length];
			foreach (var (offset, samples) in rendered)
				for (var i = 0; i < samples.Length; i++)
					mix[offset + i] += samples[i];
			foreach (var spec in _filters)
				LayerRenderer.Filter(mix, spec, SampleRate);
			if (_drive > 0)
			{
				var peak = Math.Max(1e-9, LayerRenderer.Peak(mix));
				var norm = Math.Tanh(_drive);
				for (var i = 0; i < mix.Length; i++)
					mix[i] = Math.Tanh(_drive * mix[i] / peak) / norm * peak;
			}
			if (_echoMix > 0)
				mix = Space.Echo(mix, SampleRate, _echoTime, _echoFeedback, _echoMix);
			if (_reverbMix > 0)
				mix = Space.Reverb(mix, SampleRate, _reverbMix, _reverbSeconds, _reverbDamp, _reverbPredelay);
			return mix;
		}

		private Layer Add(LayerSource source, double at, Envelope envelope)
		{
			var layer = new Layer(source, at, envelope, Rng.NextULong());
			_layers.Add(layer);
			return layer;
		}
	}
}
