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
				"Cada Glifo também empresta o desenho a dois conjuntos de runas, e a Invocação Ritual pode ser direcionada para os Glifos que você já conhece.");

			foreach (var glyph in Enum.GetValues<Glyph>())
			{
				var owners = _database.Summons.Where(s => s.Glyph == glyph).Select(s => s.Name).ToList();
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 14);
				row.AddChild(Doodle.Icon(Art.Glyph(glyph), 56, Palette.Gold));

				var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(new Label { Text = $"{Texts.Name(glyph)} · {Texts.School(glyph)}", ThemeTypeVariation = GameTheme.Heading });
				text.AddChild(new Label { Text = Texts.Meaning(glyph), AutowrapMode = TextServer.AutowrapMode.WordSmart });
				foreach (var set in RuneSets.Of(glyph))
				{
					text.AddChild(new Label
					{
						Text = $"Runas de {Texts.Name(set.Set)}: {Texts.Describe(set)}",
						ThemeTypeVariation = GameTheme.Faded,
						AutowrapMode = TextServer.AutowrapMode.WordSmart,
					});
				}
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
			Paragraph(column, "Efeitos duram turnos de quem os recebe. Os negativos passam pela Resistência do alvo menos a Precisão de quem lança, " +
				$"a chance de barrar nunca fica abaixo de {BattleRules.MinResistChance * 100:0}%. A Imunidade barra todos. " +
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
			Paragraph(column, "Cada invocação tem 6 espaços de runa em círculo. Os espaços 1, 3 e 5 dão sempre Ataque, " +
				"Defesa e Vida fixos; o 2 pode ter Velocidade, o 4 Crítico ou Dano crítico, o 6 Resistência ou Precisão, e os três podem ter " +
				"Vida, Ataque ou Defesa, fixos ou em porcentagem. Porcentagem é sempre sobre o atributo base.");
			Paragraph(column, $"Estrelas (1 a {RuneRules.MaxGrade}) decidem o tamanho de todos os números. A cor é a raridade: Normal (branca) não tem " +
				"subatributo, Mágica (verde) tem 1, Rara (azul) 2, Heroica (roxa) 3 e Lendária (laranja) 4. Às vezes a runa vem com um atributo " +
				"nativo, que nunca cresce.");
			Paragraph(column, $"Melhorar com Pó de Sigilo vai de +0 a +{RuneRules.MaxLevel}. Em +3, +6, +9 e +12 entra um subatributo novo (até " +
				$"{RuneRules.MaxSubstats}) ou, com {RuneRules.MaxSubstats}, um deles cresce; em +15 o principal dá um salto. A melhora nunca falha" +
				"e o Pó é escasso.");
			Paragraph(column, $"Exemplo de custo: uma runa {Texts.Stars(5)} de +0 a +12 custa {Cost(5, 12)} Pó; uma {Texts.Stars(6)} de +0 a +15, {Cost(6, 15)}.");
			Paragraph(column, $"Pedra de Afiar: soma um bônus a um subatributo de Vida, Ataque, Defesa ou Velocidade do mesmo tipo da pedra; uma pedra " +
				$"nova troca o bônus antigo. Gema Encantada: troca um subatributo de uma runa +{RuneForge.EnchantLevel} por outro, e só um por runa. " +
				"As duas saem das fases a partir da 10 e servem em qualquer conjunto.");
			Paragraph(column, $"Runas saem das fases da Campanha: sempre na primeira vitória, com {Campaign.RepeatRuneChance * 100:0}% de chance depois. " +
				"Fases mais altas dão runas com mais estrelas.");

			column.AddChild(new Label { Text = "Conjuntos", ThemeTypeVariation = GameTheme.Heading });
			Paragraph(column, "Com 6 espaços cabem um conjunto de 4 peças e um de 2, ou três de 2 (três iguais valem três vezes).");
			foreach (var set in RuneSets.All)
			{
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 14);
				row.AddChild(Doodle.Icon(Art.Glyph(set.Glyph), 32, Palette.Gold));
				row.AddChild(new Label { Text = Texts.Name(set.Set), CustomMinimumSize = new Vector2(130, 0) });
				row.AddChild(new Label { Text = Texts.Describe(set), AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill });
				column.AddChild(row);
			}
		}

		private static int Cost(int grade, int level) =>
			RuneRules.UpgradeCost(new Rune { Grade = grade }, level);

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
				$"+{Awakening.HealthBonus * 100:0}% de Vida, +{Awakening.AttackDefenseBonus * 100:0}% de Ataque e Defesa e um bônus: " +
				$"{Texts.AwakeningBonus(Stat.Speed)}, {Texts.AwakeningBonus(Stat.Crit)}, {Texts.AwakeningBonus(Stat.Resistance)} ou {Texts.AwakeningBonus(Stat.Accuracy)}.");
			Paragraph(column, "Líder: a primeira invocação do time aplica a Liderança dela a todos, se tiver uma. A porcentagem " +
				"é sobre o atributo de base, não sobre o que veio das runas.");
			Paragraph(column, $"Dano: Ataque × multiplicador da habilidade × {BattleRules.DefenseConstant:0} / ({BattleRules.DefenseConstant:0} + Defesa do alvo) — " +
				$"a curva de Defesa: Defesa {BattleRules.DefenseConstant:0} corta o dano pela metade. Crítico multiplica por 1 + Dano crítico " +
				"(50% de base). Toda invocação começa com 15% de Crítico, 50% de Dano crítico, 15% de Resistência e 0% de Precisão.");
		}

		private static void Paragraph(VBoxContainer column, string text) =>
			column.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1100, 0) });
	}
}
