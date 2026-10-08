using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// A invocação: o círculo, o sigilo, o portal e a criatura chegando. A raridade sobe a intensidade em
	/// degraus (mais vozes no acorde, coro, subida antes, mais brilhos), e a revelação tem uma versão por
	/// estrela (<c>reveal_star_1</c> a <c>reveal_star_5</c>).
	/// </summary>
	internal static class SummonSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("summon", Mix.Reward);

			yield return r.Of("summon_start", "Iniciar invocação: o sigilo acende sobre a taça grave.", p =>
			{
				Arcane.Sigil(p, 0, 0.8);
				Arcane.Bowl(p, D4, 0.02, 1.0, 0.5);
				Arcane.Riser(p, 0.05, 0.45, Hz(D4), Hz(A4), 0.3);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("circle_appear", "Círculo mágico aparecendo: a taça e quatro cristais em volta.", p =>
			{
				Arcane.Bowl(p, D5, 0, 1.2, 0.5);
				Arcane.Air(p, new[] { D4, A4 }, 0, 0.2, 0.3, 0.6, 0.35);
				Arcane.Arpeggio(p, new[] { D6, A6, E6, D7 }, 0.05, 0.07, (note, at, _) => Arcane.Glass(p, note, at, 0.5, 0.35));
				Foley.Swish(p, 0, 0.4, 600, 2400, 0.3);
				p.Reverb(0.15, 1);
			});

			yield return r.Of("sigil_draw", "Sigilo sendo desenhado: o traço vivo e o sigilo acendendo no fim.", p =>
			{
				p.Tone(Wave.Triangle, Hz(D5), Envelope.Swell(0.45, 0.15)).Glide(Hz(A5)).Vibrato(5.5, 0.15).Gain(0.35);
				p.Crackle(250, 0.0015, Envelope.Swell(0.45, 0.15)).Bandpass(4000, 1.5).Gain(0.3);
				Foley.Quill(p, 0, 0.5, 0.3);
				Arcane.Sigil(p, 0.5, 0.8);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("energy_build", "Energia acumulando: a quinta subindo e o pulso acelerando.", p =>
			{
				Arcane.Riser(p, 0, 1.2, Hz(D4), Hz(D5), 0.5);
				p.Tone(Wave.Saw, Hz(D3), Envelope.Swell(1.15, 0.1)).Unison(3, 12).Lowpass(500, 2500).Tremolo(4, 0.5, 16).Gain(0.4);
				p.Crackle(10, 0.002, Envelope.Swell(1.2, 0.05)).DensityTo(140).Bandpass(3500, 1).Gain(0.25);
				p.Reverb(0.1, 0.6);
			});

			yield return r.Of("portal_open", "Portal abrindo: o ar sendo puxado, o estouro macio e o coro.", p =>
			{
				p.Noise(NoiseColor.Pink, Envelope.Swell(0.5, 0.6)).Bandpass(300, 1.5, 3000).Curve(0.6).Gain(0.6);
				Arcane.Pad(p, new[] { D3, A3, D4 }, 0.1, 0.4, 0.2, 0.6, 0.4, 1400);
				Strike.Boom(p, 0.5, 0.3, 0.5);
				Arcane.Choir(p, new[] { D4, A4 }, 0.4, 0.15, 0.2, 0.6, 0.35, Vowel.O, Vowel.A);
				Arcane.Bowl(p, D4, 0.5, 1.2, 0.4);
				p.Reverb(0.18, 1.2);
			});

			yield return r.Of("portal_stable", "Portal estabilizando: o tapete parado, a taça e o brilho.", p =>
			{
				Arcane.Air(p, new[] { D4, A4, E5 }, 0, 0.3, 0.5, 0.6, 0.5);
				Arcane.Bowl(p, D5, 0.1, 1.2, 0.35);
				Arcane.Shimmer(p, 0.2, 0.9, Hz(A5), 0.2);
				p.Reverb(0.15, 1);
			});

			yield return r.Of("creature_summoned", "Criatura sendo invocada: a chegada, o sino e o acorde maior.", p =>
			{
				Strike.Impact(p, 0, 0.25, 0.5);
				Arcane.Bell(p, D5, 0, 1, 0.5);
				Arcane.Chord(p, new[] { D5, Fs5, A5 }, 0.04, 0.03, 0.9, 0.6);
				Arcane.Sparkle(p, 0.05, 0.4, 6, D6, D7, 0.3, Major);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("summon_rare", "Invocação rara: subida curta e acorde de quatro sinos.", p =>
			{
				Arcane.Riser(p, 0, 0.3, Hz(A4), Hz(D5), 0.3);
				Arcane.Chord(p, new[] { D5, Fs5, A5, D6 }, 0.3, 0.035, 1.0, 0.9);
				Arcane.Sparkle(p, 0.32, 0.5, 8, D6, D7, 0.35, Major);
				p.Reverb(0.15, 1);
			});

			yield return r.Of("summon_very_rare", "Invocação muito rara: subida maior, chegada, cinco sinos e coro.", p =>
			{
				Arcane.Riser(p, 0, 0.5, Hz(D4), Hz(A4), 0.4);
				Strike.Impact(p, 0.5, 0.35, 0.4);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6, A6 }, 0.5, 0.035, 1.4, 1);
				Arcane.Choir(p, new[] { D4, A4, Fs5 }, 0.45, 0.08, 0.25, 0.7, 0.45);
				Arcane.Sparkle(p, 0.5, 0.8, 12, D6, A7, 0.35, Major);
				p.Reverb(0.18, 1.2);
			}, levelDb: 1.5);

			yield return r.With(Mix.Epic, "summon_legendary", "Invocação lendária: a subida inteira, o estouro, o sigilo, coro e seis sinos.", p =>
			{
				Arcane.Riser(p, 0, 0.85, Hz(D4), Hz(D5), 0.5);
				Strike.Boom(p, 0.85, 0.35, 0.5);
				Arcane.Sigil(p, 0.85, 0.8);
				Arcane.Choir(p, new[] { D4, A4, D5, Fs5, A5 }, 0.8, 0.1, 0.6, 1.2, 0.65);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6, A6, D7 }, 0.85, 0.04, 1.8, 1);
				Arcane.Sparkle(p, 0.85, 1.3, 20, D6, A7, 0.35, Major);
				p.Reverb(0.22, 1.6);
			}, levelDb: 1);

			yield return r.Of("summon_fail", "Invocação falha: a energia sobe e se desfaz num sopro.", p =>
			{
				Arcane.Riser(p, 0, 0.35, Hz(D4), Hz(A4), 0.35);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.01, 0.4, 2), 0.33).Lowpass(4000, 250).Curve(0.6).Gain(0.6);
				p.Tone(Wave.Triangle, Hz(A5), Envelope.Perc(0.005, 0.4), 0.33).Glide(Hz(D4), 0.7).Gain(0.4);
				p.Modal(Modes.Glass, Hz(D6), 0.3, 0.33).Bright(0.4).Gain(0.25);
				p.Modal(Modes.Glass, Hz(Ds6), 0.3, 0.33).Bright(0.4).Gain(0.25);
				Foley.Knock(p, 0.6, 300, 0.07, 0.4);
				p.Reverb(0.1, 0.7);
			});

			yield return r.Of("summon_duplicate", "Invocação repetida: o sino em eco e a criatura virando cacos de cristal.", p =>
			{
				Arcane.Bell(p, A5, 0, 0.8, 0.5);
				Arcane.Bell(p, A5, 0.14, 0.7, 0.35);
				Strike.Shatter(p, 0.28, 0.3, 2800);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("creature_reveal", "Revelação da criatura: o vidro crescendo ao contrário até o sino.", p => Reveal(p, false));

			yield return r.Of("new_creature_reveal", "Revelação de nova criatura: a revelação com coro e mais brilho.", p => Reveal(p, true), levelDb: 1);

			yield return r.Of("fragments_received", "Fragmentos recebidos: cacos de cristal tilintando.", p =>
			{
				for (var i = 0; i < 5; i++)
					Arcane.Glass(p, Snap(p.Rng.Range(A6, A7), Pentatonic), i * p.Vary(0.035, 0.3), p.Rng.Range(0.1, 0.25), 0.5);
				Arcane.Celesta(p, E6, 0.12, 0.3, 0.35);
				p.Reverb(0.08, 0.5);
			});

			yield return r.Of("essence_received", "Essência recebida: a taça, bolhas de luz e o vidro.", p =>
			{
				Arcane.Bowl(p, D6, 0, 0.7, 0.5);
				Elemental.Bubbles(p, 0, 0.25, 4, 600, 1300, 0.35);
				Arcane.Glass(p, A6, 0.06, 0.5, 0.45);
				Arcane.Shimmer(p, 0.05, 0.4, Hz(D6), 0.2);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("reveal_star_1", "Revelação 1★: uma nota de celesta.", p => Stars(p, 1), levelDb: -3);
			yield return r.Of("reveal_star_2", "Revelação 2★: a quinta em celesta.", p => Stars(p, 2), levelDb: -2);
			yield return r.Of("reveal_star_3", "Revelação 3★: o acorde maior em sinos.", p => Stars(p, 3));
			yield return r.Of("reveal_star_4", "Revelação 4★: subida, quatro sinos e coro.", p => Stars(p, 4), levelDb: 1);
			yield return r.With(Mix.Epic, "reveal_star_5", "Revelação 5★: a subida inteira, o estouro, o sigilo, coro e seis sinos.", p => Stars(p, 5), levelDb: 1);
		}

		/// <summary>A revelação: vidro ao contrário até a chegada, sino e quinta; a nova criatura ganha coro e acorde.</summary>
		private static void Reveal(Patch p, bool isNew)
		{
			p.Modal(Modes.Glass, Hz(A5), 0.5).Reverse().Gain(0.5);
			p.Noise(NoiseColor.White, Envelope.Swell(0.45, 0.05)).Bandpass(1500, 1.5, 5000).Gain(0.2);
			Strike.Impact(p, 0.5, 0.2, 0.4);
			Arcane.Bell(p, D5, 0.5, 1, 0.6);
			Arcane.Bell(p, A5, 0.52, 0.9, 0.45);
			Arcane.Sparkle(p, 0.52, 0.4, isNew ? 10 : 5, D6, D7, 0.3, Major);
			if (isNew)
			{
				Arcane.Choir(p, new[] { D4, A4, Fs5 }, 0.48, 0.08, 0.3, 0.8, 0.5);
				Arcane.Chord(p, new[] { D6, Fs6, A6 }, 0.55, 0.03, 1, 0.5);
			}
			p.Reverb(0.15, 1);
		}

		/// <summary>A revelação de uma criatura de <paramref name="stars"/> estrelas: cada degrau põe uma camada.</summary>
		private static void Stars(Patch p, int stars)
		{
			Foley.Knock(p, 0, 900, 0.04, 0.3);
			switch (stars)
			{
				case 1:
					Arcane.Celesta(p, D6, 0, 0.4, 0.6);
					p.Reverb(0.08, 0.5);
					break;
				case 2:
					Arcane.Celesta(p, D6, 0, 0.4, 0.6);
					Arcane.Celesta(p, A6, 0.07, 0.45, 0.5);
					p.Reverb(0.1, 0.6);
					break;
				case 3:
					Arcane.Chord(p, new[] { D5, Fs5, A5 }, 0, 0.04, 0.9, 0.8);
					Arcane.Sparkle(p, 0.05, 0.3, 4, D6, D7, 0.25, Major);
					p.Reverb(0.12, 0.8);
					break;
				case 4:
					Arcane.Riser(p, 0, 0.25, Hz(A4), Hz(D5), 0.3);
					Arcane.Chord(p, new[] { D5, A5, D6, Fs6 }, 0.25, 0.035, 1.2, 0.9);
					Arcane.Choir(p, new[] { D4, A4, Fs5 }, 0.22, 0.06, 0.2, 0.6, 0.4);
					Arcane.Sparkle(p, 0.25, 0.6, 8, D6, D7, 0.3, Major);
					p.Reverb(0.16, 1.1);
					break;
				default:
					Arcane.Riser(p, 0, 0.5, Hz(D4), Hz(D5), 0.45);
					Strike.Boom(p, 0.5, 0.3, 0.45);
					Arcane.Sigil(p, 0.5, 0.8);
					Arcane.Choir(p, new[] { D4, A4, D5, Fs5, A5 }, 0.45, 0.1, 0.5, 1.1, 0.65);
					Arcane.Chord(p, new[] { D5, A5, D6, Fs6, A6, D7 }, 0.5, 0.04, 1.7, 1);
					Arcane.Sparkle(p, 0.5, 1.2, 16, D6, A7, 0.35, Major);
					p.Reverb(0.22, 1.5);
					break;
			}
		}
	}
}
