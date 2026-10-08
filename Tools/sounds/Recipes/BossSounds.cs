using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// O chefe: mais grave, mais largo e com voz própria (<see cref="Colossus"/>: o rosnado, o tambor, o gongo
	/// e o batimento), em Ré frígio. Todos da classe épica.
	/// </summary>
	internal static class BossSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat/boss", Mix.Epic);

			yield return r.Of("enter", "Boss entrando: dois passos de tambor, o gongo e o rosnado.", p =>
			{
				Colossus.Drum(p, 0, 0.9);
				Colossus.Drum(p, 0.45, 0.9);
				Colossus.Gong(p, D3, 0.9, 2.2, 0.7);
				Colossus.Growl(p, 0.9, 1.2, Hz(D2), 0, 0.7);
				Arcane.Pad(p, new[] { D3, A3, Ds4 }, 0.9, 0.4, 0.6, 1.0, 0.35, 900);
				p.Reverb(0.18, 1.5);
			});

			yield return r.Of("special", "Boss usando habilidade especial: a subida grave, o rosnado e o estouro.", p =>
			{
				Arcane.Riser(p, 0, 0.7, Hz(D3), Hz(A3), 0.5);
				Colossus.Growl(p, 0.3, 0.8, Hz(A2), Hz(D3), 0.6);
				Elemental.Umbra(p, 0.2, 0.6, D3, 0.4);
				Strike.Boom(p, 0.7, 0.6, 0.9);
				p.Reverb(0.15, 1.2);
			});

			yield return r.Of("enrage", "Boss enfurecendo: o rosnado subindo e os tambores acelerando.", p =>
			{
				Colossus.Growl(p, 0, 1.2, Hz(D2), Hz(A2), 1);
				var steps = new[] { 0, 0.3, 0.55, 0.75, 0.9 };
				foreach (var at in steps)
					Colossus.Drum(p, at, 0.7);
				Elemental.Flame(p, 0.3, 0.9, 0.4);
				p.Reverb(0.12, 1);
			});

			yield return r.Of("mechanic", "Boss ativando mecânica: o gongo e o mecanismo girando.", p =>
			{
				Colossus.Gong(p, A3, 0, 1.4, 0.6);
				for (var i = 0; i < 6; i++)
				{
					Foley.Clink(p, 0.1 + i * 0.09, i % 2 == 0 ? 900 : 1150, 0.3);
					Foley.Knock(p, 0.1 + i * 0.09, 300, 0.04, 0.3);
				}
				Arcane.Bowl(p, D4, 0.5, 1.0, 0.35);
				p.Reverb(0.15, 1.1);
			});

			yield return r.Of("regenerate", "Boss regenerando: a sombra cantando, bolhas graves e a taça ao contrário.", p =>
			{
				p.Modal(Modes.Bowl, Hz(D4), 0.9).Reverse().Gain(0.5);
				Arcane.Choir(p, new[] { D3, A3 }, 0, 0.6, 0.2, 0.5, 0.5, Vowel.U, Vowel.O);
				Elemental.Bubbles(p, 0.2, 0.8, 8, 150, 450, 0.35);
				Elemental.Umbra(p, 0, 0.9, D2, 0.4);
				p.Reverb(0.18, 1.2);
			});

			yield return r.Of("counter", "Boss contra-atacando: o metal, o rosnado curto e o golpe.", p =>
			{
				p.Modal(Modes.Metal, p.Vary(700, 0.05), 0.5).Bright(0.6).Gain(0.6);
				Colossus.Growl(p, 0, 0.35, Hz(A2), 0, 0.6);
				Strike.Whoosh(p, 0, 0.15, 400, 1600, 0.5);
				Strike.Impact(p, 0.05, 0.8, 1);
				p.Reverb(0.1, 0.8);
			});

			yield return r.Of("heavy_damage", "Boss recebendo dano pesado: o impacto máximo e o rosnado de dor.", p =>
			{
				Strike.Impact(p, 0, 1, 1);
				Colossus.Growl(p, 0.03, 0.6, Hz(A2), Hz(D2), 0.7);
				Strike.Shatter(p, 0, 0.3, 1500);
				p.Reverb(0.1, 0.8);
			});

			yield return r.Of("near_defeat", "Boss quase derrotado: o batimento cansado e o rosnado fraco.", p =>
			{
				Colossus.Heartbeat(p, 0, 0.6);
				Colossus.Heartbeat(p, 0.7, 0.5);
				Colossus.Growl(p, 0.1, 0.9, Hz(D2), Hz(D2) * 0.85, 0.35);
				Arcane.Whisper(p, 0.2, 0.8, 0.25);
				p.Reverb(0.15, 1.1);
			}, levelDb: -1);

			yield return r.Of("defeated", "Boss derrotado: o rosnado caindo, o estouro, os cacos e o gongo.", p =>
			{
				Colossus.Growl(p, 0, 1.0, Hz(A2), Hz(D2) * 0.7, 0.8);
				Strike.Boom(p, 0.5, 1, 1);
				Strike.Shatter(p, 0.5, 0.5, 1800);
				Colossus.Gong(p, D3, 0.5, 2.4, 0.6);
				Arcane.Choir(p, new[] { D4, A4, D5 }, 0.6, 0.4, 0.4, 1.2, 0.4, Vowel.O, Vowel.A);
				p.Reverb(0.2, 1.6);
			});
		}
	}
}
