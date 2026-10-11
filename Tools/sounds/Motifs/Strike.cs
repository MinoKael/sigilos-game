using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// O corpo do combate, macio: um golpe é feltro com uma nota de madeira na escala, não estalo nem
	/// estouro. Peso maior é mais grave e mais longo, nunca mais agudo nem mais alto.
	/// </summary>
	public static class Strike
	{
		/// <summary>Golpe de peso <paramref name="weight"/> (0 leve, 1 muito pesado): o feltro, o contato abafado e a nota <paramref name="midi"/>.</summary>
		public static void Hit(Patch p, double at, double weight, int midi, double gain)
		{
			Foley.Felt(p, at, p.Vary(140 - 55 * weight, 0.05), 0.09 + 0.13 * weight, gain);
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.035 + 0.04 * weight), at).Lowpass(1500 - 600 * weight).Gain(gain * 0.45);
			p.Modal(Modes.Marimba, Notes.Hz(midi), 0.18 + 0.12 * weight, at, 0.002).Bright(0.6).Lowpass(3000).Gain(gain * 0.4);
		}

		/// <summary>O ar do golpe: ruído rosa andando de <paramref name="from"/> a <paramref name="to"/> Hz.</summary>
		public static Layer Whoosh(Patch p, double at, double length, double from, double to, double gain, double q = 1.3)
		{
			return p.Noise(NoiseColor.Pink, Envelope.Gust(length, 0.65), at).Bandpass(from, q, to).Gain(gain);
		}

		/// <summary>Florada grave de tamanho <paramref name="size"/> (0 a 1): o ar abrindo e o corpo descendo, sem estouro.</summary>
		public static void Bloom(Patch p, double at, double size, double gain)
		{
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.006, 0.2 + 0.35 * size, 2.5), at).Lowpass(1400, 160).Gain(gain);
			p.Tone(Wave.Sine, 75 + 15 * (1 - size), Envelope.Perc(0.004, 0.25 + 0.3 * size), at).Sweep(1.8, 0.03).Gain(gain * 0.7);
		}
	}
}
