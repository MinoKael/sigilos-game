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
	/// O grimório: o livro respira ao abrir, as páginas viram macias, a tinta mágica brilha baixinho. Nada
	/// de rangido nem de capa batendo: o mistério está na harpa e na taça, não no peso do livro.
	/// </summary>
	internal static class GrimoireSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("grimoire", Mix.Soft);

			yield return r.Of("book_open", "Abrir grimório: o livro respira, a taça acorda e a harpa abre Mi menor com sétima.", p =>
			{
				p.Noise(NoiseColor.Pink, Envelope.Swell(0.4, 0.5)).Bandpass(400, 1, 1300).Gain(0.25);
				Arcane.Bowl(p, E4, 0.05, 1.1, 0.35);
				Arcane.Arpeggio(p, new[] { E5, G5, B5, D6 }, 0.25, 0.09, (note, at, _) => Arcane.Harp(p, note, at, 0.8, 0.55));
				Arcane.Celesta(p, E6, 0.62, 0.6, 0.25);
				p.Reverb(0.18, 1.2);
			}, levelDb: -1.5);

			yield return r.Of("book_close", "Fechar grimório: o ar empurrado, a capa encostando no feltro e Si descendo a Mi.", p =>
			{
				Foley.Swish(p, 0, 0.14, 1400, 400, 0.35);
				Foley.Felt(p, 0.1, 95, 0.14, 0.5);
				Arcane.Harp(p, B4, 0.1, 0.35, 0.4);
				Arcane.Harp(p, E4, 0.16, 0.5, 0.4);
				p.Reverb(0.1, 0.6);
			}, levelDb: -1.5);

			yield return r.Of("page_turn", "Virar página: a folha passando no ar, macia.", p => Foley.PageFlip(p, 0, p.Vary(0.22, 0.15), 1), 3, -3);

			yield return r.Of("page_select", "Escolher página: o dedo na folha e uma nota de celesta.", p =>
			{
				Foley.Swish(p, 0, 0.06, 900, 2200, 0.15);
				Arcane.Celesta(p, Pick(p, B5, A5), 0.01, 0.25, 0.45);
			}, 2, -2);

			yield return r.Of("magic_ink", "Tinta mágica: duas bolhas no tinteiro e a celesta brilhando baixinho.", p =>
			{
				Elemental.Bubbles(p, 0, 0.18, 2, 450, 800, 0.35);
				Arcane.Celesta(p, Pick(p, G6, A6), 0.04, 0.35, 0.45);
				Arcane.Celesta(p, B6, 0.1, 0.4, 0.3);
				p.Reverb(0.12, 0.6);
			}, 2);

			yield return r.Of("creature_registered", "Nova criatura no grimório: o carimbo de feltro, o sino e o coro curto.", p =>
			{
				Foley.Felt(p, 0, 120, 0.15, 0.6);
				Arcane.Bell(p, E5, 0.04, 0.9, 0.5);
				Arcane.Bell(p, B5, 0.11, 0.8, 0.35);
				Arcane.Choir(p, new[] { E4, B4 }, 0.04, 0.1, 0.15, 0.45, 0.3);
				p.Reverb(0.15, 0.9);
			}, levelDb: -2);
		}
	}
}
