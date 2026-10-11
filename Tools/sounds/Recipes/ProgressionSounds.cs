using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// O andamento da luta, na harpa da música: o turno é uma quinta baixinha, a onda nova um arpejo, e o fim
	/// sobe em Sol maior (vitória) ou desce até Mi (derrota). O combate automático liga e desliga numa quinta.
	/// </summary>
	internal static class ProgressionSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("progression", Mix.Soft);

			yield return r.Of("turn_start", "Sua vez: Mi e Si na harpa, baixinho.", p =>
			{
				Arcane.Harp(p, E5, 0, 0.3, 0.7);
				Arcane.Harp(p, B5, 0.06, 0.35, 0.6);
			}, levelDb: -4);

			yield return r.Of("new_wave", "Nova onda: um passo de feltro e o arpejo de Mi subindo até o sino.", p =>
			{
				Foley.Felt(p, 0, 70, 0.25, 0.5);
				Arcane.Arpeggio(p, new[] { E4, G4, B4, E5 }, 0.04, 0.06, (note, at, _) => Arcane.Harp(p, note, at, 0.6, 0.6));
				Arcane.Bell(p, E5, 0.24, 0.8, 0.35);
				p.Reverb(0.12, 0.9);
			}, levelDb: -2);

			yield return r.With(Mix.Epic, "victory", "Vitória: a harpa subindo em Sol maior, o acorde de sinos e os sininhos.", p =>
			{
				Arcane.Arpeggio(p, new[] { G4, B4, D5, G5 }, 0, 0.08, (note, at, _) => Arcane.Harp(p, note, at, 0.9, 0.7));
				Arcane.Chord(p, new[] { G5, B5, D6 }, 0.32, 0.03, 1.2, 0.7);
				Arcane.Air(p, new[] { G4, D5 }, 0.3, 0.2, 0.4, 0.8, 0.3);
				Arcane.Sparkle(p, 0.35, 0.6, 5, G6, D7, 0.2);
				p.Reverb(0.18, 1.2);
			}, levelDb: -1);

			yield return r.With(Mix.Epic, "defeat", "Derrota: a harpa descendo devagar até Mi sobre um tapete grave.", p =>
			{
				Arcane.Arpeggio(p, new[] { E5, D5, B4, G4 }, 0, 0.22, (note, at, _) => Arcane.Harp(p, note, at, 0.8, 0.65));
				Arcane.Harp(p, E4, 0.9, 1.4, 0.7);
				Arcane.Air(p, new[] { E3, B3 }, 0.7, 0.4, 0.4, 1.0, 0.35);
				p.Reverb(0.18, 1.3);
			}, levelDb: -2);

			yield return r.Of("auto_battle_start", "Combate automático ligado: a quinta subindo na harpa.", p =>
			{
				Arcane.Harp(p, E5, 0, 0.3, 0.7);
				Arcane.Harp(p, B5, 0.07, 0.4, 0.7);
				Arcane.Celesta(p, E6, 0.14, 0.3, 0.25);
				p.Reverb(0.1, 0.6);
			}, levelDb: -2);

			yield return r.Of("auto_battle_end", "Combate automático desligado: a quinta descendo na harpa.", p =>
			{
				Arcane.Harp(p, B5, 0, 0.3, 0.7);
				Arcane.Harp(p, E5, 0.07, 0.45, 0.7);
				p.Reverb(0.1, 0.6);
			}, levelDb: -1);
		}
	}
}
