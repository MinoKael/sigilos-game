using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Library.Recipe;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// A magia dos cinco elementos. Cada um se reconhece pela textura (<see cref="Elemental"/>), não pelo tom:
	/// Fogo crepita, Água borbulha, Vento assobia, Luz soa em cristal e coro, Trevas em sombra cantada e sons
	/// ao contrário. A grande magia de cada um começa pelo sigilo e é da classe épica.
	/// </summary>
	internal static class ElementSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat/elements", Mix.Combat);

			yield return r.Of("fire_flame", "Fogo: chama.", p =>
			{
				Elemental.Flame(p, 0, p.Vary(0.6, 0.1), 1);
				Elemental.Ignite(p, 0, 0.4);
			}, 2);

			yield return r.Of("fire_burst", "Fogo: explosão.", p =>
			{
				Elemental.Ignite(p, 0, 0.6);
				Strike.Boom(p, 0.05, 0.4, 0.8);
				Elemental.Flame(p, 0.05, 0.4, 0.5);
			}, 2);

			yield return r.Of("fire_projectile", "Fogo: projétil.", p =>
			{
				Strike.Whoosh(p, 0, 0.3, 600, 2000, 0.4, 1);
				Elemental.Flame(p, 0, 0.32, 0.7);
				Elemental.Ignite(p, 0.28, 0.5);
			}, 2);

			yield return r.Of("fire_burn", "Fogo: queimadura.", p =>
			{
				Elemental.Flame(p, 0, 0.5, 0.7);
				Elemental.Sizzle(p, 0, 0.45, 0.5);
			}, 2);

			yield return r.With(Mix.Epic, "fire_grand", "Fogo: grande magia. O sigilo, o fogo pegando, o estouro e o incêndio.", p =>
			{
				Arcane.Sigil(p, 0, 0.6, D5);
				Elemental.Ignite(p, 0.25, 0.9);
				Elemental.Flame(p, 0.25, 1.3, 0.9);
				Strike.Boom(p, 0.6, 0.9, 1);
				Arcane.Pad(p, new[] { D3, A3, D4 }, 0.2, 0.4, 0.4, 0.7, 0.35, 1100);
				Arcane.Choir(p, new[] { D4, A4 }, 0.3, 0.2, 0.3, 0.6, 0.25, Vowel.O, Vowel.A);
				p.Reverb(0.15, 1.1);
			});

			yield return r.Of("water_drop", "Água: gota.", p =>
			{
				Elemental.Bubble(p, 0, p.Vary(800, 0.1), 0.8);
				Arcane.Glass(p, Pick(p, A6, D7), 0, 0.2, 0.25);
				Elemental.Bubble(p, 0.06, p.Vary(1100, 0.1), 0.35);
			}, 2);

			yield return r.Of("water_flow", "Água: fluxo.", p =>
			{
				Elemental.Slosh(p, 0, 0.8, 0.8);
				Elemental.Bubbles(p, 0.1, 0.6, 6, 400, 1000, 0.35);
			}, 2);

			yield return r.Of("water_impact", "Água: impacto aquático.", p =>
			{
				Elemental.Splash(p, 0, 1);
				Strike.Impact(p, 0, 0.3, 0.5);
			}, 2);

			yield return r.Of("water_wave", "Água: onda.", p => Tide(p, 0), 2);

			yield return r.With(Mix.Epic, "water_grand", "Água: grande magia. O sigilo, a maré subindo e a onda quebrando.", p =>
			{
				Arcane.Sigil(p, 0, 0.6);
				Arcane.Riser(p, 0.1, 0.5, Hz(D4), Hz(A4), 0.3);
				Tide(p, 0.4);
				Elemental.Splash(p, 0.95, 1);
				Elemental.Bubbles(p, 0.95, 0.8, 14, 300, 1200, 0.4);
				Arcane.Bowl(p, D4, 0.9, 1.5, 0.4);
				p.Reverb(0.18, 1.2);
			});

			yield return r.Of("wind_cut", "Vento: corte.", p =>
			{
				Elemental.Gust(p, 0, 0.18, 1500, 4500, 0.7);
				Strike.Slash(p, 0.05, 0.6);
			}, 2);

			yield return r.Of("wind_gust", "Vento: rajada.", p => Elemental.Gust(p, 0, 0.6, 500, 1800, 1), 2);

			yield return r.Of("wind_tornado", "Vento: tornado.", p => Tornado(p, 0, 1.2), 2);

			yield return r.Of("wind_dash", "Vento: movimento rápido.", p =>
			{
				Strike.Whoosh(p, 0, 0.15, 600, 3500, 0.8, 1.5);
				Elemental.Gust(p, 0.02, 0.18, 2000, 4000, 0.4);
			}, 2);

			yield return r.With(Mix.Epic, "wind_grand", "Vento: grande magia. O sigilo, o redemoinho crescendo e a rajada.", p =>
			{
				Arcane.Sigil(p, 0, 0.6, E6);
				Tornado(p, 0.15, 1.6);
				Elemental.Gust(p, 0.3, 1.4, 300, 2400, 0.8);
				Strike.Whoosh(p, 1.1, 0.4, 500, 4000, 0.6, 1.2);
				p.Reverb(0.12, 1);
			});

			yield return r.Of("light_glint", "Luz: brilho.", p =>
			{
				Arcane.Glint(p, Pick(p, D6, A6), 0, 0.7);
				Arcane.Glass(p, A6, 0.02, 0.3, 0.4);
				p.Noise(NoiseColor.White, Envelope.Perc(0.002, 0.12)).Bandpass(4000, 2).Gain(0.12);
				p.Reverb(0.1, 0.6);
			}, 2);

			yield return r.Of("light_seal", "Luz: selo sagrado. A pedra do selo, o sino e a quinta de cristal.", p =>
			{
				p.Modal(Modes.Stone, p.Vary(500, 0.03), 0.3).Gain(0.6);
				Arcane.Bell(p, D5, 0, 0.9, 0.6);
				Arcane.Chord(p, new[] { D6, A6 }, 0.02, 0.02, 0.7, 0.5, Modes.Glass);
				Arcane.Bowl(p, D5, 0, 0.9, 0.3);
				p.Reverb(0.15, 0.9);
			}, 2);

			yield return r.Of("light_heal", "Luz: cura luminosa. Coro em \"a\" e a celesta subindo.", p =>
			{
				Arcane.Choir(p, new[] { D5, Fs5, A5 }, 0, 0.15, 0.2, 0.5, 0.6);
				Arcane.Arpeggio(p, new[] { D6, Fs6, A6, D7 }, 0.05, 0.06, (note, at, _) => Arcane.Celesta(p, note, at, 0.4, 0.45));
				Arcane.Shimmer(p, 0.05, 0.6, Hz(A5), 0.25);
				p.Reverb(0.18, 1.1);
			}, 2);

			yield return r.Of("light_burst", "Luz: explosão de luz.", p =>
			{
				p.Noise(NoiseColor.White, Envelope.Perc(0.003, 0.3, 2.5)).Bandpass(4000, 0.8, 1500).Gain(0.5);
				Arcane.Chord(p, new[] { D6, Fs6, A6, D7 }, 0, 0.01, 0.8, 0.9, Modes.Glass);
				Arcane.Bell(p, D5, 0, 1, 0.5);
				Strike.Impact(p, 0, 0.3, 0.4);
				p.Reverb(0.15, 1);
			}, 2);

			yield return r.With(Mix.Epic, "light_grand", "Luz: grande magia. O sigilo, o coro maior abrindo e a explosão de sinos.", p =>
			{
				Arcane.Sigil(p, 0, 0.7);
				Arcane.Choir(p, new[] { D4, A4, D5, Fs5, A5 }, 0.25, 0.3, 0.5, 1.1, 0.7);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6, A6 }, 0.55, 0.02, 1.6, 1);
				p.Noise(NoiseColor.White, Envelope.Perc(0.003, 0.4, 2.5), 0.55).Bandpass(4000, 0.8, 1500).Gain(0.4);
				Arcane.Shimmer(p, 0.55, 1.2, Hz(D6), 0.3);
				Arcane.Sparkle(p, 0.55, 1.0, 14, D6, A7, 0.3, Major);
				p.Reverb(0.22, 1.5);
			});

			yield return r.Of("dark_whisper", "Trevas: sussurro sombrio.", p =>
			{
				Arcane.Whisper(p, 0, 0.7, 0.8);
				Elemental.Umbra(p, 0, 0.7, D3, 0.35);
				p.Reverb(0.15, 0.9);
			}, 2);

			yield return r.Of("dark_energy", "Trevas: energia obscura. A sombra cantando e a taça ao contrário.", p =>
			{
				Elemental.Umbra(p, 0, 0.7, D3, 0.8);
				p.Modal(Modes.Bowl, Hz(Ds4), 0.7).Reverse().Gain(0.4);
				p.Noise(NoiseColor.Pink, Envelope.Swell(0.5, 0.2)).Lowpass(900).Tremolo(p.Vary(7, 0.2), 0.5).Gain(0.3);
				p.Reverb(0.12, 0.8);
			}, 2);

			yield return r.Of("dark_corruption", "Trevas: corrupção. A sombra, estalos secos e bolhas graves.", p =>
			{
				Elemental.Umbra(p, 0, 0.8, D3, 0.7);
				p.Crackle(40, 0.002, Envelope.Swell(0.4, 0.4)).Bandpass(1500, 1.2).Gain(0.35);
				Elemental.Bubbles(p, 0.1, 0.6, 6, 150, 400, 0.35);
				p.Tone(Wave.Sine, Hz(A4), Envelope.Swell(0.4, 0.4)).Glide(Hz(Gs4)).Fm(1.41, 2).Gain(0.2);
				p.Reverb(0.12, 0.8);
			}, 2);

			yield return r.Of("dark_impact", "Trevas: impacto sombrio. O sopro ao contrário até o golpe e a sombra depois.", p =>
			{
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.35)).Bandpass(700, 1.2).Reverse().Gain(0.6);
				Strike.Impact(p, 0.35, 0.65, 1);
				p.Tone(Wave.Saw, Hz(D3), Envelope.Perc(0.01, 0.5), 0.35).Unison(3, 25).Vowel(Vowel.U).Lowpass(900).Gain(0.4);
				p.Reverb(0.1, 0.8);
			}, 2);

			yield return r.With(Mix.Epic, "dark_grand", "Trevas: grande magia. O sigilo grave, o coro em \"u\", a sombra subindo e o golpe.", p =>
			{
				Arcane.Sigil(p, 0, 0.55, D5);
				Arcane.Choir(p, new[] { D3, A3, Ds4 }, 0.1, 0.5, 0.4, 0.8, 0.6, Vowel.U, Vowel.O);
				Elemental.Umbra(p, 0.1, 1.0, D2, 0.6);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.5), 0.5).Bandpass(600, 1.2).Reverse().Gain(0.5);
				Strike.Boom(p, 1.0, 0.6, 0.7);
				Arcane.Whisper(p, 0.2, 0.9, 0.3);
				p.Reverb(0.2, 1.4);
			});
		}

		/// <summary>A onda: o mar crescendo, a água correndo e a quebra.</summary>
		private static void Tide(Patch p, double at)
		{
			p.Noise(NoiseColor.Pink, Envelope.Swell(0.5, 0.5), at).Bandpass(300, 1, 1500).Wobble(1.5, 0.4).Gain(0.7);
			Elemental.Slosh(p, at + 0.1, 0.8, 0.5);
			Elemental.Splash(p, at + 0.55, 0.7);
		}

		/// <summary>O redemoinho: faixas girando rápido e um assobio que sobe e desce.</summary>
		private static void Tornado(Patch p, double at, double length)
		{
			var envelope = Envelope.Swell(length * 0.35, length * 0.65);
			p.Noise(NoiseColor.Pink, envelope, at).Bandpass(700, 3).Wobble(p.Vary(5, 0.1), 1).Gain(0.8);
			p.Noise(NoiseColor.White, envelope, at).Bandpass(1600, 10).Wobble(p.Vary(6, 0.1), 0.8, 0.3).Gain(0.3);
			Elemental.Gust(p, at, length, 500, 1000, 0.5);
		}
	}
}
