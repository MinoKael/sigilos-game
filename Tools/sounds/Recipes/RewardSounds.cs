using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// As recompensas, do miúdo ao raro: moedas macias para Ouro, a taça para Essência, a pedra para a runa,
	/// e a raridade abrindo o acorde (três sinos, quatro com tapete, cinco com coro). Os que se repetem no
	/// farm são os mais curtos e baixos.
	/// </summary>
	internal static class RewardSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("rewards", Mix.Reward);

			yield return r.Of("gold", "Ganhar ouro: duas a quatro moedas macias.", p =>
			{
				var coins = 2 + p.Variation;
				for (var i = 0; i < coins; i++)
					Foley.Coin(p, i * p.Vary(0.05, 0.3), p.Vary(2600, 0.12), p.Rng.Range(0.5, 1));
				p.Reverb(0.05, 0.4);
			}, 3, -3);

			yield return r.Of("essence", "Ganhar essência: a taça, bolhas de luz e a celesta.", p =>
			{
				Arcane.Bowl(p, E5, 0, 0.6, 0.45);
				Elemental.Bubbles(p, 0, 0.2, 3, 600, 1100, 0.25);
				Arcane.Celesta(p, B6, 0.03, 0.4, 0.35);
				Arcane.Celesta(p, E6, 0.08, 0.3, 0.3);
				p.Reverb(0.1, 0.6);
			}, levelDb: -2);

			yield return r.Of("experience", "Ganhar experiência: a pentatônica subindo em celesta.", p =>
			{
				Arcane.Arpeggio(p, new[] { E6, G6, A6, B6 }, 0, 0.035, (note, at, _) => Arcane.Celesta(p, note, at, 0.25, 0.45));
				p.Reverb(0.08, 0.5);
			}, levelDb: -4.5);

			yield return r.Of("rune", "Ganhar runa: a pedra entalhada e a celesta acendendo.", p =>
			{
				p.Modal(Modes.Stone, p.Vary(640, 0.03), 0.3, 0, 0.002).Lowpass(3000).Gain(0.6);
				Arcane.Celesta(p, B5, 0.03, 0.5, 0.45);
				Arcane.Celesta(p, E6, 0.07, 0.45, 0.3);
				p.Reverb(0.12, 0.7);
			}, levelDb: -2.5);

			yield return r.Of("item_rare", "Item raro: três sinos em Sol maior e a celesta.", p =>
			{
				Arcane.Chord(p, new[] { G5, B5, D6 }, 0, 0.035, 0.8, 0.8);
				Arcane.Celesta(p, G6, 0.1, 0.4, 0.3);
				p.Reverb(0.1, 0.7);
			}, levelDb: -2);

			yield return r.Of("item_epic", "Item épico: quatro sinos em Mi, o tapete e os sininhos.", p =>
			{
				Arcane.Chord(p, new[] { E5, B5, E6, G6 }, 0, 0.035, 1.0, 0.9);
				Arcane.Air(p, new[] { E4, B4 }, 0, 0.15, 0.2, 0.6, 0.3);
				Arcane.Sparkle(p, 0.1, 0.5, 5, E6, B6, 0.25);
				p.Reverb(0.15, 0.9);
			}, levelDb: -2.5);

			yield return r.With(Mix.Epic, "item_legendary", "Item lendário: subida, cinco sinos em Sol maior, coro, moeda e sininhos.", p =>
			{
				Arcane.Riser(p, 0, 0.3, Hz(B4), Hz(E5), 0.3);
				Arcane.Chord(p, new[] { G4, D5, G5, B5, D6 }, 0.3, 0.035, 1.4, 1);
				Arcane.Choir(p, new[] { G4, D5, B5 }, 0.28, 0.08, 0.3, 0.8, 0.45);
				Foley.Coin(p, 0.3, 2600, 0.3);
				Arcane.Sparkle(p, 0.3, 0.8, 10, E6, D7, 0.3);
				p.Reverb(0.2, 1.3);
			}, levelDb: -1.5);

			yield return r.Of("level_up", "Subir de nível: a harpa subindo o acorde de Mi, sinos e um coro curto.", p =>
			{
				Arcane.Arpeggio(p, new[] { E5, G5, B5, E6 }, 0, 0.06, (note, at, _) => Arcane.Harp(p, note, at, 0.6, 0.6));
				Arcane.Bell(p, E6, 0.24, 0.9, 0.45);
				Arcane.Bell(p, B6, 0.27, 0.8, 0.3);
				Arcane.Choir(p, new[] { E5, G5, B5 }, 0.22, 0.05, 0.2, 0.5, 0.3);
				Arcane.Sparkle(p, 0.25, 0.4, 5, E6, B6, 0.2);
				p.Reverb(0.15, 1.0);
			}, levelDb: -2.5);

			yield return r.Of("star_up", "Subir estrela (e despertar, fundir): duas celestas e o sino.", p =>
			{
				Arcane.Celesta(p, B5, 0, 0.4, 0.55);
				Arcane.Celesta(p, E6, 0.08, 0.45, 0.55);
				Arcane.Bell(p, E6, 0.16, 0.8, 0.4);
				Arcane.Sparkle(p, 0.18, 0.35, 4, E6, B6, 0.2);
				p.Reverb(0.15, 0.9);
			}, levelDb: -1.5);

			yield return r.Of("daily_reward", "Recompensa: o embrulho abrindo, a pentatônica em celesta e a moeda.", p =>
			{
				Foley.Swish(p, 0, 0.18, 700, 2000, 0.2);
				Arcane.Arpeggio(p, new[] { E6, G6, A6, B6, D7 }, 0.08, 0.045, (note, at, _) => Arcane.Celesta(p, note, at, 0.3, 0.45));
				Foley.Coin(p, 0.3, 2600, 0.3);
				Arcane.Bell(p, E6, 0.3, 0.8, 0.3);
				p.Reverb(0.12, 0.8);
			}, levelDb: -4);
		}
	}
}
