using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// O corpo do combate, estilizado e sem violência realista: o ar do golpe, o impacto (médio na frente,
	/// grave controlado), o corte, a explosão e o cristal estilhaçando.
	/// </summary>
	public static class Strike
	{
		/// <summary>O ar do golpe: ruído rosa andando de <paramref name="from"/> a <paramref name="to"/> Hz.</summary>
		public static Layer Whoosh(Patch p, double at, double length, double from, double to, double gain, double q = 1.3)
		{
			return p.Noise(NoiseColor.Pink, Envelope.Gust(length, 0.65), at).Bandpass(from, q, to).Gain(gain);
		}

		/// <summary>Impacto de peso <paramref name="weight"/> (0 leve, 1 muito pesado): corpo, estalo e, nos pesados, o ronco.</summary>
		public static void Impact(Patch p, double at, double weight, double gain)
		{
			var body = 165 - 70 * weight;
			p.Tone(Wave.Sine, p.Vary(body, 0.06), Envelope.Perc(0.0008, 0.07 + 0.18 * weight), at).Sweep(2.6, 0.012 + 0.01 * weight).Drive(0.5 + 1.5 * weight).Gain(gain * (0.7 + 0.3 * weight));
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.0005, 0.03 + 0.05 * weight, 3.5), at).Bandpass(p.Vary(1300 - 500 * weight, 0.1), 0.9).Gain(gain * 0.8);
			p.Noise(NoiseColor.White, Envelope.Perc(0.0002, 0.006, 4), at).Bandpass(3500, 1.2).Gain(gain * 0.3);
			if (weight > 0.5)
				p.Noise(NoiseColor.Brown, Envelope.Perc(0.002, 0.2 * weight, 2.5), at).Lowpass(400).Gain(gain * 0.5 * weight);
		}

		/// <summary>Corte: o sopro rápido e um "shing" metálico discreto.</summary>
		public static void Slash(Patch p, double at, double gain)
		{
			var length = p.Vary(0.14, 0.15);
			Whoosh(p, at, length, p.Vary(900, 0.1), p.Vary(4200, 0.1), gain, 1.6);
			p.Modal(Modes.Metal, p.Vary(2400, 0.08), 0.18, at + length * 0.7).Bright(0.6).Gain(gain * 0.22);
			p.Noise(NoiseColor.White, Envelope.Perc(0.0005, 0.05), at + length * 0.7).Bandpass(4800, 2).Gain(gain * 0.25);
		}

		/// <summary>Explosão de tamanho <paramref name="size"/> (0 a 1): estouro, corpo caindo de tom e o crepitar do que sobra.</summary>
		public static void Boom(Patch p, double at, double size, double gain)
		{
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.25 + 0.5 * size, 2.5), at).Lowpass(3500, 180).Curve(0.6).Drive(1.5).Gain(gain);
			p.Tone(Wave.Sine, 70 + 20 * (1 - size), Envelope.Perc(0.002, 0.3 + 0.4 * size), at).Sweep(3, 0.03).Gain(gain * 0.75);
			p.Crackle(60, 0.002, Envelope.Perc(0.01, 0.5 + 0.6 * size, 2), at + 0.03).Bandpass(2200, 0.8).Gain(gain * 0.4);
		}

		/// <summary>Cristal estilhaçando: muitos cacos curtos em volta de <paramref name="hz"/>.</summary>
		public static void Shatter(Patch p, double at, double gain, double hz = 2600)
		{
			for (var i = 0; i < 9; i++)
				p.Modal(Modes.Glass, hz * p.Rng.Range(0.6, 2.2), p.Rng.Range(0.08, 0.3), at + p.Rng.Range(0, 0.07)).Gain(gain * p.Rng.Range(0.3, 0.8));
			p.Noise(NoiseColor.White, Envelope.Perc(0.0005, 0.08), at).Bandpass(4500, 0.7).Gain(gain * 0.5);
		}
	}
}
