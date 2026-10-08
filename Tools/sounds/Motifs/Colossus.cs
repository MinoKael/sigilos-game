using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// A voz dos chefes, mais grave e mais larga que o resto: o rosnado (serras graves cantando "o" e "a"),
	/// o tambor, o gongo e o batimento. O grave fica no corpo do som, nunca num ronco sozinho.
	/// </summary>
	public static class Colossus
	{
		/// <summary>Rosnado de <paramref name="length"/> segundos em <paramref name="hz"/>, indo a <paramref name="toHz"/> (0: parado).</summary>
		public static void Growl(Patch p, double at, double length, double hz, double toHz, double gain)
		{
			var envelope = Envelope.Swell(length * 0.3, length * 0.7, 1.5);
			var voice = p.Tone(Wave.Saw, hz, envelope, at).Unison(3, 18).Fm(0.5, 1.4).Vowel(Vowel.O, Vowel.A).Tremolo(p.Vary(17, 0.15), 0.45).Drive(2.5).Gain(gain);
			if (toHz > 0)
				voice.Glide(toHz, 0.8);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(hz * 6, 1.8).Tremolo(p.Vary(23, 0.15), 0.6).Gain(gain * 0.3);
		}

		/// <summary>Tambor grande, couro esticado: o passo do chefe.</summary>
		public static void Drum(Patch p, double at, double gain)
		{
			p.Tone(Wave.Sine, p.Vary(72, 0.05), Envelope.Perc(0.002, 0.45, 2.5), at).Sweep(2.2, 0.02).Drive(1.2).Gain(gain);
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.001, 0.06), at).Bandpass(900, 0.9).Gain(gain * 0.45);
			p.Modal(Modes.Wood, 190, 0.2, at).Bright(0.5).Gain(gain * 0.3);
		}

		/// <summary>Gongo de bronze na nota <paramref name="midi"/>.</summary>
		public static void Gong(Patch p, int midi, double at, double decay, double gain)
		{
			p.Modal(Modes.Bowl, Notes.Hz(midi), decay, at, attack: 0.004).Gain(gain);
			p.Modal(Modes.Metal, Notes.Hz(midi) * 2.01, decay * 0.5, at).Bright(0.6).Gain(gain * 0.35);
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.08), at).Bandpass(1200, 0.8).Gain(gain * 0.25);
		}

		/// <summary>Um batimento: dois tambores abafados.</summary>
		public static void Heartbeat(Patch p, double at, double gain)
		{
			p.Tone(Wave.Sine, 62, Envelope.Perc(0.004, 0.18, 2.5), at).Sweep(1.6, 0.02).Lowpass(500).Gain(gain);
			p.Tone(Wave.Sine, 58, Envelope.Perc(0.004, 0.22, 2.5), at + 0.2).Sweep(1.6, 0.02).Lowpass(500).Gain(gain * 0.75);
		}
	}
}
