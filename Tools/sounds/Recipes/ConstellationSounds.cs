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
	/// As constelações: cristais, sinos suaves, harmônicos e lampejos em Ré lídio (o Sol sustenido dá o ar de
	/// sonho), sobre tapetes de triângulo. Nada de espaço futurista: céu antigo, de astrolábio.
	/// </summary>
	internal static class ConstellationSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("constellation", Mix.Soft);

			yield return r.Of("star_activate", "Ativar estrela: um lampejo de cristal.", p =>
			{
				Arcane.Glint(p, Pick(p, D6, E6, A6), 0, 0.8);
				Arcane.Sparkle(p, 0.02, 0.1, 2, A6, E7, 0.2);
				p.Reverb(0.12, 0.7);
			}, 3);

			yield return r.Of("stars_connect", "Conectar duas estrelas: dois lampejos ligados por um fio de tom.", p =>
			{
				Arcane.Glint(p, D6, 0, 0.7);
				p.Tone(Wave.Sine, Hz(D6), Envelope.Swell(0.1, 0.08)).Glide(Hz(A6)).Gain(0.25);
				Arcane.Glint(p, A6, 0.14, 0.7);
				p.Reverb(0.14, 0.8);
			});

			yield return r.Of("line_draw", "Criar linha: um traço de luz subindo, tremendo de leve.", p =>
			{
				p.Tone(Wave.Triangle, Hz(A5), Envelope.Swell(0.28, 0.14)).Glide(Hz(E6), 0.8).Tremolo(14, 0.3).Gain(0.45);
				p.Noise(NoiseColor.White, Envelope.Swell(0.28, 0.14)).Bandpass(2500, 4, 6000).Gain(0.15);
				Arcane.Glass(p, E6, 0.36, 0.4, 0.4);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("constellation_complete", "Completar constelação: a escala lídia em cristal e o acorde aberto.", p =>
			{
				Arcane.Arpeggio(p, new[] { D6, E6, Fs6, Gs6, A6 }, 0, 0.055, (note, at, _) => Arcane.Glass(p, note, at, 0.5, 0.5));
				Arcane.Chord(p, new[] { D5, A5, E6 }, 0.3, 0.03, 1.2, 0.8);
				Arcane.Air(p, new[] { D4, A4, E5 }, 0.25, 0.2, 0.2, 0.9, 0.35);
				Arcane.Shimmer(p, 0.3, 0.8, Hz(A6), 0.25);
				p.Reverb(0.2, 1.2);
			});

			yield return r.Of("partial_activate", "Parcialmente ativada: Ré e Lá, sem o Mi que fecharia.", p =>
			{
				Arcane.Glint(p, D6, 0, 0.6);
				Arcane.Glass(p, A6, 0.09, 0.4, 0.4);
				Arcane.Air(p, new[] { D5, A5 }, 0, 0.1, 0.05, 0.3, 0.2);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("full_activate", "Totalmente ativada: Ré–Lá–Mi completo, o sino e o brilho.", p =>
			{
				Arcane.Glint(p, D6, 0, 0.6);
				Arcane.Glass(p, A6, 0.07, 0.5, 0.5);
				Arcane.Glass(p, E7, 0.14, 0.5, 0.4);
				Arcane.Bell(p, D5, 0.14, 0.9, 0.5);
				Arcane.Shimmer(p, 0.14, 0.6, Hz(D6), 0.3);
				p.Reverb(0.16, 1);
			});

			yield return r.Of("star_pulse", "Estrela pulsando: um tom que acende e apaga.", p =>
			{
				var note = Pick(p, D6, A6);
				p.Tone(Wave.Sine, Hz(note), Envelope.Swell(0.12, 0.3)).Fm(2, 0.6, 0).Gain(0.6);
				p.Tone(Wave.Sine, Hz(note + 12), Envelope.Swell(0.12, 0.2)).Gain(0.15);
				p.Reverb(0.12, 0.6);
			}, 2);

			yield return r.Of("astral_charge", "Energia astral acumulando: a quinta subindo, brilhos cada vez mais densos.", p =>
			{
				Arcane.Riser(p, 0, 1.0, Hz(D5), Hz(A5), 0.5);
				Arcane.Shimmer(p, 0, 1.05, Hz(D6), 0.3);
				Arcane.Sparkle(p, 0.3, 0.75, 10, D6, E7, 0.3);
				p.Crackle(15, 0.002, Envelope.Swell(1, 0.05)).DensityTo(120).Bandpass(5000, 1).Gain(0.15);
				p.Reverb(0.1, 0.6);
			});

			yield return r.Of("astral_release", "Energia astral liberada: o estouro de ar e o acorde de cristal.", p =>
			{
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.003, 0.5, 2.5)).Bandpass(4000, 0.8, 600).Gain(0.6);
				Arcane.Chord(p, new[] { D6, A6, E7 }, 0, 0.012, 0.8, 0.8, Modes.Glass);
				Arcane.Bowl(p, D5, 0, 1.0, 0.5);
				Arcane.Sparkle(p, 0.05, 0.5, 8, A6, A7, 0.3);
				p.Reverb(0.18, 1.1);
			});

			yield return r.Of("stardust", "Poeira estelar: brilhos miúdos caindo.", p =>
			{
				Arcane.Sparkle(p, 0, 0.6, 12, D7, A7, 0.5);
				p.Noise(NoiseColor.White, Envelope.Swell(0.2, 0.4)).Bandpass(5500, 2).Gain(0.08);
				p.Reverb(0.12, 0.7);
			}, 2);

			yield return r.Of("astral_glint", "Brilho astral: um lampejo e o harmônico.", p =>
			{
				Arcane.Glint(p, E6, 0, 0.7);
				Arcane.Glass(p, A6, 0.04, 0.35, 0.35);
				p.Reverb(0.12, 0.6);
			});

			yield return r.Of("constellation_discovered", "Descobrir constelação: o céu abrindo, três lampejos e o sino.", p =>
			{
				Arcane.Air(p, new[] { D5, A5, E6 }, 0, 0.3, 0.3, 0.7, 0.4);
				Arcane.Arpeggio(p, new[] { D6, A6, E7 }, 0.05, 0.1, (note, at, _) => Arcane.Glint(p, note, at, 0.45));
				Arcane.Bell(p, D5, 0.3, 1.2, 0.5);
				Arcane.Sparkle(p, 0.3, 0.6, 6, D7, A7, 0.25);
				p.Reverb(0.2, 1.3);
			});

			yield return r.Of("node_unlock", "Desbloquear nó: o cristal soltando e um lampejo.", p =>
			{
				Foley.Clink(p, 0, 2600, 0.3);
				Arcane.Glass(p, D6, 0.02, 0.4, 0.6);
				Arcane.Glint(p, A6, 0.08, 0.6);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("path_unlock", "Desbloquear caminho: o traço de luz acendendo quatro estrelas.", p =>
			{
				p.Tone(Wave.Triangle, Hz(D6), Envelope.Swell(0.35, 0.2)).Glide(Hz(A6), 0.8).Tremolo(12, 0.3).Gain(0.3);
				Arcane.Arpeggio(p, new[] { D6, E6, Fs6, A6 }, 0.05, 0.08, (note, at, _) => Arcane.Glass(p, note, at, 0.4, 0.45));
				Arcane.Glint(p, D7, 0.38, 0.5);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("constellation_evolve", "Evoluir constelação: a energia subindo até um acorde lídio maior.", p =>
			{
				Arcane.Riser(p, 0, 0.55, Hz(A4), Hz(A5), 0.45);
				Arcane.Chord(p, new[] { D5, Fs5, A5, Cs6, E6 }, 0.55, 0.035, 1.2, 0.9);
				Arcane.Shimmer(p, 0.55, 0.8, Hz(Fs6), 0.3);
				Arcane.Glint(p, Gs6, 0.6, 0.5);
				Arcane.Sparkle(p, 0.55, 0.6, 8, D6, D7, 0.3);
				p.Reverb(0.2, 1.2);
			});

			yield return r.Of("constellation_reset", "Resetar constelação: as estrelas voltando ao contrário e o ar descendo.", p =>
			{
				var notes = new[] { A6, Fs6, E6, D6 };
				for (var i = 0; i < notes.Length; i++)
					p.Modal(Modes.Glass, Hz(notes[i]), 0.25, i * 0.06).Reverse().Gain(0.5);
				p.Noise(NoiseColor.Pink, Envelope.Gust(0.4, 0.3)).Bandpass(5000, 1, 800).Gain(0.35);
				Arcane.Bowl(p, D5, 0.35, 0.6, 0.35);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("activate_fail", "Falha ao ativar: o cristal em segunda menor apagando.", p =>
			{
				p.Modal(Modes.Glass, Hz(D6), 0.25).Bright(0.4).Gain(0.5);
				p.Modal(Modes.Glass, Hz(Ds6), 0.25).Bright(0.4).Gain(0.4);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.01, 0.3, 2)).Lowpass(3000, 300).Gain(0.3);
				p.Tone(Wave.Sine, Hz(A5), Envelope.Perc(0.005, 0.25)).Glide(Hz(D5), 0.6).Gain(0.3);
				p.Lowpass(6000);
			});

			yield return r.With(Mix.Epic, "legendary_unlock", "Constelação lendária desbloqueada: a subida, o sigilo, o coro lídio e a chuva de estrelas.", p =>
			{
				Arcane.Riser(p, 0, 0.8, Hz(D4), Hz(D5), 0.45);
				Arcane.Sigil(p, 0.8, 0.9);
				Arcane.Choir(p, new[] { D4, A4, E5, Fs5 }, 0.75, 0.1, 0.6, 1.1, 0.6);
				Arcane.Chord(p, new[] { D5, A5, E6, Gs6, A6 }, 0.8, 0.04, 1.8, 0.9);
				Arcane.Sparkle(p, 0.8, 1.2, 16, D6, A7, 0.35);
				Arcane.Air(p, new[] { D3, A3 }, 0.7, 0.2, 0.6, 1.0, 0.25);
				p.Reverb(0.22, 1.6);
			});
		}
	}
}
