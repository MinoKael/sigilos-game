using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// A textura de cada elemento, que o reconhece sem depender do volume: Fogo é um sopro morno que tremula,
	/// Água borbulha, Vento é ar passando com um assobio baixo, Luz soa em sinos, Trevas numa sombra grave
	/// cantando "u" em quinta. Tudo abafado em cima.
	/// </summary>
	public static class Elemental
	{
		/// <summary>Chama por <paramref name="length"/> segundos: o sopro morno tremulando e umas brasas ao fundo.</summary>
		public static void Flame(Patch p, double at, double length, double gain)
		{
			var envelope = Envelope.Swell(length * 0.3, length * 0.7);
			p.Noise(NoiseColor.Brown, envelope, at).Lowpass(800, 0, 0.9).Wobble(p.Vary(7, 0.2), 0.4).Gain(gain);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(1300, 0.8).Wobble(9, 0.3).Tremolo(11, 0.35).Gain(gain * 0.25);
			p.Crackle(p.Vary(18, 0.2), 0.0015, envelope, at).Bandpass(1800, 0.8).Lowpass(2600).Gain(gain * 0.18);
		}

		/// <summary>Uma bolha: seno curto que sobe de tom.</summary>
		public static void Bubble(Patch p, double at, double hz, double gain)
		{
			p.Tone(Wave.Sine, hz, Envelope.Perc(0.004, p.Vary(0.05, 0.3), 2), at).Glide(hz * p.Vary(1.8, 0.15), 0.6).Gain(gain);
		}

		public static void Bubbles(Patch p, double at, double span, int count, double low, double high, double gain)
		{
			for (var i = 0; i < count; i++)
				Bubble(p, at + span * p.Rng.Next(), p.Rng.Range(low, high), gain * p.Rng.Range(0.4, 1));
		}

		/// <summary>Água chegando: a onda abafada e as bolhas depois.</summary>
		public static void Splash(Patch p, double at, double gain)
		{
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.006, 0.26, 2.5), at).Bandpass(1000, 0.7, 600).Gain(gain);
			Bubbles(p, at + 0.03, 0.25, 4, 380, 900, gain * 0.35);
		}

		/// <summary>Ar passando de <paramref name="from"/> a <paramref name="to"/> Hz, com um assobio baixo por cima.</summary>
		public static void Gust(Patch p, double at, double length, double from, double to, double gain)
		{
			var envelope = Envelope.Gust(length, 0.5);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(from, 2.5, to).Wobble(p.Vary(1.3, 0.2), 0.3).Gain(gain);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(from * 1.5, 7, to * 1.5).Lowpass(3000).Gain(gain * 0.3);
		}

		/// <summary>Sombra: serras graves desafinadas cantando "u", com a quinta por cima.</summary>
		public static void Umbra(Patch p, double at, double length, int midi, double gain)
		{
			var envelope = Envelope.Swell(length * 0.6, length * 0.4);
			p.Tone(Wave.Saw, Notes.Hz(midi), envelope, at).Unison(4, 20).Vowel(Vowel.U, Vowel.O).Lowpass(1100).Gain(gain);
			p.Tone(Wave.Sine, Notes.Hz(midi + 7), envelope, at).Gain(gain * 0.3);
		}
	}
}
