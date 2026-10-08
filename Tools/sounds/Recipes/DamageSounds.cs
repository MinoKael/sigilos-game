using System.Collections.Generic;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Motifs;
using Sigilos.Sounds.Synth;
using static Sigilos.Sounds.Synth.Notes;

namespace Sigilos.Sounds.Recipes
{
	/// <summary>
	/// O dano em quem recebe: o corpo do impacto mais a cor do tipo (vidro na magia, textura do elemento,
	/// bolhas no veneno, chiado na queimadura). Três variações de cada, porque toca o tempo todo.
	/// </summary>
	internal static class DamageSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("combat/damage", Mix.Combat);

			yield return r.Of("physical_light", "Dano físico leve.", p =>
			{
				Strike.Impact(p, 0, 0.2, 1);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.001, 0.05)).Bandpass(p.Vary(700, 0.15), 1).Gain(0.3);
			}, 3);

			yield return r.Of("physical_medium", "Dano físico médio.", p =>
			{
				Strike.Impact(p, 0, 0.5, 1);
				Foley.Leather(p, 0, 0.3);
			}, 3);

			yield return r.Of("physical_heavy", "Dano físico pesado.", p =>
			{
				Strike.Impact(p, 0, 0.85, 1);
				Foley.Thud(p, 0, p.Vary(120, 0.05), 0.2);
				Foley.Knock(p, 0, p.Vary(380, 0.08), 0.08, 0.45);
			}, 3);

			yield return r.Of("critical", "Dano crítico: o impacto, o anel de metal e cacos.", p =>
			{
				Strike.Impact(p, 0, 0.75, 1);
				p.Modal(Modes.Metal, p.Vary(1900, 0.05), 0.45).Bright(0.7).Gain(0.35);
				Arcane.Glint(p, A6, 0, 0.3);
				Strike.Shatter(p, 0, 0.25, 3200);
			}, 3, 1.5);

			yield return r.Of("magic", "Dano mágico: impacto leve com a quinta de vidro e um sopro.", p =>
			{
				Strike.Impact(p, 0, 0.3, 0.7);
				Arcane.Glass(p, D6, 0, 0.25, 0.4);
				Arcane.Glass(p, A6, 0.01, 0.25, 0.3);
				p.Noise(NoiseColor.White, Envelope.Perc(0.002, 0.15)).Bandpass(3500, 1.5, 1500).Gain(0.35);
			}, 3);

			yield return r.Of("elemental", "Dano elemental: impacto com ruído vivo, estalos e vidro.", p =>
			{
				Strike.Impact(p, 0, 0.35, 0.8);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.25, 2.5)).Bandpass(1500, 1, 600).Wobble(9, 0.5).Gain(0.5);
				Arcane.Glass(p, A5, 0, 0.3, 0.3);
				p.Crackle(60, 0.0015, Envelope.Perc(0.005, 0.2)).Bandpass(3000, 1).Gain(0.3);
			}, 3);

			yield return r.Of("periodic", "Dano contínuo: uma pontada curta, para repetir a cada turno.", p =>
			{
				p.Tone(Wave.Sine, p.Vary(Hz(A5), 0.03), Envelope.Perc(0.002, 0.12)).Glide(Hz(D5), 0.6).Gain(0.5);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.06)).Bandpass(1100, 1).Gain(0.4);
				Strike.Impact(p, 0, 0.1, 0.4);
			}, 3, -3);

			yield return r.Of("poison", "Dano de veneno: bolhas graves e o tom azedando.", p =>
			{
				Elemental.Bubbles(p, 0, 0.18, 4, 250, 550, 0.6);
				p.Tone(Wave.Sine, Hz(D5), Envelope.Perc(0.005, 0.18)).Glide(Hz(Gs4), 0.7).Fm(1.41, 0.8).Gain(0.35);
				Strike.Impact(p, 0, 0.15, 0.4);
			}, 3);

			yield return r.Of("burn", "Dano de queimadura: o fogo pegando e o chiado.", p =>
			{
				Elemental.Ignite(p, 0, 0.6);
				Elemental.Sizzle(p, 0.05, 0.25, 0.35);
				Strike.Impact(p, 0, 0.2, 0.5);
			}, 3);

			yield return r.Of("bleed", "Dano de sangramento/ferida: um corte rápido e a pulsação, estilizados.", p =>
			{
				Strike.Whoosh(p, 0, 0.06, 1800, 4200, 0.4, 2);
				p.Tone(Wave.Sine, 120, Envelope.Perc(0.003, 0.16, 2.5)).Sweep(1.5, 0.02).Gain(0.5);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.001, 0.04)).Bandpass(1600, 1.2).Gain(0.5);
				Elemental.Bubble(p, 0.04, 700, 0.25);
			}, 3);

			yield return r.Of("light", "Dano de luz: o impacto que brilha.", p =>
			{
				Strike.Impact(p, 0, 0.25, 0.6);
				Arcane.Glass(p, A6, 0, 0.35, 0.5);
				Arcane.Glass(p, D7, 0.01, 0.3, 0.35);
				Arcane.Shimmer(p, 0, 0.25, Hz(D6), 0.25);
				p.Noise(NoiseColor.White, Envelope.Perc(0.002, 0.12)).Bandpass(4500, 1).Gain(0.25);
			}, 3);

			yield return r.Of("dark", "Dano de trevas: a sombra crescendo ao contrário até o golpe.", p =>
			{
				Elemental.Umbra(p, 0, 0.2, D3, 0.5);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.18)).Bandpass(900, 1.5).Reverse().Gain(0.5);
				Strike.Impact(p, 0.17, 0.4, 0.8);
			}, 3);

			yield return r.Of("fire", "Dano de fogo.", p =>
			{
				Strike.Impact(p, 0, 0.4, 0.8);
				Elemental.Flame(p, 0, 0.3, 0.6);
			}, 3);

			yield return r.Of("water", "Dano de água.", p =>
			{
				Elemental.Splash(p, 0, 0.8);
				Strike.Impact(p, 0, 0.25, 0.5);
			}, 3);

			yield return r.Of("wind", "Dano de vento.", p =>
			{
				Elemental.Gust(p, 0, 0.18, 1200, 3500, 0.6);
				Strike.Whoosh(p, 0, 0.08, 2500, 5000, 0.35, 2.5);
				Strike.Impact(p, 0.1, 0.2, 0.5);
			}, 3);

			yield return r.Of("reflected", "Dano refletido: o golpe volta num \"boing\" de metal.", p =>
			{
				Strike.Impact(p, 0, 0.4, 0.8);
				p.Modal(Modes.Metal, p.Vary(1400, 0.05), 0.25).Bright(0.6).Gain(0.4);
				p.Tone(Wave.Sine, Hz(D5), Envelope.Perc(0.003, 0.16), 0.05).Glide(Hz(D6), 0.5).Gain(0.35);
			}, 3);

			yield return r.Of("true_damage", "Dano verdadeiro: seco, limpo, com um \"tink\" que atravessa.", p =>
			{
				Strike.Impact(p, 0, 0.55, 1);
				p.Noise(NoiseColor.White, Envelope.Perc(0.0002, 0.004)).Bandpass(5000, 1).Gain(0.5);
				p.Tone(Wave.Sine, Hz(A6), Envelope.Perc(0.001, 0.12)).Gain(0.25);
			}, 3);

			yield return r.Of("absorbed", "Dano absorvido: a taça engole o golpe.", p =>
			{
				Arcane.Bowl(p, D5, 0, 0.4, 0.5);
				Strike.Impact(p, 0, 0.2, 0.4);
				p.Tone(Wave.Sine, Hz(A5), Envelope.Perc(0.005, 0.2)).Glide(Hz(D5), 0.6).Gain(0.35);
			}, 3, -1);

			yield return r.Of("reduced", "Dano reduzido: o golpe abafado na madeira.", p =>
			{
				Strike.Impact(p, 0, 0.3, 0.6);
				Foley.Knock(p, 0, p.Vary(420, 0.06), 0.06, 0.5);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.001, 0.05)).Lowpass(1200).Gain(0.4);
			}, 3, -1);

			yield return r.Of("nullified", "Dano nulo: um \"tink\" de vidro e nada mais.", p =>
			{
				Arcane.Glass(p, D6, 0, 0.25, 0.5);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.002, 0.1)).Bandpass(2500, 1).Gain(0.3);
				Arcane.Bowl(p, A5, 0, 0.35, 0.3);
			}, 3, -2);

			yield return r.Of("shield_absorb", "Escudo absorvendo: o golpe na redoma de cristal.", p =>
			{
				Arcane.Bowl(p, D5, 0, 0.5, 0.55);
				Arcane.Glass(p, A6, 0, 0.35, 0.35);
				Strike.Impact(p, 0, 0.3, 0.5);
				Arcane.Shimmer(p, 0, 0.25, Hz(D6), 0.2);
			}, 3);
		}
	}
}
