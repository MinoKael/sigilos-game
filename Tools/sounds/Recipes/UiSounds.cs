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
	/// A interface: toques pequenos de madeira, papel e couro com notinhas de marimba e celesta em Ré
	/// pentatônico. Curtos, secos (sem eco) e no volume mais baixo da biblioteca.
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

			yield return r.Of("button_hover", "Hover/foco: um brilho de celesta quase sem ataque.", p =>
			{
				Arcane.Celesta(p, Pick(p, A6, Fs6, B6), 0, 0.09, 0.6);
				p.Noise(NoiseColor.White, Envelope.Perc(0.0005, 0.012), 0).Bandpass(5500, 2).Gain(0.2);
			}, 3, -7);

			yield return r.Of("button_press", "Pressionar: a metade de baixo do clique, mais abafada.", p =>
			{
				Foley.Knock(p, 0, p.Vary(900, 0.04), 0.04, 1);
				p.Noise(NoiseColor.Pink, Envelope.Perc(0.001, 0.02), 0).Bandpass(1200, 1).Gain(0.3);
				Foley.Tick(p, 0, 2600, 0.35);
			});

			yield return r.Of("button_release", "Soltar: a metade de cima do clique, com uma nota leve.", p =>
			{
				Foley.Tick(p, 0, p.Vary(4200, 0.05), 0.6);
				Foley.Knock(p, 0, 1500, 0.025, 0.35);
				Arcane.Marimba(p, A6, 0.004, 0.07, 0.3);
			});

			yield return r.Of("item_select", "Selecionar item: corda dedilhada e quinta subindo na marimba.", p =>
			{
				p.Pluck(Hz(A5), 0.25).Bright(0.6).Gain(0.6);
				Arcane.Marimba(p, D6, 0, 0.14, 0.5);
				Arcane.Marimba(p, A6, 0.045, 0.14, 0.45);
				Foley.Tick(p, 0, 3800, 0.3);
			});

			yield return r.Of("item_deselect", "Desselecionar item: a quinta descendo, mais macia.", p =>
			{
				Arcane.Marimba(p, A6, 0, 0.1, 0.4);
				Arcane.Marimba(p, D6, 0.045, 0.12, 0.4);
				Foley.Knock(p, 0, 1000, 0.03, 0.4);
			}, levelDb: -2);

			yield return r.Of("tab_switch", "Trocar aba: a aba de couro passando e um toc.", p =>
			{
				Foley.Swish(p, 0, 0.07, 1400, 4200, 0.35);
				Foley.Knock(p, 0.035, p.Vary(1350, 0.03), 0.04, 0.8);
				Arcane.Celesta(p, Pick(p, E6, Fs6), 0.035, 0.1, 0.35);
			}, 2);

			yield return r.Of("menu_open", "Abrir menu: couro, um sopro subindo e Ré–Fá♯–Lá na marimba.", p =>
			{
				Foley.Leather(p, 0, 0.5);
				Foley.Swish(p, 0, 0.12, 700, 2600, 0.4);
				Arcane.Arpeggio(p, new[] { D5, Fs5, A5 }, 0.03, 0.035, (note, at, _) => Arcane.Marimba(p, note, at, 0.18, 0.45));
				Arcane.Celesta(p, D6, 0.14, 0.15, 0.25);
			});

			yield return r.Of("menu_close", "Fechar menu: o arpejo descendo e a capa assentando.", p =>
			{
				Foley.Swish(p, 0, 0.1, 2600, 800, 0.35);
				Arcane.Arpeggio(p, new[] { A5, Fs5, D5 }, 0, 0.035, (note, at, _) => Arcane.Marimba(p, note, at, 0.15, 0.45));
				Foley.Knock(p, 0.11, 700, 0.05, 0.6);
				Foley.Leather(p, 0.11, 0.35);
			});

			yield return r.Of("panel_open", "Abrir painel: o painel deslizando e uma celesta.", p =>
			{
				Foley.Swish(p, 0, 0.14, 500, 3000, 0.5);
				Foley.Knock(p, 0.02, 1100, 0.04, 0.5);
				Arcane.Celesta(p, D6, 0.09, 0.2, 0.4);
				Arcane.Celesta(p, A6, 0.12, 0.2, 0.25);
			});

			yield return r.Of("panel_close", "Fechar painel: o painel voltando e encostando.", p =>
			{
				Foley.Swish(p, 0, 0.12, 3000, 600, 0.45);
				Foley.Knock(p, 0.1, 800, 0.05, 0.7);
				Arcane.Celesta(p, A5, 0.03, 0.14, 0.25);
			});

			yield return r.Of("dialog_open", "Abrir diálogo: um bilhete desdobrando com uma nota de vidro.", p =>
			{
				Foley.Paper(p, 0, 0.1, 0.35);
				Arcane.Glass(p, A5, 0.02, 0.3, 0.45);
				Arcane.Celesta(p, E6, 0.06, 0.25, 0.4);
				Foley.Tick(p, 0, 4000, 0.25);
			});

			yield return r.Of("dialog_close", "Fechar diálogo: o bilhete dobrando, a nota descendo.", p =>
			{
				Foley.Paper(p, 0, 0.08, 0.3);
				Arcane.Celesta(p, E6, 0, 0.15, 0.35);
				Arcane.Glass(p, A5, 0.05, 0.2, 0.4);
				Foley.Knock(p, 0.06, 900, 0.03, 0.4);
			});

			yield return r.Of("confirm", "Confirmar ação: Ré e Lá firmes, com o brilho da celesta.", p =>
			{
				Foley.Tick(p, 0, 3600, 0.4);
				Arcane.Marimba(p, D6, 0, 0.16, 0.6);
				Arcane.Marimba(p, A6, 0.06, 0.22, 0.6);
				Arcane.Celesta(p, D7, 0.06, 0.2, 0.25);
				Arcane.Glass(p, A6, 0.06, 0.3, 0.15);
			});

			yield return r.Of("cancel", "Cancelar ação: um toc mais grave e a quarta descendo.", p =>
			{
				Foley.Knock(p, 0, 700, 0.05, 0.7);
				p.Modal(Modes.Marimba, Hz(A5), 0.12).Bright(0.6).Gain(0.45);
				p.Modal(Modes.Marimba, Hz(E5), 0.16, 0.07).Bright(0.6).Gain(0.45);
			});

			yield return r.Of("back", "Voltar: a folha passando para trás e duas notas descendo.", p =>
			{
				Foley.Swish(p, 0, 0.08, 3200, 1100, 0.35);
				Arcane.Marimba(p, E6, 0.03, 0.1, 0.4);
				Arcane.Marimba(p, A5, 0.07, 0.12, 0.4);
				Foley.Knock(p, 0.07, 950, 0.03, 0.3);
			});

			yield return r.Of("forward", "Avançar: a folha passando para a frente e duas notas subindo.", p =>
			{
				Foley.Swish(p, 0, 0.08, 1100, 3200, 0.35);
				Arcane.Marimba(p, A5, 0.03, 0.1, 0.4);
				Arcane.Marimba(p, E6, 0.07, 0.12, 0.4);
				Foley.Knock(p, 0.03, 1150, 0.03, 0.3);
			});

			yield return r.Of("scroll", "Scroll: um dente de catraca de madeira, bem baixinho.", p =>
			{
				Foley.Tick(p, 0, p.Vary(3200, 0.1), 0.6);
				Foley.Knock(p, 0, p.Vary(1800, 0.06), 0.015, 0.4);
			}, 3, -9);

			yield return r.Of("page_change", "Trocar página: uma folha curta virando.", p =>
			{
				Foley.PageFlip(p, 0, 0.14, 0.9);
				Arcane.Celesta(p, D6, 0.1, 0.12, 0.25);
			});

			yield return r.Of("drag_item", "Arrastar item: a peça levantando, um \"blup\" subindo.", p =>
			{
				Foley.Leather(p, 0, 0.4);
				p.Tone(Wave.Sine, Hz(A5), Envelope.Perc(0.003, 0.09)).Glide(Hz(D6), 0.5).Gain(0.6);
				Foley.Swish(p, 0, 0.09, 900, 2400, 0.3);
			});

			yield return r.Of("drop_item", "Soltar item: a peça assentando, um \"plop\" descendo.", p =>
			{
				p.Tone(Wave.Sine, Hz(D6), Envelope.Perc(0.002, 0.08)).Glide(Hz(A5), 0.6).Gain(0.5);
				Foley.Knock(p, 0.05, 800, 0.05, 0.8);
				Foley.Leather(p, 0.05, 0.35);
			});

			yield return r.Of("equip_item", "Equipar item: o couro, a fivela e a quinta subindo.", p =>
			{
				Foley.Leather(p, 0, 0.6);
				Foley.Clink(p, 0.03, p.Vary(2300, 0.04), 0.45);
				Arcane.Marimba(p, D6, 0.05, 0.14, 0.4);
				Arcane.Marimba(p, A6, 0.09, 0.18, 0.4);
			});

			yield return r.Of("unequip_item", "Desequipar item: a fivela abrindo e a quinta descendo.", p =>
			{
				Foley.Clink(p, 0, p.Vary(1900, 0.04), 0.4);
				Foley.Leather(p, 0.03, 0.5);
				Arcane.Marimba(p, A6, 0.04, 0.1, 0.35);
				Arcane.Marimba(p, D6, 0.08, 0.12, 0.35);
			});

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

			yield return r.Of("option_choose", "Escolher opção: corda e celesta na quinta.", p =>
			{
				p.Pluck(Hz(D6), 0.2).Bright(0.7).Gain(0.6);
				Arcane.Celesta(p, A6, 0.03, 0.18, 0.45);
				Foley.Tick(p, 0, 3800, 0.3);
			});

			yield return r.Of("filter_applied", "Filtro aplicado: uma peneirada de papel e duas notas.", p =>
			{
				Foley.Paper(p, 0, 0.07, 0.3, 2800);
				Arcane.Marimba(p, E6, 0, 0.08, 0.35);
				Arcane.Marimba(p, A6, 0.05, 0.12, 0.4);
				Foley.Tick(p, 0.05, 4200, 0.3);
			});

			yield return r.Of("sort_changed", "Ordenação alterada: três cartas de madeira se arrumando.", p =>
			{
				for (var i = 0; i < 3; i++)
					Foley.Knock(p, i * 0.035, 900 + 200 * i, 0.03, 0.55);
				Foley.Paper(p, 0, 0.12, 0.25);
				Arcane.Marimba(p, D6, 0.1, 0.1, 0.3);
			});

			yield return r.Of("item_locked", "Item bloqueado: o cadeado fechando.", p =>
			{
				Foley.Clink(p, 0, 1700, 0.6);
				Foley.Knock(p, 0.04, 620, 0.05, 0.9);
				Arcane.Marimba(p, D5, 0.04, 0.1, 0.25);
			});

			yield return r.Of("item_unlocked", "Item desbloqueado: o cadeado abrindo e um arpejo de celesta.", p =>
			{
				Foley.Clink(p, 0, 2200, 0.55);
				Foley.Knock(p, 0.03, 1000, 0.03, 0.5);
				Arcane.Arpeggio(p, new[] { D6, Fs6, A6 }, 0.05, 0.04, (note, at, _) => Arcane.Celesta(p, note, at, 0.18, 0.35));
			});

			yield return r.Of("interaction_error", "Erro de interação: duas notas abafadas em trítono, sem susto.", p =>
			{
				p.Modal(Modes.Marimba, Hz(Gs5), 0.14).Bright(0.5).Gain(0.5);
				p.Modal(Modes.Marimba, Hz(D5), 0.2, 0.09).Bright(0.5).Gain(0.55);
				Foley.Knock(p, 0.09, 380, 0.06, 0.5);
			});

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
