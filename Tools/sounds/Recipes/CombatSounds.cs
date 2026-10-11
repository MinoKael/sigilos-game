using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using static Sigilos.Sounds.Library.Recipe;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// O golpe e a queda, os sons que mais tocam no jogo: feltro com uma nota de madeira na pentatônica, cada
	/// variação numa nota, para cem golpes seguidos virarem uma frase e não um martelo.
	/// </summary>
	internal static class CombatSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat", Mix.Combat);

			yield return r.Of("hit", "Golpe: o feltro e uma nota de madeira.", p => Strike.Hit(p, 0, 0.3, Pick(p, E4, G4, B4, A4), 1), 4);

			yield return r.Of("hit_heavy", "Golpe pesado (chefe, Bomba): o feltro grave, a nota baixa e o ar abrindo.", p =>
			{
				Strike.Hit(p, 0, 0.8, Pick(p, E3, B2), 1);
				Strike.Bloom(p, 0.01, 0.2, 0.35);
			}, 2, 0.5);

			yield return r.Of("knockout", "Nocaute: a harpa descendo até Mi e o corpo no chão, macio.", p =>
			{
				Arcane.Harp(p, Pick(p, B4, G4), 0, 0.5, 0.8);
				Arcane.Harp(p, E4, 0.11, 0.7, 0.8);
				Foley.Felt(p, 0.1, p.Vary(105, 0.05), 0.16, 0.6);
				p.Reverb(0.12, 0.7);
			}, 2, -3);
		}
	}
}
