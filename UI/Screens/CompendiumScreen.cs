using System;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O Compêndio: as regras do jogo, em abas — como jogar, combate, atributos, Glifos, efeitos e
	/// runas. Cada tópico é um cartão curto com o seu símbolo. Todo número vem das regras do Core, então
	/// a explicação acompanha o balanceamento. O que existe no jogo (invocações, tabelas de runa, pedras)
	/// fica no Grimório.
	/// </summary>
	public partial class CompendiumScreen : Control
	{
		private const float CardWidth = 560;

		public CompendiumScreen()
		{
		}

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("compendio.titulo"), null, T("geral.voltar_santuario"), () => BackRequested?.Invoke()));

			var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			page.AddChild(tabs);
			Basics(Layout.Tab(tabs, T("compendio.aba.basico")));
			Combat(Layout.Tab(tabs, T("compendio.aba.combate")));
			Stats(Layout.Tab(tabs, T("compendio.aba.atributos")));
			Glyphs(Layout.Tab(tabs, T("compendio.aba.glifos")));
			Statuses(Layout.Tab(tabs, T("compendio.aba.efeitos")));
			Runes(Layout.Tab(tabs, T("compendio.aba.runas")));
		}

		private static void Basics(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, "campaign", T("compendio.basico.campanha.titulo"), T("compendio.basico.campanha.texto", GameDatabase.MaxCampaignRuneGrade));
			Card(grid, "dungeon", T("compendio.basico.masmorras.titulo"), T("compendio.basico.masmorras.texto"));
			Card(grid, "mana", T("compendio.basico.mana.titulo"), T("compendio.basico.mana.texto", Mana.BaseMax, Mana.BaseMax + Mana.MaxFromLevels, Mana.PerHour, Account.MaxLevel));
			Card(grid, "gold", T("compendio.basico.ouro.titulo"), T("compendio.basico.ouro.texto", Account.LevelUpGold));
			Card(grid, "summon", T("compendio.basico.invocacao.titulo"), T("compendio.basico.invocacao.texto"));
			Card(grid, "storage", T("compendio.basico.monstros.titulo"), T("compendio.basico.monstros.texto", PlayerState.CollectionCapacity, RuneInventory.Capacity));
			Card(grid, "team", T("compendio.basico.equipes.titulo"), T("compendio.basico.equipes.texto", PlayerState.TeamSize));
			Card(grid, "essence", T("compendio.basico.nivel.titulo"), T("compendio.basico.nivel.texto", Leveling.MaxLevel));
			Card(grid, "fragments", T("compendio.basico.ecos.titulo"), T("compendio.basico.ecos.texto", Growth.MaxEchoes, Texts.Percent(Growth.SkillPowerPerEcho), Texts.Percent(Growth.FullEchoStatBonus)));
			Card(grid, "grimoire", T("compendio.basico.despertar.titulo"), T("compendio.basico.despertar.texto",
				Texts.Percent(Awakening.HealthBonus), Texts.Percent(Awakening.AttackDefenseBonus),
				Texts.AwakeningBonus(Stat.Speed), Texts.AwakeningBonus(Stat.Crit), Texts.AwakeningBonus(Stat.Resistance), Texts.AwakeningBonus(Stat.Accuracy)));
		}

		private static void Combat(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, RuneSets.For(RuneSet.Nemesis).Glyph, T("compendio.combate.impeto.titulo"), T("compendio.combate.impeto.texto", Texts.Impeto));
			Card(grid, "essence", T("compendio.combate.eter.titulo"), T("compendio.combate.eter.texto", Texts.Ether, BattleRules.MaxEther, BattleRules.SpecialEtherGain, BattleRules.KillEtherGain, BattleRules.MinEnhanceCost));
			Card(grid, "campaign", T("compendio.combate.luta.titulo"), T("compendio.combate.luta.texto", PlayerState.TeamSize, GameDatabase.MaxWaves, GameDatabase.MaxEnemiesPerWave, BattleRules.RoundLimit));
			Card(grid, Texts.GlyphOf(Stat.Defense), T("compendio.combate.dano.titulo"), T("compendio.combate.dano.texto", Math.Round(BattleRules.DefenseConstant)));
			Card(grid, Texts.GlyphOf(Stat.Crit), T("compendio.combate.critico.titulo"), T("compendio.combate.critico.texto"));
			Card(grid, Texts.GlyphOf(Stat.Resistance), T("compendio.combate.resistencia.titulo"), T("compendio.combate.resistencia.texto", Texts.Percent(BattleRules.MinResistChance)));
			Card(grid, "team", T("compendio.combate.lider.titulo"), T("compendio.combate.lider.texto"));
			Card(grid, "search", T("compendio.combate.automatico.titulo"), T("compendio.combate.automatico.texto", Texts.Ether));

			column.AddChild(new Label { Text = T("compendio.combate.elementos"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(Layout.Text(T("compendio.combate.elementos_texto", Texts.Percent(BattleRules.AdvantageMultiplier - 1), Texts.Percent(1 - BattleRules.DisadvantageMultiplier)), GameTheme.Faded));
			var elements = new HFlowContainer();
			elements.AddThemeConstantOverride("h_separation", 24);
			foreach (var element in Enum.GetValues<Element>())
			{
				var beats = Enum.GetValues<Element>().Where(other => ElementChart.HasAdvantage(element, other)).Select(Texts.Name);
				var row = new HBoxContainer();
				row.AddChild(Doodle.Icon(Art.Element(element), 32, Palette.Of(element)));
				var name = new Label { Text = T("compendio.combate.vence", Texts.Name(element), string.Join(T("geral.e"), beats)) };
				name.AddThemeColorOverride("font_color", Palette.Of(element));
				row.AddChild(name);
				elements.AddChild(row);
			}

			column.AddChild(elements);
		}

		private static void Stats(VBoxContainer column)
		{
			column.AddChild(Layout.Text(T("compendio.atributos.intro"), GameTheme.Faded));
			var grid = Cards(column);
			foreach (var stat in Enum.GetValues<Stat>())
				Card(grid, Texts.GlyphOf(stat), Texts.Name(stat), Texts.Explain(stat));
		}

		private static void Glyphs(VBoxContainer column)
		{
			column.AddChild(RichText.Label(T("compendio.glifos.intro", Texts.Term(StatusKind.Stun)), 1150, GameTheme.Faded));
			var grid = new GridContainer { Columns = 4 };
			grid.AddThemeConstantOverride("h_separation", 10);
			grid.AddThemeConstantOverride("v_separation", 10);
			foreach (var set in RuneSets.All)
			{
				var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(284, 0) };
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 10);
				row.AddChild(Doodle.Icon(Art.Glyph(set.Glyph), 56, Palette.Gold));
				var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(new Label { Text = Texts.Name(set.Glyph), ThemeTypeVariation = GameTheme.Heading });
				var meaning = new Label { Text = Texts.Meaning(set.Glyph) };
				meaning.AddThemeColorOverride("font_color", Palette.Gold);
				text.AddChild(meaning);
				text.AddChild(new Label { Text = T("compendio.glifos.conjunto", Texts.Name(set.Set)), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(text);
				panel.AddChild(row);
				grid.AddChild(panel);
			}

			column.AddChild(grid);
		}

		private static void Statuses(VBoxContainer column)
		{
			column.AddChild(Layout.Text(T("compendio.efeitos.intro", Texts.Percent(BattleRules.MinResistChance)), GameTheme.Faded));
			var grid = Cards(column);
			foreach (var status in Enum.GetValues<StatusKind>())
			{
				var tag = BattleRules.IsNegative(status) ? T("compendio.efeitos.negativo") : T("compendio.efeitos.positivo");
				var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(CardWidth, 0) };
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 10);
				var shortTag = new Label { Text = Texts.Short(status), CustomMinimumSize = new Vector2(52, 0), VerticalAlignment = VerticalAlignment.Center };
				shortTag.AddThemeColorOverride("font_color", BattleRules.IsNegative(status) ? Palette.Negative : Palette.Positive);
				row.AddChild(shortTag);
				row.AddChild(RichText.Label($"{Texts.Term(status)}  [color=#{Palette.TextFaded.ToHtml(false)}]{tag}[/color]\n{Texts.Explain(status)}", CardWidth - 80));
				panel.AddChild(row);
				grid.AddChild(panel);
			}
		}

		private static void Runes(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, "rune", T("compendio.runas.espacos.titulo"), T("compendio.runas.espacos.texto"));
			Card(grid, "rune", T("compendio.runas.estrelas.titulo"), T("compendio.runas.estrelas.texto", RuneRules.MaxGrade));
			Card(grid, "essence", T("compendio.runas.melhora.titulo"), T("compendio.runas.melhora.texto", RuneRules.MaxLevel, RuneRules.MaxSubstats));
			Card(grid, RuneSets.For(RuneSet.Violent).Glyph, T("compendio.runas.conjuntos.titulo"), T("compendio.runas.conjuntos.texto"));
			Card(grid, "grindstone", T("compendio.runas.afiar.titulo"), T("compendio.runas.afiar.texto"));
			Card(grid, "gem", T("compendio.runas.gema.titulo"), T("compendio.runas.gema.texto", RuneForge.EnchantLevel));
			Card(grid, "dungeon", T("compendio.runas.onde.titulo"), T("compendio.runas.onde.texto", GameDatabase.MaxCampaignRuneGrade, Texts.Percent(Campaign.RepeatRuneChance)));
			Card(grid, "grimoire", T("compendio.runas.grimorio.titulo"), T("compendio.runas.grimorio.texto"));
		}

		private static GridContainer Cards(VBoxContainer column)
		{
			var grid = new GridContainer { Columns = 2 };
			grid.AddThemeConstantOverride("h_separation", 12);
			grid.AddThemeConstantOverride("v_separation", 12);
			column.AddChild(grid);
			return grid;
		}

		private static void Card(GridContainer grid, string icon, string title, string text) => Card(grid, Art.Icon(icon), title, text);

		private static void Card(GridContainer grid, Glyph glyph, string title, string text) => Card(grid, Art.Glyph(glyph), title, text);

		/// <summary>Um tópico: símbolo à esquerda, título e texto rico.</summary>
		private static void Card(GridContainer grid, Texture2D? icon, string title, string text)
		{
			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(CardWidth, 0) };
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			row.AddChild(Doodle.Icon(icon, 44, Palette.Gold));
			var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			content.AddChild(new Label { Text = title, ThemeTypeVariation = GameTheme.Heading });
			content.AddChild(RichText.Label(text, CardWidth - 76));
			row.AddChild(content);
			panel.AddChild(row);
			grid.AddChild(panel);
		}
	}
}
