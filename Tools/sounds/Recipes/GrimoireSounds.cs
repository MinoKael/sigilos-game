using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// O grimório: capa de couro, lombada rangendo, páginas, pena e tinta. A magia entra por cima, em celesta,
	/// vidro e coro, e cresce do registro de uma página até o livro completo.
	/// </summary>
	internal static class GrimoireSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("grimoire", Mix.Soft);

			yield return r.Of("book_open", "Abrir grimório: a lombada rangendo, a capa caindo aberta e duas notas de celesta.", p =>
			{
				Foley.Creak(p, 0, 0.3, 700, 0.35);
				Foley.Leather(p, 0.02, 0.5);
				Foley.Thud(p, 0.2, 130, 0.7);
				Foley.Paper(p, 0.18, 0.3, 0.35);
				Arcane.Celesta(p, D6, 0.3, 0.4, 0.3);
				Arcane.Celesta(p, A6, 0.36, 0.5, 0.25);
				p.Reverb(0.1, 0.6);
			});

			yield return r.Of("book_close", "Fechar grimório: o ar empurrado e a capa fechando.", p =>
			{
				Foley.Swish(p, 0, 0.12, 1800, 500, 0.5);
				Foley.Thud(p, 0.1, 115, 1);
				Foley.Leather(p, 0.1, 0.5);
				Foley.Paper(p, 0.06, 0.08, 0.2);
				p.Reverb(0.08, 0.5);
			});

			yield return r.Of("page_turn", "Virar página.", p => Foley.PageFlip(p, 0, p.Vary(0.22, 0.15), 1), 3);

			yield return r.Of("pages_riffle", "Virar várias páginas rapidamente.", p =>
			{
				for (var i = 0; i < 8; i++)
					Foley.PageFlip(p, i * p.Vary(0.045, 0.15), p.Vary(0.08, 0.2), 1 - i * 0.06);
				Foley.Paper(p, 0, 0.45, 0.35);
				Foley.Swish(p, 0, 0.45, 800, 2400, 0.25);
			});

			yield return r.Of("page_select", "Selecionar página: o dedo na folha e uma nota.", p =>
			{
				Foley.Knock(p, 0, 1500, 0.025, 0.4);
				Foley.Paper(p, 0, 0.06, 0.35);
				Arcane.Celesta(p, A6, 0.01, 0.25, 0.35);
			});

			yield return r.Of("write", "Escrever: a pena riscando em três traços.", p =>
			{
				Foley.Quill(p, 0, 0.25, 1);
				Foley.Quill(p, 0.3, 0.18, 0.8);
				Foley.Quill(p, 0.52, 0.3, 0.9);
			});

			yield return r.Of("trace_symbol", "Traçar símbolo: a pena e um rastro de luz que sobe até a quinta.", p =>
			{
				Foley.Quill(p, 0, 0.55, 0.8);
				p.Tone(Wave.Triangle, Hz(D6), Envelope.Swell(0.35, 0.25)).Glide(Hz(A6)).Vibrato(5, 0.1).Gain(0.3);
				Arcane.Sparkle(p, 0.45, 0.25, 4, A6, E7, 0.35);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("magic_ink", "Tinta mágica: bolhas no tinteiro e um brilho tremendo.", p =>
			{
				Elemental.Bubbles(p, 0, 0.25, 4, 500, 1100, 0.5);
				Elemental.Slosh(p, 0, 0.35, 0.35);
				Arcane.Shimmer(p, 0.05, 0.5, Hz(D6), 0.4);
				Arcane.Glass(p, A6, 0.05, 0.4, 0.3);
				p.Reverb(0.12, 0.6);
			});

			yield return r.Of("reveal_text", "Revelar texto: um vidro crescendo ao contrário até a quinta.", p =>
			{
				p.Modal(Modes.Glass, Hz(A5), 0.45).Reverse().Gain(0.6);
				Arcane.Celesta(p, D6, 0.45, 0.4, 0.5);
				Arcane.Celesta(p, A6, 0.5, 0.4, 0.4);
				Foley.Paper(p, 0.4, 0.15, 0.2);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("magic_text_appear", "Texto mágico aparecendo: brilhos em Ré lídio sobre um tapete leve.", p =>
			{
				Arcane.Sparkle(p, 0, 0.45, 9, D6, D7, 0.4);
				Arcane.Air(p, new[] { D5, A5 }, 0, 0.15, 0.15, 0.35, 0.3);
				Foley.Quill(p, 0, 0.4, 0.25);
				p.Reverb(0.15, 0.8);
			});

			yield return r.Of("entry_discovered", "Descobrir entrada: a página, a taça e um arpejo.", p =>
			{
				Foley.PageFlip(p, 0, 0.16, 0.6);
				Arcane.Bowl(p, D5, 0.1, 0.9, 0.45);
				Arcane.Arpeggio(p, new[] { D6, Fs6, A6 }, 0.12, 0.06, (note, at, _) => Arcane.Celesta(p, note, at, 0.35, 0.45));
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("creature_registered", "Nova criatura registrada: o carimbo, um coro curto e o sino.", p =>
			{
				Foley.Thud(p, 0, 150, 0.8);
				Foley.Knock(p, 0, 600, 0.06, 0.5);
				Arcane.Choir(p, new[] { D4, A4, D5 }, 0.04, 0.08, 0.15, 0.4, 0.4);
				Arcane.Bell(p, D6, 0.05, 0.8, 0.45);
				Arcane.Bell(p, A6, 0.12, 0.7, 0.3);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("skill_discovered", "Nova habilidade descoberta: o sigilo acendendo na página.", p =>
			{
				Arcane.Sigil(p, 0, 0.9);
				Arcane.Celesta(p, A6, 0.15, 0.5, 0.35);
				Foley.Paper(p, 0, 0.1, 0.2);
				p.Reverb(0.15, 0.8);
			});

			yield return r.Of("info_unlocked", "Nova informação desbloqueada: o fecho abrindo e duas notas.", p =>
			{
				Foley.Clink(p, 0, 2400, 0.35);
				Foley.Paper(p, 0.02, 0.1, 0.25);
				Arcane.Celesta(p, E6, 0.05, 0.3, 0.45);
				Arcane.Celesta(p, A6, 0.1, 0.35, 0.4);
				p.Reverb(0.1, 0.6);
			});

			yield return r.Of("page_complete", "Completar página: a marimba subindo o acorde e o sino.", p =>
			{
				Foley.PageFlip(p, 0, 0.14, 0.5);
				Arcane.Arpeggio(p, new[] { D5, Fs5, A5, D6 }, 0.05, 0.05, (note, at, _) => Arcane.Marimba(p, note, at, 0.25, 0.5));
				Arcane.Bell(p, D6, 0.22, 0.8, 0.4);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("section_complete", "Completar seção: acorde de sinos, coro e brilhos.", p =>
			{
				Foley.Thud(p, 0, 140, 0.5);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6 }, 0, 0.05, 1.1, 0.9);
				Arcane.Choir(p, new[] { D4, A4, Fs5 }, 0.05, 0.1, 0.25, 0.6, 0.4);
				Arcane.Sparkle(p, 0.15, 0.6, 7, D6, D7, 0.35, Major);
				p.Reverb(0.18, 1.1);
			});

			yield return r.With(Mix.Epic, "book_complete", "Livro completamente preenchido: a capa fechando, coro, sinos e o sigilo.", p =>
			{
				Foley.Thud(p, 0, 110, 0.9);
				Foley.Leather(p, 0, 0.5);
				Arcane.Choir(p, new[] { D4, A4, D5, Fs5 }, 0.05, 0.35, 0.5, 1.0, 0.6);
				Arcane.Pad(p, new[] { D3, A3 }, 0.05, 0.3, 0.4, 0.9, 0.35, 1200);
				Arcane.Chord(p, new[] { D5, Fs5, A5, D6, A6 }, 0.1, 0.06, 1.6, 1);
				Arcane.Sigil(p, 0.45, 0.7);
				Arcane.Sparkle(p, 0.3, 1.1, 14, D6, A7, 0.4, Major);
				p.Reverb(0.22, 1.6);
			});
		}
	}
}
