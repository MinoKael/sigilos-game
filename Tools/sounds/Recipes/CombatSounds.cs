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
	/// Os golpes: o ar do movimento e o impacto, estilizados como em desenho (corpo médio na frente, grave
	/// contido, um "bonk" de madeira na pancada). Cada um com variações, para a luta não repetir o mesmo som.
	/// </summary>
	internal static class CombatSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat", Mix.Combat);

			yield return r.Of("attack_light", "Ataque físico leve.", p =>
			{
				Strike.Whoosh(p, 0, p.Vary(0.1, 0.15), p.Vary(800, 0.1), p.Vary(2600, 0.1), 0.6);
				Strike.Impact(p, 0.08, 0.15, 1);
			}, 3);

			yield return r.Of("attack_medium", "Ataque físico médio.", p =>
			{
				Strike.Whoosh(p, 0, p.Vary(0.13, 0.15), p.Vary(600, 0.1), p.Vary(2200, 0.1), 0.6);
				Strike.Impact(p, 0.1, 0.45, 1);
			}, 3);

			yield return r.Of("attack_heavy", "Ataque físico pesado.", p =>
			{
				Strike.Whoosh(p, 0, p.Vary(0.18, 0.1), p.Vary(400, 0.1), p.Vary(1600, 0.1), 0.7, 1.1);
				Strike.Impact(p, 0.15, 0.75, 1);
			}, 3);

			yield return r.Of("critical_hit", "Golpe crítico: o impacto com um anel de metal e um lampejo.", p =>
			{
				Strike.Whoosh(p, 0, 0.1, 700, 3000, 0.5);
				Strike.Impact(p, 0.08, 0.7, 1);
				p.Modal(Modes.Metal, p.Vary(1650, 0.05), 0.5, 0.08).Bright(0.7).Gain(0.35);
				Arcane.Glint(p, Pick(p, A6, B6), 0.08, 0.35);
				p.Reverb(0.08, 0.5);
			}, 2, 1.5);

			yield return r.Of("very_heavy_hit", "Golpe muito pesado: impacto máximo com o estouro.", p =>
			{
				Strike.Whoosh(p, 0, 0.22, 300, 1300, 0.7, 1);
				Strike.Impact(p, 0.18, 1, 1);
				Strike.Boom(p, 0.18, 0.25, 0.45);
			}, 2, 1);

			yield return r.Of("slash", "Corte.", p => Strike.Slash(p, 0, 1), 3);

			yield return r.Of("pierce", "Perfuração: o sopro fino e o \"tchk\" da ponta.", p =>
			{
				Strike.Whoosh(p, 0, 0.07, 2000, 5000, 0.5, 2);
				p.Modal(Modes.Metal, p.Vary(2900, 0.06), 0.08, 0.06).Bright(0.5).Gain(0.35);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.0005, 0.03, 4), 0.06).Bandpass(2200, 1.5).Gain(0.7);
				p.Tone(Wave.Sine, p.Vary(320, 0.05), Envelope.Perc(0.001, 0.05), 0.06).Sweep(2, 0.006).Gain(0.5);
			}, 3);

			yield return r.Of("impact", "Impacto.", p =>
			{
				Strike.Impact(p, 0, 0.5, 1);
				Foley.Thud(p, 0, p.Vary(150, 0.06), 0.4);
			}, 3);

			yield return r.Of("blunt", "Pancada: o impacto com um \"bonk\" de madeira.", p =>
			{
				Strike.Impact(p, 0, 0.6, 0.9);
				p.Modal(Modes.Wood, p.Vary(260, 0.08), 0.12).Bright(0.6).Gain(0.6);
			}, 3);

			yield return r.Of("ranged_attack", "Ataque à distância: o disparo e o ar indo embora.", p =>
			{
				p.Pluck(p.Vary(110, 0.05), 0.25).Bright(0.5).Gain(0.6);
				Foley.Swish(p, 0, 0.06, 1500, 600, 0.3);
				Strike.Whoosh(p, 0.03, 0.2, 1200, 3500, 0.5, 1.8);
			}, 2);

			yield return r.Of("magic_projectile", "Projétil mágico: o lampejo de saída e o rastro de cristal.", p =>
			{
				var note = Pick(p, D6, E6, A6);
				Arcane.Glint(p, note, 0, 0.5);
				p.Tone(Wave.Sine, Hz(note + 12), Envelope.Perc(0.005, 0.3)).Glide(Hz(note), 0.6).Fm(2, 1, 0).Gain(0.35);
				p.Noise(NoiseColor.White, Envelope.Gust(0.3, 0.25)).Bandpass(3500, 2, 1200).Gain(0.3);
				Arcane.Sparkle(p, 0.05, 0.25, 3, note - 12, note, 0.25);
				p.Reverb(0.08, 0.5);
			}, 3);

			yield return r.Of("physical_projectile", "Projétil físico: algo girando no ar e batendo.", p =>
			{
				Strike.Whoosh(p, 0, 0.22, 700, 2400, 0.7, 1.6).Tremolo(p.Vary(18, 0.2), 0.6);
				Foley.Knock(p, 0.2, 500, 0.05, 0.5);
			}, 2);

			yield return r.Of("arrow", "Flecha: a corda, o assobio e a ponta fincando.", p =>
			{
				p.Pluck(p.Vary(150, 0.05), 0.22).Bright(0.45).Gain(0.55);
				Strike.Whoosh(p, 0.02, 0.13, 2500, 4500, 0.5, 2.5);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.0005, 0.025, 4), 0.17).Bandpass(1500, 1.2).Gain(0.8);
				Foley.Knock(p, 0.17, p.Vary(700, 0.06), 0.06, 0.6);
			}, 3);

			yield return r.Of("multi_projectile", "Múltiplos projéteis: quatro disparos em leque.", p =>
			{
				p.Pluck(120, 0.2).Bright(0.5).Gain(0.4);
				for (var i = 0; i < 4; i++)
				{
					var at = i * p.Vary(0.05, 0.2);
					Strike.Whoosh(p, at, 0.12, p.Vary(1500, 0.2), p.Vary(4000, 0.15), 0.45, 2);
					Foley.Knock(p, at + 0.1, p.Vary(600, 0.1), 0.04, 0.35);
				}
			}, 2);

			yield return r.Of("area_attack", "Ataque em área: o golpe largo e a onda de choque.", p =>
			{
				Strike.Whoosh(p, 0, 0.3, 250, 1800, 0.6, 0.9);
				Strike.Impact(p, 0.25, 0.6, 0.9);
				Strike.Boom(p, 0.25, 0.3, 0.5);
				Foley.Swish(p, 0.25, 0.35, 3000, 600, 0.3);
			}, 2);

			yield return r.Of("explosion", "Explosão.", p =>
			{
				Strike.Boom(p, 0, 0.7, 1);
				Strike.Impact(p, 0, 0.6, 0.6);
				p.Reverb(0.08, 0.6);
			}, 2);

			yield return r.Of("attack_miss", "Ataque que erra: só o ar passando.", p =>
			{
				Strike.Whoosh(p, 0, p.Vary(0.2, 0.15), p.Vary(900, 0.1), p.Vary(3000, 0.1), 0.8, 1.6);
				Strike.Whoosh(p, 0.08, 0.18, 3000, 1200, 0.25, 1.2);
			}, 3, -2);

			yield return r.Of("attack_blocked", "Ataque bloqueado: o golpe no escudo de madeira e metal.", p =>
			{
				Strike.Impact(p, 0, 0.35, 0.7);
				p.Modal(Modes.Metal, p.Vary(820, 0.06), 0.3).Bright(0.6).Gain(0.6);
				Foley.Knock(p, 0, p.Vary(380, 0.06), 0.08, 0.6);
			}, 3);

			yield return r.Of("attack_resisted", "Ataque resistido: o golpe abafado numa taça grave.", p =>
			{
				Arcane.Bowl(p, D4, 0, 0.5, 0.5);
				Foley.Thud(p, 0, 130, 0.6);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.08)).Lowpass(1200).Gain(0.4);
			}, 2, -1);
		}
	}
}
