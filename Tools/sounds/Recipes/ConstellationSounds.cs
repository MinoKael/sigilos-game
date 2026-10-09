using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using static Sigilos.Sounds.Library.Recipe;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// As constelações: uma estrela acende com dois sininhos em quinta ou quarta, cada variação num par da
	/// pentatônica, para um caminho de estrelas soar como uma melodia da música.
	/// </summary>
	internal static class ConstellationSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("constellation", Mix.Soft);

			yield return r.Of("star_activate", "Ativar estrela: dois sininhos e o sopro do céu.", p =>
			{
				var note = Pick(p, E6, G6, A6);
				Foley.Swish(p, 0, 0.25, 900, 2400, 0.1);
				Arcane.Celesta(p, note, 0, 0.45, 0.6);
				Arcane.Celesta(p, Pick(p, B6, D7, E7), 0.07, 0.5, 0.35);
				Arcane.Bell(p, note - 12, 0.02, 0.6, 0.25);
				p.Reverb(0.15, 0.8);
			}, 3);
		}
	}
}
