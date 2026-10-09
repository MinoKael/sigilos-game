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
	/// A magia dos cinco elementos, dois sons cada: o impacto da habilidade e a grande magia (a de recarga
	/// longa). Cada elemento se reconhece pela textura (<see cref="Elemental"/>) sobre o mesmo golpe macio; a
	/// grande magia começa pelo sigilo e cresce em espaço, não em volume.
	/// </summary>
	internal static class ElementSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat/elements", Mix.Combat);

			yield return r.Of("fire_impact", "Fogo: o sopro morno e o golpe.", p =>
			{
				Elemental.Flame(p, 0, p.Vary(0.4, 0.1), 0.8);
				Strike.Hit(p, 0.02, 0.45, Pick(p, B4, E4), 0.6);
				Strike.Bloom(p, 0.02, 0.15, 0.25);
			}, 2, -2);

			yield return r.Of("water_impact", "Água: a onda abafada, as bolhas e o golpe.", p =>
			{
				Elemental.Splash(p, 0, 1);
				Strike.Hit(p, 0, 0.35, Pick(p, G4, E4), 0.55);
			}, 2, 1);

			yield return r.Of("wind_impact", "Vento: o ar passando e o golpe.", p =>
			{
				Elemental.Gust(p, 0, 0.28, p.Vary(600, 0.1), p.Vary(2000, 0.1), 0.8);
				Strike.Hit(p, 0.1, 0.3, Pick(p, A4, D5), 0.5);
			}, 2, 0.5);

			yield return r.Of("light_impact", "Luz: o sino, a celesta e o golpe.", p =>
			{
				Arcane.Bell(p, Pick(p, B5, G5), 0, 0.5, 0.6);
				Arcane.Celesta(p, E6, 0.025, 0.4, 0.35);
				Strike.Hit(p, 0, 0.3, E4, 0.5);
				p.Reverb(0.1, 0.6);
			}, 2, 1.5);

			yield return r.Of("dark_impact", "Trevas: o sopro ao contrário até o golpe e a sombra depois.", p =>
			{
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.18)).Bandpass(500, 1.2).Lowpass(1500).Reverse().Gain(0.5);
				Strike.Hit(p, 0.18, 0.6, Pick(p, E3, G3), 0.8);
				Elemental.Umbra(p, 0.14, 0.4, E3, 0.3);
				p.Reverb(0.1, 0.7);
			}, 2, -1);

			yield return r.With(Mix.Epic, "fire_grand", "Fogo: grande magia. O sigilo, a chama crescendo e a florada quente.", p =>
			{
				Arcane.Sigil(p, 0, 0.45);
				Elemental.Flame(p, 0.15, 0.8, 0.9);
				Strike.Bloom(p, 0.45, 0.6, 0.7);
				Strike.Hit(p, 0.45, 0.6, E3, 0.6);
				Arcane.Air(p, new[] { E3, B3 }, 0.15, 0.3, 0.2, 0.5, 0.3);
				p.Reverb(0.15, 1.0);
			}, levelDb: -0.5);

			yield return r.With(Mix.Epic, "water_grand", "Água: grande magia. O sigilo, a maré subindo e a onda chegando.", p =>
			{
				Arcane.Sigil(p, 0, 0.45);
				Arcane.Riser(p, 0.1, 0.4, Hz(E4), Hz(B4), 0.25);
				Elemental.Splash(p, 0.5, 1);
				Elemental.Bubbles(p, 0.5, 0.5, 8, 300, 1000, 0.3);
				Arcane.Bowl(p, E4, 0.45, 1.2, 0.35);
				Strike.Hit(p, 0.5, 0.5, B3, 0.5);
				p.Reverb(0.16, 1.1);
			}, levelDb: 2);

			yield return r.With(Mix.Epic, "wind_grand", "Vento: grande magia. O sigilo, o redemoinho e a rajada.", p =>
			{
				Arcane.Sigil(p, 0, 0.45, A4);
				Elemental.Gust(p, 0.1, 0.9, 400, 2200, 0.9);
				Strike.Whoosh(p, 0.55, 0.35, 500, 2500, 0.45);
				Strike.Hit(p, 0.62, 0.4, A4, 0.5);
				p.Reverb(0.12, 0.9);
			}, levelDb: -1.5);

			yield return r.With(Mix.Epic, "light_grand", "Luz: grande magia. O sigilo, o coro abrindo e o acorde de sinos.", p =>
			{
				Arcane.Sigil(p, 0, 0.45);
				Arcane.Choir(p, new[] { E4, B4, E5 }, 0.2, 0.25, 0.3, 0.6, 0.45);
				Arcane.Chord(p, new[] { E5, G5, B5, E6 }, 0.45, 0.03, 1.2, 0.9);
				Arcane.Sparkle(p, 0.45, 0.6, 6, E6, B6, 0.25);
				Strike.Hit(p, 0.45, 0.3, E4, 0.4);
				p.Reverb(0.18, 1.2);
			}, levelDb: -2);

			yield return r.With(Mix.Epic, "dark_grand", "Trevas: grande magia. O sigilo, o coro em \"u\", a sombra subindo e o golpe.", p =>
			{
				Arcane.Sigil(p, 0, 0.4, G4);
				Arcane.Choir(p, new[] { E3, B3 }, 0.1, 0.4, 0.2, 0.6, 0.45, Vowel.U, Vowel.O);
				Elemental.Umbra(p, 0.1, 0.8, E2, 0.5);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.4), 0.25).Bandpass(500, 1.2).Lowpass(1500).Reverse().Gain(0.4);
				Strike.Bloom(p, 0.65, 0.6, 0.7);
				Strike.Hit(p, 0.65, 0.8, E3, 0.6);
				p.Reverb(0.18, 1.2);
			}, levelDb: -1.5);
		}
	}
}
