using System;
using System.Collections.Generic;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// A voz mágica do Sigilos: sinos, cristais, celesta, coro, tapetes discretos, brilhos e o lampejo do
	/// sigilo. Notas em <see cref="Notes"/> (MIDI), sempre em volta de Ré.
	/// </summary>
	public static class Arcane
	{
		public static void Bell(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Bell, Notes.Hz(midi), decay, at).Gain(gain);

		public static void Glass(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Glass, Notes.Hz(midi), decay, at).Gain(gain);

		public static void Celesta(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Celesta, Notes.Hz(midi), decay, at).Gain(gain);

		public static void Marimba(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Marimba, Notes.Hz(midi), decay, at).Gain(gain);

		/// <summary>Taça cantante: entra macia e fica batendo devagar.</summary>
		public static void Bowl(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Bowl, Notes.Hz(midi), decay, at, attack: 0.025).Gain(gain);

		/// <summary>As notas uma depois da outra, a cada <paramref name="step"/> segundos.</summary>
		public static void Arpeggio(Patch p, IReadOnlyList<int> notes, double at, double step, Action<int, double, int> voice)
		{
			for (var i = 0; i < notes.Count; i++)
				voice(notes[i], at + i * step, i);
		}

		/// <summary>Pequenos brilhos de cristal espalhados em <paramref name="span"/> segundos, nas notas da escala entre <paramref name="low"/> e <paramref name="high"/>.</summary>
		public static void Sparkle(Patch p, double at, double span, int count, int low, int high, double gain, IReadOnlyList<int>? scale = null)
		{
			scale ??= Notes.Lydian;
			for (var i = 0; i < count; i++)
			{
				var midi = Notes.Snap(p.Rng.Range(low, high), scale);
				p.Modal(Modes.Glass, Notes.Hz(midi), p.Rng.Range(0.12, 0.35), at + span * p.Rng.Next()).Lowpass(7000).Gain(gain * p.Rng.Range(0.4, 1));
			}
		}

		/// <summary>Brilho que treme: parciais altos, cada um vibrando num ritmo, mais um sopro de ar.</summary>
		public static void Shimmer(Patch p, double at, double length, double hz, double gain)
		{
			var partials = new[] { (1.0, 7.1), (1.5, 9.3), (2.0, 11.7), (3.0, 13.1) };
			foreach (var (ratio, rate) in partials)
				p.Tone(Wave.Sine, hz * ratio * (1 + p.Rng.Signed() * 0.003), Envelope.Swell(length * 0.3, length * 0.7), at).Tremolo(p.Vary(rate, 0.1), 0.7).Gain(gain / ratio);
			p.Noise(NoiseColor.White, Envelope.Swell(length * 0.3, length * 0.7), at).Bandpass(Math.Min(hz * 3, 8000), 3).Tremolo(13, 0.6).Gain(gain * 0.15);
		}

		/// <summary>Tapete discreto de serras filtradas: sustenta acordes sem soar eletrônico.</summary>
		public static void Pad(Patch p, IReadOnlyList<int> notes, double at, double attack, double hold, double release, double gain, double cutoff = 1800)
		{
			foreach (var note in notes)
				p.Tone(Wave.Saw, Notes.Hz(note), Envelope.Pad(attack, hold, release), at).Unison(3, 9).Lowpass(cutoff).Vibrato(4.5, 0.05).Gain(gain / Math.Sqrt(notes.Count));
		}

		/// <summary>Tapete ainda mais macio, de triângulos: o fundo do céu.</summary>
		public static void Air(Patch p, IReadOnlyList<int> notes, double at, double attack, double hold, double release, double gain)
		{
			foreach (var note in notes)
				p.Tone(Wave.Triangle, Notes.Hz(note), Envelope.Pad(attack, hold, release), at).Unison(2, 6).Tremolo(p.Vary(2.5, 0.3), 0.25).Gain(gain / Math.Sqrt(notes.Count));
		}

		/// <summary>Coro sem palavras na vogal <paramref name="vowel"/> (indo para <paramref name="to"/>).</summary>
		public static void Choir(Patch p, IReadOnlyList<int> notes, double at, double attack, double hold, double release, double gain, Vowel vowel = Vowel.A, Vowel? to = null)
		{
			foreach (var note in notes)
				p.Tone(Wave.Saw, Notes.Hz(note), Envelope.Pad(attack, hold, release), at).Unison(3, 12).Vibrato(p.Vary(5.2, 0.08), 0.12, attack * 0.5).Vowel(vowel, to).Gain(gain / Math.Sqrt(notes.Count));
		}

		/// <summary>Energia subindo de <paramref name="fromHz"/> a <paramref name="toHz"/>: tom em quinta e ar.</summary>
		public static void Riser(Patch p, double at, double length, double fromHz, double toHz, double gain)
		{
			var envelope = Envelope.Swell(length * 0.92, length * 0.08);
			p.Tone(Wave.Triangle, fromHz, envelope, at).Glide(toHz, 1.6).Vibrato(6, 0.08).Gain(gain);
			p.Tone(Wave.Triangle, fromHz * 1.5, envelope, at).Glide(toHz * 1.5, 1.6).Gain(gain * 0.45);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(fromHz * 2, 2, Math.Min(toHz * 4, 9000)).Gain(gain * 0.5);
		}

		/// <summary>A assinatura do Sigilos: o lampejo de um sigilo aceso, Ré–Lá–Mi em cristal com um sopro subindo.</summary>
		public static void Sigil(Patch p, double at, double gain, int root = Notes.D6)
		{
			p.Noise(NoiseColor.White, Envelope.Gust(0.22, 0.4), at).Bandpass(1800, 2.2, 6500).Gain(gain * 0.25);
			Glass(p, root, at + 0.03, 0.7, gain);
			Glass(p, root + 7, at + 0.055, 0.6, gain * 0.7);
			Glass(p, root + 14, at + 0.08, 0.5, gain * 0.45);
			p.Tone(Wave.Sine, Notes.Hz(root), Envelope.Perc(0.002, 0.35), at + 0.03).Fm(3.01, 1.6, 0).Gain(gain * 0.3);
		}

		/// <summary>Lampejo com FM: o brilho de uma estrela.</summary>
		public static void Glint(Patch p, int midi, double at, double gain)
		{
			p.Tone(Wave.Sine, Notes.Hz(midi), Envelope.Perc(0.001, 0.4), at).Fm(3.5, 1.6, 0).Lowpass(6500).Gain(gain);
			p.Tone(Wave.Sine, Notes.Hz(midi + 7), Envelope.Perc(0.002, 0.1), at).Glide(Notes.Hz(midi + 12), 0.5).Gain(gain * 0.3);
		}

		/// <summary>Um acorde de sinos levemente arpejado (<paramref name="strum"/> segundos entre as notas).</summary>
		public static void Chord(Patch p, IReadOnlyList<int> notes, double at, double strum, double decay, double gain, Modes? modes = null)
		{
			modes ??= Modes.Bell;
			for (var i = 0; i < notes.Count; i++)
				p.Modal(modes, Notes.Hz(notes[i]), decay, at + i * strum).Gain(gain / Math.Sqrt(notes.Count));
		}

		/// <summary>Sussurro: ruído "falando" entre O e E, com o tremor da respiração.</summary>
		public static void Whisper(Patch p, double at, double length, double gain)
		{
			p.Noise(NoiseColor.Pink, Envelope.Swell(length * 0.4, length * 0.6), at).Vowel(Vowel.O, Vowel.E).Highpass(400).Tremolo(p.Vary(6, 0.2), 0.7).Gain(gain);
		}
	}
}
