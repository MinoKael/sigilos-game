using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// O chefe: mais grave e mais largo que o resto, nunca mais alto. Tambores de feltro, a taça grave, a
	/// sombra cantando em quinta. Três momentos só: a entrada, a habilidade e a queda.
	/// </summary>
	internal static class BossSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat/boss", Mix.Epic);

			yield return r.Of("enter", "Chefe entrando: dois passos de feltro, a taça grave e a sombra.", p =>
			{
				Foley.Felt(p, 0, 62, 0.4, 0.9);
				Foley.Felt(p, 0.42, 58, 0.45, 0.9);
				Arcane.Bowl(p, E3, 0.8, 1.6, 0.6);
				Arcane.Air(p, new[] { E3, B3 }, 0.75, 0.3, 0.4, 0.8, 0.35);
				Elemental.Umbra(p, 0.8, 0.9, E2, 0.35);
				p.Reverb(0.16, 1.3);
			}, levelDb: -3);

			yield return r.Of("special", "Chefe usando habilidade especial: a subida grave, a sombra e a florada.", p =>
			{
				Arcane.Riser(p, 0, 0.6, Hz(E3), Hz(B3), 0.45);
				Elemental.Umbra(p, 0.15, 0.55, E3, 0.35);
				Strike.Bloom(p, 0.6, 0.5, 0.8);
				Strike.Hit(p, 0.6, 0.85, E3, 0.6);
				p.Reverb(0.14, 1.0);
			}, levelDb: -0.5);

			yield return r.Of("defeated", "Chefe derrotado: a taça descendo a Mi, a florada e o coro se abrindo.", p =>
			{
				Arcane.Bowl(p, B3, 0, 0.7, 0.5);
				Arcane.Bowl(p, E3, 0.3, 1.3, 0.6);
				Strike.Bloom(p, 0.3, 0.8, 0.8);
				Arcane.Choir(p, new[] { E4, B4, E5 }, 0.45, 0.35, 0.25, 0.8, 0.35, Vowel.O, Vowel.A);
				p.Reverb(0.18, 1.3);
			}, levelDb: 2);
		}
	}
}
