using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// A textura de cada elemento, que o reconhece sem depender do tom: Fogo crepita e ruge (estalos e ruído
	/// grave tremulando); Água borbulha e escorre (bolhas subindo e faixa balançando devagar); Vento assobia
	/// (faixa estreita que anda, sem nota de sino); Luz soa em cristal e coro maior; Trevas em serras graves
	/// cantando "u", segunda menor e sons que crescem ao contrário.
	/// </summary>
	public static class Elemental
	{
		/// <summary>Chama acesa por <paramref name="length"/> segundos: rugido tremulando e estalos.</summary>
		public static void Flame(Patch p, double at, double length, double gain)
		{
			var envelope = Envelope.Swell(length * 0.25, length * 0.75);
			p.Noise(NoiseColor.Brown, envelope, at).Lowpass(900, 0, 0.9).Wobble(p.Vary(7, 0.2), 0.5).Gain(gain);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(1800, 0.8).Wobble(11, 0.4).Tremolo(13, 0.5).Gain(gain * 0.35);
			p.Crackle(p.Vary(45, 0.2), 0.0012, envelope, at).Bandpass(3000, 0.7).Gain(gain * 0.7);
		}

		/// <summary>O "fuum" do fogo pegando.</summary>
		public static void Ignite(Patch p, double at, double gain)
		{
			p.Noise(NoiseColor.Pink, new Envelope(0.05, 0, 0.35, 0, 0, 0, 2.5, 1.5), at).Lowpass(500, 3500).Curve(0.5).Drive(2).Gain(gain);
			p.Crackle(90, 0.0012, Envelope.Perc(0.01, 0.35, 2), at + 0.02).Bandpass(2800, 0.7).Gain(gain * 0.6);
		}

		/// <summary>Chiado da brasa.</summary>
		public static void Sizzle(Patch p, double at, double length, double gain)
		{
			p.Noise(NoiseColor.White, Envelope.Perc(0.01, length, 2), at).Bandpass(4200, 1.5).Tremolo(p.Vary(26, 0.2), 0.5).Gain(gain);
		}

		/// <summary>Uma bolha: seno curto que sobe de tom.</summary>
		public static void Bubble(Patch p, double at, double hz, double gain)
		{
			p.Tone(Wave.Sine, hz, Envelope.Perc(0.002, p.Vary(0.05, 0.3), 2), at).Glide(hz * p.Vary(2.2, 0.2), 0.6).Gain(gain);
		}

		public static void Bubbles(Patch p, double at, double span, int count, double low, double high, double gain)
		{
			for (var i = 0; i < count; i++)
				Bubble(p, at + span * p.Rng.Next(), p.Rng.Range(low, high), gain * p.Rng.Range(0.4, 1));
		}

		/// <summary>Água correndo: faixas balançando devagar.</summary>
		public static void Slosh(Patch p, double at, double length, double gain)
		{
			var envelope = Envelope.Swell(length * 0.35, length * 0.65);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(700, 1.6).Wobble(p.Vary(2.2, 0.2), 1.0).Gain(gain);
			p.Noise(NoiseColor.White, envelope, at).Bandpass(2500, 1.2).Wobble(p.Vary(3.1, 0.2), 0.8, 0.3).Gain(gain * 0.35);
		}

		/// <summary>Respingo: o estouro de água e as bolhas e gotas depois.</summary>
		public static void Splash(Patch p, double at, double gain)
		{
			p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.28, 2.5), at).Bandpass(1200, 0.6, 700).Gain(gain);
			p.Noise(NoiseColor.White, Envelope.Perc(0.002, 0.15, 3), at).Bandpass(3200, 1).Gain(gain * 0.3);
			Bubbles(p, at + 0.03, 0.3, 6, 400, 1100, gain * 0.35);
		}

		/// <summary>Rajada de vento indo de <paramref name="from"/> a <paramref name="to"/> Hz, com o assobio por cima.</summary>
		public static void Gust(Patch p, double at, double length, double from, double to, double gain)
		{
			var envelope = Envelope.Gust(length, 0.5);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(from, 3.5, to).Wobble(p.Vary(1.3, 0.2), 0.35).Gain(gain);
			p.Noise(NoiseColor.White, envelope, at).Bandpass(from * 2, 9, to * 2).Wobble(p.Vary(1.7, 0.2), 0.3, 0.25).Gain(gain * 0.3);
		}

		/// <summary>Sombra: serras graves desafinadas cantando "u", com a segunda menor por cima.</summary>
		public static void Umbra(Patch p, double at, double length, int midi, double gain)
		{
			var envelope = Envelope.Swell(length * 0.7, length * 0.3);
			p.Tone(Wave.Saw, Notes.Hz(midi), envelope, at).Unison(4, 25).Vowel(Vowel.U, Vowel.O).Lowpass(1400).Gain(gain);
			p.Tone(Wave.Sine, Notes.Hz(midi + 13), envelope, at).Fm(1.41, 1.2, 0.3).Gain(gain * 0.35);
		}
	}
}
