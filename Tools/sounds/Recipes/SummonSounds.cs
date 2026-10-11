using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// A invocação: o sigilo acende sobre a taça, a energia sobe e a criatura chega. A raridade sobe em
	/// degraus de camadas (mais vozes no acorde, coro, a subida antes, mais sininhos), não de volume; a
	/// revelação tem uma versão por estrela (<c>reveal_star_1</c> a <c>reveal_star_5</c>).
	/// </summary>
	internal static class SummonSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("summon", Mix.Reward);

			yield return r.Of("summon_start", "Iniciar invocação: o sigilo acende sobre a taça grave.", p =>
			{
				Arcane.Sigil(p, 0, 0.7);
				Arcane.Bowl(p, E4, 0.02, 1.0, 0.45);
				Arcane.Riser(p, 0.05, 0.45, Hz(E4), Hz(B4), 0.25);
				p.Reverb(0.15, 0.9);
			}, levelDb: -2.5);

			yield return r.Of("energy_build", "Energia acumulando: a quinta subindo e o pulso acelerando.", p =>
			{
				Arcane.Riser(p, 0, 1.2, Hz(E4), Hz(E5), 0.45);
				p.Tone(Wave.Saw, Hz(E3), Envelope.Swell(1.15, 0.1)).Unison(3, 12).Lowpass(450, 1800).Tremolo(4, 0.5, 16).Gain(0.3);
				p.Reverb(0.1, 0.6);
			}, levelDb: -3.5);

			yield return r.Of("creature_summoned", "Criatura invocada: a chegada de feltro, o sino e o acorde de Sol maior.", p =>
			{
				Foley.Felt(p, 0, 110, 0.15, 0.4);
				Arcane.Bell(p, E5, 0, 1.0, 0.5);
				Arcane.Chord(p, new[] { G5, B5, D6 }, 0.04, 0.03, 0.9, 0.6);
				Arcane.Sparkle(p, 0.05, 0.4, 4, E6, B6, 0.25);
				p.Reverb(0.15, 0.9);
			}, levelDb: -2);

			yield return r.Of("reveal_star_1", "Revelação 1★: uma nota de celesta.", p => Stars(p, 1), levelDb: -1);
			yield return r.Of("reveal_star_2", "Revelação 2★: a quarta em celesta.", p => Stars(p, 2), levelDb: -3);
			yield return r.Of("reveal_star_3", "Revelação 3★: o acorde de Sol maior em sinos.", p => Stars(p, 3), levelDb: -1.5);
			yield return r.Of("reveal_star_4", "Revelação 4★: subida curta, quatro sinos e coro.", p => Stars(p, 4), levelDb: -1.5);
			yield return r.With(Mix.Epic, "reveal_star_5", "Revelação 5★: a subida inteira, o sigilo, coro e seis sinos.", p => Stars(p, 5), levelDb: 1);
		}

		/// <summary>A revelação de uma criatura de <paramref name="stars"/> estrelas: cada degrau põe uma camada.</summary>
		private static void Stars(Patch p, int stars)
		{
			switch (stars)
			{
				case 1:
					Arcane.Celesta(p, B5, 0, 0.4, 0.6);
					p.Reverb(0.08, 0.5);
					break;
				case 2:
					Arcane.Celesta(p, B5, 0, 0.4, 0.6);
					Arcane.Celesta(p, E6, 0.07, 0.45, 0.5);
					p.Reverb(0.1, 0.6);
					break;
				case 3:
					Arcane.Chord(p, new[] { G5, B5, D6 }, 0, 0.04, 0.9, 0.8);
					Arcane.Sparkle(p, 0.05, 0.3, 3, E6, B6, 0.2);
					p.Reverb(0.12, 0.8);
					break;
				case 4:
					Arcane.Riser(p, 0, 0.25, Hz(B4), Hz(E5), 0.25);
					Arcane.Chord(p, new[] { E5, B5, E6, G6 }, 0.25, 0.035, 1.1, 0.9);
					Arcane.Choir(p, new[] { E4, B4, G5 }, 0.22, 0.06, 0.2, 0.5, 0.35);
					Arcane.Sparkle(p, 0.25, 0.5, 6, E6, B6, 0.25);
					p.Reverb(0.16, 1.0);
					break;
				default:
					Arcane.Riser(p, 0, 0.45, Hz(E4), Hz(E5), 0.4);
					Strike.Bloom(p, 0.45, 0.3, 0.35);
					Arcane.Sigil(p, 0.45, 0.7);
					Arcane.Choir(p, new[] { G4, D5, G5, B5 }, 0.42, 0.1, 0.35, 0.8, 0.55);
					Arcane.Chord(p, new[] { G4, D5, G5, B5, D6, G6 }, 0.45, 0.04, 1.2, 1);
					Arcane.Sparkle(p, 0.45, 0.8, 10, E6, D7, 0.3);
					p.Reverb(0.2, 1.1);
					break;
			}
		}
	}
}
