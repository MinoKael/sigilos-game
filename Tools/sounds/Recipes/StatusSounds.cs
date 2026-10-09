using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// Os efeitos de batalha em dois sons só, baixinhos: o que ajuda sobe (fortalecer, curar, reviver), o que
	/// atrapalha desce (enfraquecer, atordoar, envenenar). O efeito aparece na tela; o som só diz o lado.
	/// </summary>
	internal static class StatusSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat/status", Mix.Soft);

			yield return r.Of("boon", "Efeito bom: a celesta subindo de Mi a Si.", p =>
			{
				Arcane.Celesta(p, E6, 0, 0.3, 0.6);
				Arcane.Celesta(p, B6, 0.06, 0.4, 0.5);
				Foley.Swish(p, 0, 0.18, 700, 2200, 0.12);
				p.Reverb(0.12, 0.6);
			}, levelDb: -2);

			yield return r.Of("bane", "Efeito ruim: a harpa descendo de Sol a Mi, abafada, com a sombra.", p =>
			{
				Arcane.Harp(p, G4, 0, 0.35, 0.7);
				Arcane.Harp(p, E4, 0.07, 0.45, 0.7);
				Elemental.Umbra(p, 0, 0.3, E3, 0.15);
				p.Lowpass(3000);
				p.Reverb(0.1, 0.6);
			}, levelDb: -2.5);
		}
	}
}
