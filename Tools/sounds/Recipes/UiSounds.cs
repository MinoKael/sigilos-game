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
	/// A interface: toques pequenos de madeira e couro com notinhas de marimba, celesta e harpa na
	/// pentatônica de Mi. Curtos, quase secos e no volume mais baixo da biblioteca; os cliques são a medida
	/// de intensidade de tudo.
	/// </summary>
	internal static class UiSounds
	{
		public static IEnumerable<SoundDef> All()
		{
			var r = new Recipe("ui", Mix.Ui);

			yield return r.Of("button_click", "Clique de botão: toc de madeira com uma notinha de marimba.", p =>
			{
				Foley.Knock(p, 0, p.Vary(1250, 0.04), 0.05, 1);
				Foley.Tick(p, 0, p.Vary(3600, 0.08), 0.5);
				Arcane.Marimba(p, Pick(p, D6, E6, D6), 0.004, 0.12, 0.35);
			}, 3);

			yield return r.Of("tab_switch", "Trocar aba: um toc de madeira e uma nota de celesta.", p =>
			{
				Foley.Swish(p, 0, 0.06, 1000, 2600, 0.15);
				Foley.Knock(p, 0.01, p.Vary(1300, 0.03), 0.035, 0.6);
				Arcane.Celesta(p, Pick(p, E6, D6), 0.02, 0.12, 0.35);
			}, 2, -2.5);

			yield return r.Of("panel_open", "Abrir painel: uma nota de harpa, a celesta por cima e um sopro leve.", p =>
			{
				Foley.Swish(p, 0, 0.14, 500, 1600, 0.12);
				Arcane.Harp(p, Pick(p, B5, A5), 0, 0.3, 0.7);
				Arcane.Celesta(p, Pick(p, E6, D6), 0.035, 0.25, 0.35);
				p.Reverb(0.08, 0.4);
			}, 2, -2);

			yield return r.Of("dialog_open", "Abrir diálogo: o bilhete abrindo e a quarta subindo na celesta.", p =>
			{
				Foley.Swish(p, 0, 0.1, 800, 2400, 0.15);
				Arcane.Celesta(p, B5, 0, 0.22, 0.45);
				Arcane.Celesta(p, E6, 0.05, 0.25, 0.4);
			}, levelDb: -3);

			yield return r.Of("dialog_close", "Fechar diálogo: o bilhete fechando e a quarta descendo.", p =>
			{
				Foley.Swish(p, 0, 0.08, 2400, 800, 0.12);
				Arcane.Celesta(p, E6, 0, 0.15, 0.35);
				Arcane.Celesta(p, B5, 0.05, 0.2, 0.4);
				Foley.Knock(p, 0.05, 800, 0.03, 0.3);
			}, levelDb: -3);

			yield return r.Of("back", "Voltar: a folha passando para trás e duas notas descendo.", p =>
			{
				Foley.Swish(p, 0, 0.08, 3200, 1100, 0.35);
				Arcane.Marimba(p, E6, 0.03, 0.1, 0.4);
				Arcane.Marimba(p, A5, 0.07, 0.12, 0.4);
				Foley.Knock(p, 0.07, 950, 0.03, 0.3);
			});

			yield return r.Of("equip_item", "Equipar item: o couro, um toc e a quinta subindo.", p =>
			{
				Foley.Leather(p, 0, 0.5);
				Foley.Knock(p, 0.03, p.Vary(1100, 0.04), 0.04, 0.4);
				Arcane.Marimba(p, D6, 0.05, 0.14, 0.4);
				Arcane.Marimba(p, A6, 0.09, 0.18, 0.4);
			}, levelDb: -2.5);

			yield return r.Of("unequip_item", "Desequipar item: um toc, o couro e a quinta descendo.", p =>
			{
				Foley.Knock(p, 0, p.Vary(900, 0.04), 0.04, 0.4);
				Foley.Leather(p, 0.03, 0.45);
				Arcane.Marimba(p, A6, 0.04, 0.1, 0.35);
				Arcane.Marimba(p, D6, 0.08, 0.12, 0.35);
			}, levelDb: -2.5);

			yield return r.Of("toggle_on", "Ativar toggle: o estalo da chave e um tom subindo.", p =>
			{
				Foley.Tick(p, 0, 4200, 0.5);
				Foley.Knock(p, 0, 1300, 0.03, 0.6);
				p.Tone(Wave.Sine, Hz(D6), Envelope.Perc(0.002, 0.12)).Glide(Hz(A6), 0.4, 0.03).Gain(0.45);
			});

			yield return r.Of("toggle_off", "Desativar toggle: o estalo mais grave e o tom descendo.", p =>
			{
				Foley.Tick(p, 0, 3000, 0.45);
				Foley.Knock(p, 0, 900, 0.03, 0.6);
				p.Tone(Wave.Sine, Hz(A6), Envelope.Perc(0.002, 0.1)).Glide(Hz(D6), 0.4, 0.03).Gain(0.35);
			});

			yield return r.Of("slider_up", "Slider aumentando: um dente com o tom subindo um pouco.", p =>
			{
				Foley.Tick(p, 0, p.Vary(3400, 0.04), 0.5);
				p.Tone(Wave.Sine, Hz(Fs6), Envelope.Perc(0.001, 0.04)).Glide(Hz(A6), 1, 0.02).Gain(0.4);
			}, levelDb: -6);

			yield return r.Of("slider_down", "Slider diminuindo: um dente com o tom descendo um pouco.", p =>
			{
				Foley.Tick(p, 0, p.Vary(2800, 0.04), 0.5);
				p.Tone(Wave.Sine, Hz(D6), Envelope.Perc(0.001, 0.04)).Glide(Hz(B5), 1, 0.02).Gain(0.4);
			}, levelDb: -6);

			yield return r.Of("action_unavailable", "Ação indisponível: um toc-toc de madeira abafado.", p =>
			{
				p.Modal(Modes.Wood, 520, 0.07).Bright(0.4).Gain(0.8);
				p.Modal(Modes.Wood, 470, 0.08, 0.08).Bright(0.4).Gain(0.6);
				p.Modal(Modes.Marimba, Hz(D5), 0.12).Bright(0.3).Gain(0.3);
				p.Lowpass(2500);
			}, levelDb: -2);
		}
	}
}
