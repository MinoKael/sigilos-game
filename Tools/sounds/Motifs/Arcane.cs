using System;
using System.Collections.Generic;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds.Motifs
{
	/// <summary>
	/// A voz mágica do Sigilos, a mesma da música: harpa macia, sininhos de caixinha de música, taça, coro e
	/// tapetes discretos. Os sinos entram com ataque macio e sem o agudo do martelo. Notas em
	/// <see cref="Notes"/> (MIDI), em Mi menor.
	/// </summary>
	public static class Arcane
	{
		/// <summary>O ataque dos sinos, em segundos: macio, sem o "tic" do martelo.</summary>
		private const double SoftAttack = 0.003;

		/// <summary>Acima disto os sinos não têm nada a dizer, só cansam.</summary>
		private const double BellCeiling = 6000;

		public static void Bell(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Bell, Notes.Hz(midi), decay, at, SoftAttack).Lowpass(BellCeiling).Gain(gain);

		/// <summary>Celesta: quase só a fundamental, o sininho de caixinha de música.</summary>
		public static void Celesta(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Celesta, Notes.Hz(midi), decay, at, SoftAttack).Lowpass(BellCeiling).Gain(gain);

		/// <summary>Marimba com o ataque seco de baqueta: só nos cliques, que já soam bem assim.</summary>
		public static void Marimba(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Marimba, Notes.Hz(midi), decay, at).Gain(gain);

		/// <summary>Taça cantante: entra macia e fica batendo devagar.</summary>
		public static void Bowl(Patch p, int midi, double at, double decay, double gain) => p.Modal(Modes.Bowl, Notes.Hz(midi), decay, at, attack: 0.025).Gain(gain);

		/// <summary>Harpa: a corda com dedo macio e o seno redondo por baixo. A voz da música do jogo.</summary>
		public static void Harp(Patch p, int midi, double at, double decay, double gain)
		{
			var hz = Notes.Hz(midi);
			p.Pluck(hz, decay, at, 0.004).Bright(0.4).Lowpass(Math.Clamp(hz * 6, 2200, 4500)).Gain(gain);
			p.Tone(Wave.Sine, hz, Envelope.Perc(0.008, decay * 0.5), at).Gain(gain * 0.25);
		}

		/// <summary>As notas uma depois da outra, a cada <paramref name="step"/> segundos.</summary>
		public static void Arpeggio(Patch p, IReadOnlyList<int> notes, double at, double step, Action<int, double, int> voice)
		{
			for (var i = 0; i < notes.Count; i++)
				voice(notes[i], at + i * step, i);
		}

		/// <summary>Pequenos sininhos espalhados em <paramref name="span"/> segundos, nas notas da pentatônica entre <paramref name="low"/> e <paramref name="high"/>.</summary>
		public static void Sparkle(Patch p, double at, double span, int count, int low, int high, double gain)
		{
			for (var i = 0; i < count; i++)
			{
				var midi = Notes.Snap(p.Rng.Range(low, high), Notes.Pentatonic);
				Celesta(p, midi, at + span * p.Rng.Next(), p.Rng.Range(0.2, 0.4), gain * p.Rng.Range(0.4, 1));
			}
		}

		/// <summary>Tapete macio de triângulos: o fundo do céu.</summary>
		public static void Air(Patch p, IReadOnlyList<int> notes, double at, double attack, double hold, double release, double gain)
		{
			foreach (var note in notes)
				p.Tone(Wave.Triangle, Notes.Hz(note), Envelope.Pad(attack, hold, release), at).Unison(2, 6).Tremolo(p.Vary(2.5, 0.3), 0.25).Gain(gain / Math.Sqrt(notes.Count));
		}

		/// <summary>Coro sem palavras na vogal <paramref name="vowel"/> (indo para <paramref name="to"/>), sem o chiado de cima.</summary>
		public static void Choir(Patch p, IReadOnlyList<int> notes, double at, double attack, double hold, double release, double gain, Vowel vowel = Vowel.A, Vowel? to = null)
		{
			foreach (var note in notes)
				p.Tone(Wave.Saw, Notes.Hz(note), Envelope.Pad(attack, hold, release), at).Unison(3, 12).Vibrato(p.Vary(5.2, 0.08), 0.12, attack * 0.5).Vowel(vowel, to).Lowpass(3200).Gain(gain / Math.Sqrt(notes.Count));
		}

		/// <summary>Energia subindo de <paramref name="fromHz"/> a <paramref name="toHz"/>: tom em quinta e um sopro baixo.</summary>
		public static void Riser(Patch p, double at, double length, double fromHz, double toHz, double gain)
		{
			var envelope = Envelope.Swell(length * 0.92, length * 0.08);
			p.Tone(Wave.Triangle, fromHz, envelope, at).Glide(toHz, 1.6).Vibrato(6, 0.08).Gain(gain);
			p.Tone(Wave.Triangle, fromHz * 1.5, envelope, at).Glide(toHz * 1.5, 1.6).Gain(gain * 0.4);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(fromHz * 2, 2, Math.Min(toHz * 3, 2800)).Gain(gain * 0.3);
		}

		/// <summary>A assinatura do Sigilos: o sigilo acendendo, um sopro e Ré–Lá–Mi em sininhos, resolvendo em Mi.</summary>
		public static void Sigil(Patch p, double at, double gain, int root = Notes.D5)
		{
			p.Noise(NoiseColor.Pink, Envelope.Gust(0.3, 0.5), at).Bandpass(900, 1.4, 2600).Gain(gain * 0.15);
			Celesta(p, root, at + 0.03, 0.8, gain);
			Celesta(p, root + 7, at + 0.09, 0.7, gain * 0.75);
			Bell(p, root + 14, at + 0.15, 0.9, gain * 0.55);
		}

		/// <summary>Um acorde de sinos levemente arpejado (<paramref name="strum"/> segundos entre as notas).</summary>
		public static void Chord(Patch p, IReadOnlyList<int> notes, double at, double strum, double decay, double gain, Modes? modes = null)
		{
			modes ??= Modes.Bell;
			for (var i = 0; i < notes.Count; i++)
				p.Modal(modes, Notes.Hz(notes[i]), decay, at + i * strum, SoftAttack).Lowpass(BellCeiling).Gain(gain / Math.Sqrt(notes.Count));
		}
	}
}
