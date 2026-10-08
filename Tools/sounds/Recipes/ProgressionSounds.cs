using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// O andamento da luta: Ímpeto, habilidades, turnos e o fim. Os recorrentes (turno, Ímpeto) são curtos e
	/// baixos; vitória, derrota e chefe derrotado são da classe épica.
	/// </summary>
	internal static class ProgressionSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("progression", Mix.Soft);

			yield return r.Of("impetus_gain", "Ganhar Ímpeto: um vidro e o tom subindo à quinta.", p =>
			{
				Arcane.Glass(p, D6, 0, 0.18, 0.5);
				p.Tone(Wave.Sine, Hz(D6), Envelope.Perc(0.002, 0.1)).Glide(Hz(A6), 0.5, 0.05).Gain(0.3);
				Foley.Tick(p, 0, 4000, 0.25);
			}, levelDb: -3);

			yield return r.Of("impetus_loss", "Perder Ímpeto: o tom caindo e o ar saindo.", p =>
			{
				p.Tone(Wave.Sine, Hz(A5), Envelope.Perc(0.002, 0.14)).Glide(Hz(D5), 0.5, 0.08).Gain(0.4);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.003, 0.08)).Lowpass(1500).Gain(0.3);
			}, levelDb: -3);

			yield return r.Of("impetus_full", "Ímpeto máximo: Ré–Lá–Mi em lampejos e o brilho.", p =>
			{
				Arcane.Glint(p, D6, 0, 0.5);
				Arcane.Glint(p, A6, 0.05, 0.5);
				Arcane.Glint(p, E6, 0.1, 0.4);
				Arcane.Shimmer(p, 0.1, 0.5, Hz(D6), 0.25);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("skill_activate", "Ativar habilidade: o sigilo acendendo com um sopro.", p =>
			{
				Arcane.Sigil(p, 0, 0.8, A5);
				Strike.Whoosh(p, 0, 0.2, 700, 3000, 0.35);
				p.Reverb(0.1, 0.7);
			});

			yield return r.Of("skill_charging", "Habilidade carregando: a subida com o pulso acelerando.", p =>
			{
				Arcane.Riser(p, 0, 0.7, Hz(A4), Hz(A5), 0.5);
				p.Tone(Wave.Triangle, Hz(D5), Envelope.Swell(0.65, 0.05)).Tremolo(5, 0.5, 18).Gain(0.25);
				p.Crackle(20, 0.0015, Envelope.Swell(0.7, 0.05)).DensityTo(160).Bandpass(4000, 1.2).Gain(0.2);
			});

			yield return r.Of("skill_ready", "Habilidade pronta: dois lampejos e o sino.", p =>
			{
				Arcane.Glint(p, D6, 0, 0.6);
				Arcane.Glint(p, A6, 0.07, 0.6);
				Arcane.Bell(p, D6, 0.07, 0.7, 0.35);
				p.Reverb(0.12, 0.8);
			});

			yield return r.Of("ultimate_ready", "Ultimate pronto: subida curta, sinos Ré–Lá–Mi, coro e lampejo.", p =>
			{
				Arcane.Riser(p, 0, 0.3, Hz(D5), Hz(A5), 0.35);
				Arcane.Chord(p, new[] { D5, A5, D6, E6 }, 0.3, 0.03, 1, 0.8);
				Arcane.Choir(p, new[] { D4, A4, E5 }, 0.28, 0.05, 0.2, 0.6, 0.35);
				Arcane.Glint(p, A6, 0.3, 0.4);
				Arcane.Sparkle(p, 0.3, 0.5, 6, D6, A7, 0.25);
				p.Reverb(0.18, 1);
			}, levelDb: 1);

			yield return r.Of("turn_start", "Turno iniciado: a quinta subindo na marimba, baixinho.", p =>
			{
				Arcane.Marimba(p, D5, 0, 0.2, 0.6);
				Arcane.Marimba(p, A5, 0.07, 0.25, 0.6);
				Foley.Knock(p, 0, 900, 0.03, 0.25);
			}, levelDb: -3);

			yield return r.Of("turn_end", "Turno encerrado: a quinta descendo, mais baixinho.", p =>
			{
				Arcane.Marimba(p, A5, 0, 0.15, 0.5);
				Arcane.Marimba(p, D5, 0.07, 0.2, 0.5);
			}, levelDb: -4);

			yield return r.With(Mix.Epic, "victory", "Vitória: o acorde subindo, sinos, coro e a chuva de brilhos.", p =>
			{
				Foley.Thud(p, 0, 130, 0.5);
				Arcane.Arpeggio(p, new[] { D5, Fs5, A5, D6 }, 0, 0.09, (note, at, _) => Arcane.Marimba(p, note, at, 0.35, 0.65));
				Arcane.Chord(p, new[] { D5, Fs5, A5, D6, Fs6 }, 0.36, 0.03, 1.6, 1);
				Arcane.Choir(p, new[] { D4, A4, D5, Fs5 }, 0.34, 0.1, 0.6, 1.0, 0.6);
				Arcane.Sparkle(p, 0.4, 1.0, 14, D6, A7, 0.35, Major);
				p.Reverb(0.2, 1.4);
			});

			yield return r.With(Mix.Epic, "defeat", "Derrota: sinos abafados descendo em Ré frígio sobre o acorde menor.", p =>
			{
				var notes = new[] { D5, C5, As4, A4 };
				for (var i = 0; i < notes.Length; i++)
					p.Modal(Modes.Bell, Hz(notes[i]), i == 3 ? 1.6 : 1, i * 0.3).Bright(0.6).Gain(0.6);
				Arcane.Pad(p, new[] { D3, F3, A3 }, 0.85, 0.4, 0.5, 1.2, 0.5, 900);
				Arcane.Whisper(p, 0.9, 0.8, 0.2);
				p.Reverb(0.2, 1.4);
			}, levelDb: -1);

			yield return r.Of("stage_clear", "Fase concluída: a marimba e três sinos.", p =>
			{
				Arcane.Arpeggio(p, new[] { D5, A5, D6 }, 0, 0.07, (note, at, _) => Arcane.Marimba(p, note, at, 0.3, 0.6));
				Arcane.Chord(p, new[] { D6, Fs6, A6 }, 0.2, 0.03, 1.0, 0.8);
				Arcane.Sparkle(p, 0.22, 0.5, 6, D6, D7, 0.3, Major);
				p.Reverb(0.15, 1);
			});

			yield return r.With(Mix.Epic, "boss_defeated", "Boss derrotado: o estouro, o gongo, coro e sinos.", p =>
			{
				Strike.Boom(p, 0, 0.6, 0.7);
				Colossus.Gong(p, D4, 0, 2.0, 0.5);
				Arcane.Choir(p, new[] { D4, A4, D5, Fs5, A5 }, 0.3, 0.3, 0.6, 1.2, 0.7);
				Arcane.Chord(p, new[] { D5, A5, D6, Fs6, A6 }, 0.35, 0.04, 1.8, 1);
				Arcane.Sparkle(p, 0.4, 1.2, 16, D6, A7, 0.35, Major);
				p.Reverb(0.22, 1.6);
			});

			yield return r.With(Mix.Epic, "boss_reward", "Recompensa de boss: moedas, quatro sinos, coro e brilhos.", p =>
			{
				for (var i = 0; i < 6; i++)
					Foley.Coin(p, i * p.Vary(0.05, 0.3), p.Vary(3000, 0.15), p.Rng.Range(0.4, 0.8));
				Arcane.Chord(p, new[] { D5, Fs5, A5, D6 }, 0.05, 0.035, 1.4, 0.9);
				Arcane.Choir(p, new[] { D4, A4, Fs5 }, 0.05, 0.15, 0.4, 0.9, 0.5);
				Arcane.Sparkle(p, 0.1, 0.9, 14, D6, A7, 0.35, Major);
				p.Reverb(0.18, 1.3);
			});

			yield return r.Of("new_wave", "Nova onda: dois passos, o ar e o chamado do sino.", p =>
			{
				Foley.Thud(p, 0, 120, 0.8);
				Foley.Thud(p, 0.18, 120, 0.8);
				Foley.Swish(p, 0.15, 0.3, 500, 2500, 0.4);
				Arcane.Bell(p, D5, 0.32, 0.9, 0.5);
				Arcane.Bell(p, A5, 0.34, 0.8, 0.35);
				p.Reverb(0.12, 0.9);
			});

			yield return r.Of("auto_battle_start", "Combate automático iniciado: a engrenagem e dois lampejos subindo.", p =>
			{
				Foley.Clink(p, 0, 1800, 0.4);
				Foley.Clink(p, 0.06, 2300, 0.35);
				Arcane.Glint(p, D6, 0.1, 0.5);
				Arcane.Glint(p, A6, 0.18, 0.5);
				p.Reverb(0.1, 0.7);
			});

			yield return r.Of("auto_battle_end", "Combate automático encerrado: os lampejos descendo e a engrenagem parando.", p =>
			{
				Arcane.Glint(p, A6, 0, 0.45);
				Arcane.Glint(p, D6, 0.08, 0.45);
				Foley.Clink(p, 0.16, 2000, 0.35);
				Foley.Knock(p, 0.2, 800, 0.04, 0.4);
			});
		}
	}
}
