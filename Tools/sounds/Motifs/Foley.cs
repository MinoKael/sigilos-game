using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// A camada física, sempre por baixo da magia e sempre macia: madeira, papel, couro, pano, feltro e
	/// moedas. Estilizada, curta e sem grave nem agudo sobrando.
	/// </summary>
	public static class Foley
	{
		/// <summary>Estalinho seco: a ponta de um clique.</summary>
		public static void Tick(Patch p, double at, double hz, double gain)
		{
			p.Noise(NoiseColor.White, Envelope.Perc(0.0002, 0.006, 4), at).Bandpass(hz, 1.4).Gain(gain);
			p.Modal(Modes.Wood, hz * 0.5, 0.025, at).Gain(gain * 0.5);
		}

		/// <summary>"Toc" de madeira: botões, abas, peças.</summary>
		public static void Knock(Patch p, double at, double hz, double decay, double gain)
		{
			p.Modal(Modes.Wood, hz, decay, at).Gain(gain);
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.0005, 0.012), at).Bandpass(hz * 2.2, 1.2).Gain(gain * 0.3);
		}

		/// <summary>Baque de feltro: um corpo grave que cai um pouco de tom, sem estalo. O golpe, o passo, a capa fechando.</summary>
		public static void Felt(Patch p, double at, double hz, double length, double gain)
		{
			p.Tone(Wave.Sine, hz, Envelope.Perc(0.003, length), at).Sweep(1.5, 0.014).Gain(gain);
			p.Noise(NoiseColor.Brown, Envelope.Perc(0.003, length * 0.5), at).Lowpass(520).Gain(gain * 0.45);
		}

		/// <summary>Uma página virando, macia: o ar da folha e um roçar leve, sem o estalido do papel.</summary>
		public static void PageFlip(Patch p, double at, double length, double gain)
		{
			var hz = p.Vary(1700, 0.15);
			p.Noise(NoiseColor.Pink, Envelope.Gust(length, 0.55), at).Bandpass(hz * 0.4, 0.8, hz).Gain(gain * 0.7);
			p.Crackle(p.Vary(260, 0.2), 0.002, Envelope.Swell(length * 0.5, length * 0.5), at).Bandpass(hz * 1.3, 0.8).Lowpass(3500).Gain(gain * 0.35);
		}

		/// <summary>Couro: um tapa abafado com o rangido curto da peça.</summary>
		public static void Leather(Patch p, double at, double gain)
		{
			p.Noise(NoiseColor.Brown, Envelope.Perc(0.004, 0.08, 2.5), at).Lowpass(700).Gain(gain);
			p.Crackle(160, 0.0015, Envelope.Perc(0.01, 0.12, 2), at).Bandpass(1100, 3).Gain(gain * 0.35);
		}

		/// <summary>Pano, capa, folha passando no ar: um sopro de ruído que anda de <paramref name="from"/> a <paramref name="to"/> Hz.</summary>
		public static void Swish(Patch p, double at, double length, double from, double to, double gain)
		{
			p.Noise(NoiseColor.Pink, Envelope.Gust(length), at).Bandpass(from, 1.1, to).Gain(gain);
		}

		/// <summary>Moeda de ouro macia: o corpo da moeda, sem o brilho que fura.</summary>
		public static void Coin(Patch p, double at, double hz, double gain)
		{
			p.Modal(Modes.Coin, hz, p.Vary(0.22, 0.2), at, 0.0015).Bright(0.55).Lowpass(4500).Gain(gain);
		}
	}
}
