using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// A camada física, sempre por baixo da magia: madeira, papel, couro, pano, fivelas e moedas. Estilizada,
	/// curta e sem grave sobrando.
	/// </summary>
	public static class Foley
	{
		/// <summary>Estalinho seco: a ponta de um clique.</summary>
		public static void Tick(Patch p, double at, double hz, double gain)
		{
			p.Noise(NoiseColor.White, Envelope.Perc(0.0002, 0.006, 4), at).Bandpass(hz, 1.4).Gain(gain);
			p.Modal(Modes.Wood, hz * 0.5, 0.025, at).Gain(gain * 0.5);
		}

		/// <summary>"Toc" de madeira: botões, tampas, a capa do livro.</summary>
		public static void Knock(Patch p, double at, double hz, double decay, double gain)
		{
			p.Modal(Modes.Wood, hz, decay, at).Gain(gain);
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.0005, 0.012), at).Bandpass(hz * 2.2, 1.2).Gain(gain * 0.3);
		}

		/// <summary>Baque abafado: a capa caindo, um carimbo, um passo pesado.</summary>
		public static void Thud(Patch p, double at, double hz, double gain)
		{
			p.Tone(Wave.Sine, hz, Envelope.Perc(0.002, 0.14), at).Sweep(1.8, 0.012).Gain(gain);
			p.Noise(NoiseColor.Brown, Envelope.Perc(0.002, 0.07), at).Lowpass(600).Gain(gain * 0.6);
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.001, 0.025), at).Bandpass(1400, 0.8).Gain(gain * 0.25);
		}

		/// <summary>Papel amassando de leve por <paramref name="length"/> segundos.</summary>
		public static void Paper(Patch p, double at, double length, double gain, double hz = 3200)
		{
			p.Crackle(700, 0.003, Envelope.Swell(length * 0.3, length * 0.7), at).Bandpass(hz, 0.9).Gain(gain);
			p.Noise(NoiseColor.Pink, Envelope.Swell(length * 0.4, length * 0.6), at).Bandpass(hz * 0.6, 0.7).Gain(gain * 0.3);
		}

		/// <summary>Uma página virando: o ar da folha, o roçar e a folha assentando.</summary>
		public static void PageFlip(Patch p, double at, double length, double gain)
		{
			var hz = p.Vary(2600, 0.15);
			p.Noise(NoiseColor.Pink, Envelope.Gust(length, 0.55), at).Bandpass(hz * 0.35, 0.8, hz).Gain(gain * 0.6);
			p.Crackle(p.Vary(900, 0.2), 0.0025, Envelope.Swell(length * 0.5, length * 0.5), at).Bandpass(hz, 0.8).Gain(gain * 0.8);
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.001, 0.04), at + length * 0.85).Bandpass(1800, 0.9).Gain(gain * 0.45);
		}

		/// <summary>Couro: um tapa abafado com o rangido curto da peça.</summary>
		public static void Leather(Patch p, double at, double gain)
		{
			p.Noise(NoiseColor.Brown, Envelope.Perc(0.004, 0.08, 2.5), at).Lowpass(700).Gain(gain);
			p.Crackle(160, 0.0015, Envelope.Perc(0.01, 0.12, 2), at).Bandpass(1100, 3).Gain(gain * 0.35);
		}

		/// <summary>Rangido de dobradiça ou de couro esticado.</summary>
		public static void Creak(Patch p, double at, double length, double hz, double gain)
		{
			p.Tone(Wave.Saw, p.Vary(55, 0.2), Envelope.Swell(length * 0.4, length * 0.6), at).Glide(p.Vary(85, 0.2)).Bandpass(hz, 5).Tremolo(p.Vary(9, 0.2), 0.6).Gain(gain);
		}

		/// <summary>Pano, capa, folha passando no ar: um sopro de ruído que anda de <paramref name="from"/> a <paramref name="to"/> Hz.</summary>
		public static void Swish(Patch p, double at, double length, double from, double to, double gain)
		{
			p.Noise(NoiseColor.Pink, Envelope.Gust(length), at).Bandpass(from, 1.1, to).Gain(gain);
		}

		/// <summary>Fivela, trava, aro de metal pequeno.</summary>
		public static void Clink(Patch p, double at, double hz, double gain)
		{
			p.Modal(Modes.Metal, hz, 0.12, at).Bright(0.75).Gain(gain);
		}

		public static void Coin(Patch p, double at, double hz, double gain)
		{
			p.Modal(Modes.Coin, hz, p.Vary(0.22, 0.2), at).Bright(0.8).Gain(gain);
			p.Noise(NoiseColor.White, Envelope.Perc(0.0002, 0.004), at).Bandpass(5200, 1.2).Gain(gain * 0.25);
		}

		/// <summary>A pena riscando o pergaminho por <paramref name="length"/> segundos.</summary>
		public static void Quill(Patch p, double at, double length, double gain)
		{
			p.Crackle(p.Vary(380, 0.2), 0.0015, Envelope.Swell(length * 0.3, length * 0.7), at).Bandpass(p.Vary(4200, 0.1), 1.8).Gain(gain);
			p.Noise(NoiseColor.White, Envelope.Swell(length * 0.3, length * 0.7), at).Bandpass(3000, 3, 4600).Gain(gain * 0.18);
		}
	}
}
