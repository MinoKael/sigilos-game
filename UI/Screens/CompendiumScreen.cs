using System;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O Compêndio: o que são os Glifos e para que servem, a roda de elementos, cada efeito, como as
	/// runas funcionam e as regras de combate. Todo número vem das regras do Core, não de texto escrito
	/// à mão, então a explicação acompanha o balanceamento.
	/// </summary>
	public partial class CompendiumScreen : Control
	{
		private readonly GameDatabase _database;

		public CompendiumScreen(GameDatabase database)
		{
			_database = database;
		}

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = "Compêndio", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var back = new Button { Text = "Voltar ao Santuário" };
			back.Pressed += () => BackRequested?.Invoke();
			header.AddChild(back);
			page.AddChild(header);

			var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			tabs.AddChild(Tab("Glifos", Glyphs));
			tabs.AddChild(Tab("Elementos", Elements));
			tabs.AddChild(Tab("Efeitos", Statuses));
			tabs.AddChild(Tab("Runas", Runes));
			tabs.AddChild(Tab("Combate", Combat));
			page.AddChild(tabs);
		}

		private static Control Tab(string name, Action<VBoxContainer> fill)
		{
			var scroll = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 10);
			scroll.AddChild(column);
			fill(column);
			return scroll;
		}

		private void Glyphs(VBoxContainer column)
		{
			Paragraph(column, "Tudo neste mundo foi escrito com oito Glifos. Cada invocação carrega um: ele diz o estilo das habilidades dela. " +
				"O mesmo Glifo dá nome a um conjunto de runas, e a Invocação Ritual pode ser direcionada para os Glifos que você já conhece.");

			foreach (var glyph in Enum.GetValues<Glyph>())
			{
				var owners = _database.Summons.Where(s => s.Glyph == glyph).Select(s => s.Name).ToList();
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 14);
				row.AddChild(Doodle.Icon(Art.Glyph(glyph), 56, Palette.Gold));

				var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(new Label { Text = $"{Texts.Name(glyph)} · {Texts.School(glyph)}", ThemeTypeVariation = GameTheme.Heading });
				text.AddChild(new Label { Text = Texts.Meaning(glyph), AutowrapMode = TextServer.AutowrapMode.WordSmart });
				text.AddChild(new Label { Text = $"Conjunto de runas: {Texts.Describe(RuneSets.For(glyph))}", ThemeTypeVariation = GameTheme.Faded });
				text.AddChild(new Label
				{
					Text = owners.Count == 0 ? "Nenhuma invocação ainda." : $"Invocações: {string.Join(", ", owners)}",
					ThemeTypeVariation = GameTheme.Faded,
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
				});
				row.AddChild(text);
				column.AddChild(row);
			}
		}

		private static void Elements(VBoxContainer column)
		{
			Paragraph(column, $"Fogo vence Vento, Vento vence Água, Água vence Fogo. Luz e Trevas vencem uma à outra. " +
				$"Com vantagem, o dano sobe {(BattleRules.AdvantageMultiplier - 1) * 100:0}%; com desvantagem, cai {(1 - BattleRules.DisadvantageMultiplier) * 100:0}%. " +
				"O automático mira primeiro em quem ele vence.");

			foreach (var element in Enum.GetValues<Element>())
			{
				var beats = Enum.GetValues<Element>().Where(other => ElementChart.HasAdvantage(element, other)).Select(Texts.Name);
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 14);
				row.AddChild(Doodle.Icon(Art.Element(element), 40, Palette.Of(element)));
				var name = new Label { Text = Texts.Name(element), CustomMinimumSize = new Vector2(120, 0), ThemeTypeVariation = GameTheme.Heading };
				name.AddThemeColorOverride("font_color", Palette.Of(element));
				row.AddChild(name);
				row.AddChild(new Label { Text = $"vence {string.Join(" e ", beats)}" });
				column.AddChild(row);
			}
		}

		private static void Statuses(VBoxContainer column)
		{
			Paragraph(column, "Efeitos duram turnos de quem os recebe. Os negativos passam pela Resistência do alvo, menos o Foco de quem lança. " +
				"No cartão de cada unidade aparecem pela sigla; passe o mouse para ver a duração.");

			foreach (var status in Enum.GetValues<StatusKind>())
			{
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 14);
				var tag = new Label { Text = Texts.Short(status), CustomMinimumSize = new Vector2(56, 0) };
				tag.AddThemeColorOverride("font_color", BattleRules.IsNegative(status) ? Palette.Negative : Palette.Positive);
				row.AddChild(tag);
				row.AddChild(new Label { Text = Texts.Name(status), CustomMinimumSize = new Vector2(150, 0) });
				row.AddChild(new Label { Text = Texts.Explain(status), ThemeTypeVariation = GameTheme.Faded });
				column.AddChild(row);
			}
		}

		private static void Runes(VBoxContainer column)
		{
			Paragraph(column, "Cada invocação tem 6 espaços de runa em círculo. Os espaços 1, 3 e 5 dão sempre Ataque, Defesa e Vida; " +
				"os espaços 2, 4 e 6 variam (Velocidade, Crítico, Resistência, Foco ou porcentagens).");
			Paragraph(column, $"Toda runa tem 1 a {RuneRules.MaxGrade} estrelas, um atributo principal e {RuneRules.SubstatCount} subatributos. " +
				$"Melhorar com Pó de Sigilo sobe o principal até +{RuneRules.MaxLevel}; em +3, +6 e +9 um subatributo ao acaso cresce. " +
				"Refazer troca um subatributo por outro; desfazer devolve Pó.");
			Paragraph(column, "Runas saem das fases da Campanha: sempre na primeira vitória, com 50% de chance depois. Fases mais altas dão runas com mais estrelas.");

			column.AddChild(new Label { Text = "Conjuntos", ThemeTypeVariation = GameTheme.Heading });
			foreach (var set in RuneSets.All)
			{
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 14);
				row.AddChild(Doodle.Icon(Art.Glyph(set.Set), 32, Palette.Gold));
				row.AddChild(new Label { Text = Texts.Name(set.Set), CustomMinimumSize = new Vector2(110, 0) });
				row.AddChild(new Label { Text = Texts.Describe(set) });
				column.AddChild(row);
			}
		}

		private static void Combat(VBoxContainer column)
		{
			Paragraph(column, "Cada unidade tem uma barra de Ímpeto que enche em proporção à Velocidade; quem chega a 100% age. " +
				"Empurrar ou atrasar o Ímpeto muda a ordem dos turnos — a barra \"Próximos\" mostra quem vem.");
			Paragraph(column, $"Éter: recurso do time, de 0 a {BattleRules.MaxEther}. O básico não gera Éter; cada habilidade de Glifo gera " +
				$"{BattleRules.GlyphEtherGain} e cada inimigo derrubado, {BattleRules.KillEtherGain}. Ele paga as versões aprimoradas das habilidades, " +
				"marcando \"Aprimorar\" antes de escolher. Só no modo manual: o automático nunca gasta Éter.");
			Paragraph(column, $"Uma luta tem até 3 ondas e acaba em derrota se passar de {BattleRules.RoundLimit} rodadas.");
			Paragraph(column, $"Nível: de 1 a {Leveling.MaxLevel}. Vitórias dão experiência ao time; a Essência pode ser infundida como experiência na tela de Monstros.");
			Paragraph(column, $"Despertar: paga Essência e a invocação ganha nome próprio, desenho novo, estrelas roxas, Assinatura melhorada, " +
				$"+{Awakening.StatBonus * 100:0}% de Vida, Ataque e Defesa e um atributo extra.");
			Paragraph(column, "Líder: a primeira invocação do time aplica a Liderança dela a todos, se tiver uma.");
		}

		private static void Paragraph(VBoxContainer column, string text) =>
			column.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1100, 0) });
	}
}
