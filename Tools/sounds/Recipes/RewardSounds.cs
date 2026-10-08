using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// As recompensas: ataque claro (moeda, vidro, marimba) e o que é: moedas para Ouro, taça e bolhas para
	/// Essência, cacos para Fragmentos, pedra entalhada para runa. A raridade abre o acorde.
	/// </summary>
	internal static class RewardSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("rewards", Mix.Reward);

			yield return r.Of("gold", "Ganhar ouro: moedas caindo umas sobre as outras.", p =>
			{
				var coins = 3 + p.Variation;
				for (var i = 0; i < coins; i++)
					Foley.Coin(p, i * p.Vary(0.045, 0.3), p.Vary(3000, 0.15), p.Rng.Range(0.5, 1));
				p.Reverb(0.05, 0.4);
			}, 3);

			yield return r.Of("essence", "Ganhar essência: a taça aguda, o vidro e bolhas de luz.", p =>
			{
				Arcane.Bowl(p, A5, 0, 0.6, 0.45);
				Arcane.Glass(p, D7, 0.03, 0.4, 0.4);
				Elemental.Bubbles(p, 0, 0.2, 3, 700, 1400, 0.3);
				Arcane.Celesta(p, A6, 0.08, 0.3, 0.35);
				p.Reverb(0.1, 0.6);
			});

			yield return r.Of("fragments", "Ganhar fragmentos: cacos de cristal e a fivela.", p =>
			{
				for (var i = 0; i < 4; i++)
					Arcane.Glass(p, Snap(p.Rng.Range(D7, A7), Pentatonic), i * 0.04, 0.18, 0.5);
				Foley.Clink(p, 0, 3000, 0.3);
				Arcane.Celesta(p, D6, 0.14, 0.3, 0.35);
				p.Reverb(0.08, 0.5);
			});

			yield return r.Of("experience", "Ganhar experiência: a escala subindo em celesta.", p =>
			{
				Arcane.Arpeggio(p, new[] { D6, E6, Fs6, A6 }, 0, 0.035, (note, at, _) => Arcane.Celesta(p, note, at, 0.25, 0.5));
				Arcane.Glint(p, D7, 0.14, 0.4);
				p.Reverb(0.08, 0.5);
			});

			yield return r.Of("equipment", "Ganhar equipamento: couro, metal e a quinta na marimba.", p =>
			{
				Foley.Leather(p, 0, 0.6);
				Foley.Clink(p, 0.02, 2000, 0.5);
				Foley.Knock(p, 0.02, 800, 0.05, 0.5);
				Arcane.Marimba(p, D5, 0.06, 0.25, 0.5);
				Arcane.Marimba(p, A5, 0.11, 0.25, 0.5);
				p.Reverb(0.06, 0.4);
			});

			yield return r.Of("rune", "Ganhar runa: a pedra entalhada e o vidro acendendo.", p =>
			{
				p.Modal(Modes.Stone, p.Vary(720, 0.03), 0.35).Gain(0.8);
				Arcane.Glass(p, A6, 0.03, 0.6, 0.45);
				Arcane.Glass(p, E7, 0.07, 0.5, 0.3);
				Arcane.Shimmer(p, 0.05, 0.4, Hz(D6), 0.2);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("item_rare", "Item raro: três sinos e um lampejo.", p =>
			{
				Arcane.Chord(p, new[] { D6, Fs6, A6 }, 0, 0.035, 0.8, 0.8);
				Arcane.Glint(p, D7, 0.1, 0.45);
				p.Reverb(0.1, 0.7);
			});

			yield return r.Of("item_epic", "Item épico: quatro sinos, tapete e brilhos.", p =>
			{
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6 }, 0, 0.035, 1.1, 0.9);
				Arcane.Air(p, new[] { D5, A5 }, 0, 0.15, 0.2, 0.6, 0.3);
				Arcane.Sparkle(p, 0.1, 0.5, 8, D6, D7, 0.3, Major);
				p.Reverb(0.15, 0.9);
			}, levelDb: 1);

			yield return r.With(Mix.Epic, "item_legendary", "Item lendário: subida, cinco sinos, coro, moeda e chuva de brilhos.", p =>
			{
				Arcane.Riser(p, 0, 0.35, Hz(A4), Hz(D5), 0.35);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6, A6 }, 0.35, 0.035, 1.6, 1);
				Arcane.Choir(p, new[] { D4, A4, Fs5 }, 0.32, 0.08, 0.35, 0.9, 0.5);
				Foley.Coin(p, 0.35, 3200, 0.4);
				Arcane.Sparkle(p, 0.35, 1.0, 16, D6, A7, 0.35, Major);
				p.Reverb(0.2, 1.4);
			});

			yield return r.Of("level_up", "Subir de nível: o acorde subindo na marimba, sinos e um coro curto.", p =>
			{
				Arcane.Arpeggio(p, new[] { D5, Fs5, A5, D6 }, 0, 0.06, (note, at, _) => Arcane.Marimba(p, note, at, 0.3, 0.6));
				Arcane.Bell(p, D6, 0.24, 1, 0.5);
				Arcane.Bell(p, A6, 0.27, 0.9, 0.35);
				Arcane.Choir(p, new[] { D5, Fs5, A5 }, 0.22, 0.05, 0.2, 0.6, 0.35);
				Arcane.Sparkle(p, 0.25, 0.5, 8, D6, D7, 0.3, Major);
				p.Reverb(0.15, 1);
			}, levelDb: 1);

			yield return r.Of("star_up", "Subir estrela: dois lampejos e o sino.", p =>
			{
				Arcane.Glint(p, D6, 0, 0.6);
				Arcane.Glint(p, A6, 0.08, 0.6);
				Arcane.Bell(p, D6, 0.16, 0.8, 0.5);
				Arcane.Sparkle(p, 0.18, 0.4, 6, D6, D7, 0.3);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("skill_unlock", "Desbloquear habilidade: o sigilo e o acorde Ré–Lá–Mi.", p =>
			{
				Arcane.Sigil(p, 0, 0.8);
				Arcane.Chord(p, new[] { D5, A5, E6 }, 0.12, 0.03, 0.8, 0.6);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("creature_unlock", "Desbloquear criatura: a trava, quatro sinos e o coro.", p =>
			{
				Foley.Clink(p, 0, 2200, 0.4);
				Arcane.Chord(p, new[] { D5, Fs5, A5, D6 }, 0.05, 0.04, 1.0, 0.8);
				Arcane.Choir(p, new[] { D4, A4 }, 0.05, 0.08, 0.2, 0.6, 0.35);
				Arcane.Sparkle(p, 0.1, 0.5, 6, D6, D7, 0.3, Major);
				p.Reverb(0.15, 1);
			});

			yield return r.Of("quest_complete", "Completar missão: \"tã-tã-TÃ\" na marimba e o sino.", p =>
			{
				Foley.Knock(p, 0, 900, 0.04, 0.3);
				Arcane.Marimba(p, D5, 0, 0.2, 0.6);
				Arcane.Marimba(p, A5, 0.1, 0.2, 0.6);
				Arcane.Marimba(p, D6, 0.2, 0.4, 0.7);
				Arcane.Bell(p, D6, 0.2, 0.9, 0.5);
				Arcane.Bell(p, Fs6, 0.22, 0.8, 0.3);
				p.Reverb(0.12, 0.9);
			});

			yield return r.Of("achievement_complete", "Completar conquista: acorde de sinos, celesta subindo e moeda.", p =>
			{
				Arcane.Chord(p, new[] { D5, Fs5, A5, D6 }, 0, 0.04, 1.0, 0.8);
				Arcane.Arpeggio(p, new[] { A6, D7, Fs7 }, 0.08, 0.05, (note, at, _) => Arcane.Celesta(p, note, at, 0.3, 0.35));
				Foley.Coin(p, 0.05, 3400, 0.35);
				Arcane.Sparkle(p, 0.1, 0.6, 8, D6, D7, 0.3, Major);
				p.Reverb(0.15, 1);
			});

			yield return r.Of("daily_reward", "Recompensa diária: o embrulho de papel, a escala e a moeda.", p =>
			{
				Foley.Paper(p, 0, 0.18, 0.4);
				Arcane.Arpeggio(p, new[] { D6, E6, Fs6, A6, B6 }, 0.1, 0.045, (note, at, _) => Arcane.Celesta(p, note, at, 0.3, 0.5));
				Foley.Coin(p, 0.32, 3000, 0.4);
				Arcane.Bell(p, D6, 0.32, 0.8, 0.35);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("chest_open", "Baú abrindo: a trava, a dobradiça, a tampa e o brilho de dentro.", p =>
			{
				Chest(p);
				p.Reverb(0.08, 0.6);
			});

			yield return r.Of("chest_rare", "Baú raro: o baú e três sinos.", p =>
			{
				Chest(p);
				Arcane.Chord(p, new[] { D6, Fs6, A6 }, 0.36, 0.03, 0.9, 0.7);
				Arcane.Sparkle(p, 0.36, 0.5, 6, D6, D7, 0.3, Major);
				p.Reverb(0.12, 0.8);
			}, levelDb: 1);

			yield return r.With(Mix.Epic, "chest_legendary", "Baú lendário: o baú, a subida, cinco sinos, coro e moedas.", p =>
			{
				Chest(p);
				Arcane.Riser(p, 0.02, 0.34, Hz(A4), Hz(D5), 0.3);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6, A6 }, 0.36, 0.035, 1.6, 0.9);
				Arcane.Choir(p, new[] { D4, A4, Fs5 }, 0.34, 0.08, 0.35, 0.9, 0.5);
				for (var i = 0; i < 4; i++)
					Foley.Coin(p, 0.4 + i * p.Vary(0.05, 0.3), p.Vary(3000, 0.15), 0.35);
				Arcane.Sparkle(p, 0.36, 1.0, 16, D6, A7, 0.35, Major);
				p.Reverb(0.2, 1.4);
			});
		}

		/// <summary>O baú: a trava, a dobradiça rangendo, a tampa batendo para trás e um brilho com moeda.</summary>
		private static void Chest(Patch p)
		{
			Foley.Clink(p, 0, 1800, 0.5);
			Foley.Creak(p, 0.04, 0.35, 800, 0.35);
			Foley.Knock(p, 0.36, 450, 0.08, 0.6);
			Foley.Thud(p, 0.36, 140, 0.4);
			Arcane.Sparkle(p, 0.3, 0.4, 5, D6, D7, 0.3, Major);
			Foley.Coin(p, 0.42, 3000, 0.3);
		}
	}
}
