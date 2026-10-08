using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// Os efeitos de batalha: o que é bom sobe (celesta, sinos, coro), o que é ruim desce ou desafina (sombra,
	/// segunda menor), e cada um tem um gesto próprio (as estrelinhas do atordoamento, a canção de ninar do
	/// sono, o pavio da bomba).
	/// </summary>
	internal static class StatusSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat/status", Mix.Combat);

			yield return r.Of("buff_apply", "Aplicar buff: a celesta subindo e um brilho.", p =>
			{
				Arcane.Arpeggio(p, new[] { D6, Fs6, A6 }, 0, 0.045, (note, at, _) => Arcane.Celesta(p, note, at, 0.3, 0.55));
				Arcane.Shimmer(p, 0.05, 0.35, Hz(A6), 0.25);
				Foley.Swish(p, 0, 0.2, 800, 3500, 0.25);
				p.Reverb(0.1, 0.6);
			});

			yield return r.Of("buff_remove", "Remover buff: a celesta descendo e o ar saindo.", p =>
			{
				Arcane.Arpeggio(p, new[] { A6, Fs6, D6 }, 0, 0.04, (note, at, _) => Arcane.Celesta(p, note, at, 0.2, 0.45));
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.005, 0.15)).Bandpass(2500, 1, 900).Gain(0.3);
			}, levelDb: -2);

			yield return r.Of("debuff_apply", "Aplicar debuff: a sombra e o vidro escorregando meio tom.", p =>
			{
				Elemental.Umbra(p, 0, 0.3, D4, 0.5);
				Arcane.Glass(p, A5, 0, 0.3, 0.4);
				Arcane.Glass(p, Gs5, 0.06, 0.35, 0.4);
				Foley.Swish(p, 0, 0.25, 2500, 600, 0.35);
				p.Reverb(0.1, 0.6);
			});

			yield return r.Of("debuff_remove", "Remover debuff: um sopro limpando e a quinta subindo.", p =>
			{
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.005, 0.2)).Bandpass(800, 1, 3000).Gain(0.4);
				Arcane.Glass(p, D6, 0.05, 0.3, 0.4);
				Arcane.Glass(p, A6, 0.1, 0.3, 0.35);
			}, levelDb: -2);

			yield return r.Of("stun", "Stun: o golpe e as estrelinhas girando.", p =>
			{
				Strike.Impact(p, 0, 0.35, 0.6);
				for (var i = 0; i < 6; i++)
					Arcane.Glass(p, i % 2 == 0 ? E7 : Cs7, 0.06 + i * 0.07, 0.12, 0.35);
				p.Tone(Wave.Sine, Hz(A6), Envelope.Swell(0.1, 0.5), 0.05).Vibrato(9, 1.2).Gain(0.2);
				p.Reverb(0.08, 0.5);
			});

			yield return r.Of("sleep", "Sono: três notas de canção de ninar e um bocejo.", p =>
			{
				Arcane.Arpeggio(p, new[] { A5, Fs5, D5 }, 0, 0.12, (note, at, _) => Arcane.Celesta(p, note, at, 0.45, 0.5));
				Arcane.Air(p, new[] { D4, A4 }, 0, 0.2, 0.3, 0.5, 0.3);
				p.Tone(Wave.Sine, Hz(D5), Envelope.Swell(0.3, 0.4)).Glide(Hz(A4)).Tremolo(3, 0.4).Gain(0.15);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("silence", "Silêncio: um \"shh\" e a nota sendo abafada.", p =>
			{
				p.Noise(NoiseColor.Pink, Envelope.Swell(0.08, 0.35)).Bandpass(3000, 1, 600).Gain(0.5);
				p.Tone(Wave.Triangle, Hz(A5), Envelope.Perc(0.005, 0.4)).Lowpass(4000, 300).Gain(0.35);
				Foley.Knock(p, 0.05, 500, 0.05, 0.4);
			});

			yield return r.Of("oblivion", "Esquecimento: o vidro ao contrário, um sussurro e a nota se perdendo.", p =>
			{
				p.Modal(Modes.Glass, Hz(A6), 0.4).Reverse().Gain(0.4);
				Arcane.Whisper(p, 0.2, 0.45, 0.4);
				p.Tone(Wave.Sine, Hz(D6), Envelope.Perc(0.01, 0.5), 0.3).Glide(Hz(D5)).Vibrato(5, 0.3).Gain(0.3);
				p.Reverb(0.18, 1);
			});

			yield return r.Of("bomb", "Bomba: o pavio aceso e o tique-taque.", p =>
			{
				for (var i = 0; i < 3; i++)
					Foley.Tick(p, i * 0.12, 2200, 0.5);
				Elemental.Sizzle(p, 0, 0.4, 0.22);
				Foley.Knock(p, 0.36, 600, 0.06, 0.4);
			});

			yield return r.Of("wound", "Ferida: um corte curto e a pulsação, estilizados.", p =>
			{
				p.Tone(Wave.Sine, 140, Envelope.Perc(0.004, 0.2, 2.5)).Sweep(1.4, 0.02).Gain(0.5);
				Strike.Whoosh(p, 0, 0.07, 1800, 4000, 0.3, 2);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.12)).Bandpass(900, 1.2).Tremolo(14, 0.6).Gain(0.35);
			});

			yield return r.Of("poison", "Veneno: bolhas, a sombra e o tom azedo.", p =>
			{
				Elemental.Bubbles(p, 0, 0.35, 7, 250, 700, 0.6);
				Elemental.Umbra(p, 0, 0.35, D4, 0.3);
				p.Tone(Wave.Sine, Hz(Gs4), Envelope.Perc(0.01, 0.3)).Fm(1.41, 1.5).Gain(0.25);
			});

			yield return r.Of("burn", "Queimadura: o fogo pegando, a chama curta e o chiado.", p =>
			{
				Elemental.Ignite(p, 0, 0.6);
				Elemental.Flame(p, 0.05, 0.35, 0.5);
				Elemental.Sizzle(p, 0.1, 0.3, 0.3);
			});

			yield return r.Of("blessing", "Benção: coro curto, o sino e o brilho.", p =>
			{
				Arcane.Choir(p, new[] { D5, A5 }, 0, 0.12, 0.15, 0.5, 0.55);
				Arcane.Bell(p, D6, 0.05, 0.9, 0.5);
				Arcane.Bell(p, A6, 0.1, 0.8, 0.35);
				Arcane.Shimmer(p, 0.05, 0.5, Hz(D6), 0.25);
				p.Reverb(0.18, 1);
			});

			yield return r.Of("counter", "Contragolpe: a lâmina em guarda, \"shing\".", p =>
			{
				p.Modal(Modes.Metal, p.Vary(1500, 0.05), 0.35).Bright(0.7).Gain(0.5);
				Strike.Whoosh(p, 0, 0.12, 2500, 900, 0.5, 1.6);
				Foley.Knock(p, 0, 700, 0.04, 0.4);
			});

			yield return r.Of("revive", "Reviver: a energia subindo, coro, sinos e brilho.", p =>
			{
				Arcane.Riser(p, 0, 0.5, Hz(D4), Hz(D5), 0.4);
				Arcane.Choir(p, new[] { D5, Fs5, A5 }, 0.4, 0.1, 0.3, 0.7, 0.55);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6 }, 0.45, 0.04, 1.1, 0.8);
				Arcane.Sparkle(p, 0.45, 0.6, 8, D6, D7, 0.3, Major);
				p.Reverb(0.2, 1.2);
			}, levelDb: 1);

			yield return r.Of("karma", "Karma: a quinta girando em círculo sobre a taça.", p =>
			{
				for (var i = 0; i < 6; i++)
					Arcane.Glass(p, i % 2 == 0 ? D6 : A6, i * 0.06, 0.3, 0.4 * (1 - i * 0.08));
				Arcane.Bowl(p, D5, 0, 0.7, 0.4);
				Foley.Swish(p, 0, 0.4, 900, 2400, 0.25);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("resist", "Resistência: a taça curta e um \"tink\" abafado.", p =>
			{
				Arcane.Bowl(p, D5, 0, 0.4, 0.5);
				Arcane.Glass(p, D6, 0, 0.2, 0.25);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.06)).Bandpass(1200, 1).Gain(0.3);
			});

			yield return r.Of("immunity", "Imunidade: o acorde de cristal fechado sobre a taça.", p =>
			{
				Arcane.Chord(p, new[] { D6, A6, E7 }, 0, 0.015, 0.6, 0.7, Modes.Glass);
				Arcane.Bowl(p, D5, 0, 0.6, 0.4);
				Arcane.Shimmer(p, 0, 0.4, Hz(A5), 0.2);
				p.Reverb(0.12, 0.7);
			});

			yield return r.Of("purify", "Purificação: um sopro claro subindo, o sino e brilhos.", p =>
			{
				p.Noise(NoiseColor.White, Envelope.Swell(0.15, 0.3)).Bandpass(1500, 1, 6000).Gain(0.4);
				Arcane.Bell(p, D6, 0.15, 0.8, 0.5);
				Arcane.Glass(p, A6, 0.18, 0.5, 0.35);
				Arcane.Sparkle(p, 0.15, 0.4, 6, D6, D7, 0.3, Major);
				p.Reverb(0.15, 0.9);
			});

			yield return r.Of("heal", "Cura: a celesta subindo sobre um tapete leve.", p =>
			{
				Arcane.Arpeggio(p, new[] { D6, Fs6, A6 }, 0, 0.05, (note, at, _) => Arcane.Celesta(p, note, at, 0.35, 0.5));
				Arcane.Air(p, new[] { D5, A5 }, 0, 0.1, 0.15, 0.4, 0.3);
				Arcane.Shimmer(p, 0.05, 0.4, Hz(D6), 0.2);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("heal_critical", "Cura crítica: coro, a celesta até a oitava, sinos e brilhos.", p =>
			{
				Arcane.Choir(p, new[] { D5, Fs5, A5 }, 0, 0.1, 0.25, 0.6, 0.55);
				Arcane.Arpeggio(p, new[] { D6, Fs6, A6, D7 }, 0, 0.05, (note, at, _) => Arcane.Celesta(p, note, at, 0.4, 0.5));
				Arcane.Chord(p, new[] { D5, A5, D6 }, 0.15, 0.03, 1, 0.6);
				Arcane.Sparkle(p, 0.15, 0.6, 10, D6, A7, 0.3, Major);
				p.Reverb(0.18, 1.1);
			}, levelDb: 1.5);

			yield return r.Of("shield", "Escudo: o ar fechando a redoma de cristal.", p =>
			{
				Foley.Swish(p, 0, 0.18, 600, 2400, 0.35);
				Arcane.Bowl(p, D5, 0.12, 0.7, 0.55);
				Arcane.Glass(p, A6, 0.14, 0.5, 0.35);
				Arcane.Shimmer(p, 0.12, 0.4, Hz(A5), 0.2);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("shield_break", "Escudo quebrando: a redoma estilhaçando.", p =>
			{
				Strike.Shatter(p, 0, 0.9, 2400);
				Strike.Impact(p, 0, 0.45, 0.7);
				Arcane.Bowl(p, D5, 0, 0.3, 0.25);
				p.Reverb(0.08, 0.6);
			});
		}
	}
}
