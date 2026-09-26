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
			page.AddChild(Layout.Header(T("compendium.title"), null, T("common.back_to_hub"), () => BackRequested?.Invoke()));

			var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			page.AddChild(tabs);
			Basics(Layout.Tab(tabs, T("compendium.tab.basic")));
			Combat(Layout.Tab(tabs, T("compendium.tab.combat")));
			Stats(Layout.Tab(tabs, T("compendium.tab.stats")));
			Glyphs(Layout.Tab(tabs, T("compendium.tab.glyphs")));
			Statuses(Layout.Tab(tabs, T("compendium.tab.effects")));
			Runes(Layout.Tab(tabs, T("compendium.tab.runes")));
		}

		private static void Basics(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, "campaign", T("compendium.basic.campaign.title"), T("compendium.basic.campaign.text", GameDatabase.MaxCampaignRuneGrade));
			Card(grid, "dungeon", T("compendium.basic.dungeons.title"), T("compendium.basic.dungeons.text"));
			Card(grid, "mana", T("compendium.basic.mana.title"), T("compendium.basic.mana.text", Mana.BaseMax, Mana.BaseMax + Mana.MaxFromLevels, Mana.PerHour, Account.MaxLevel));
			Card(grid, "gold", T("compendium.basic.gold.title"), T("compendium.basic.gold.text", Account.LevelUpGold));
			Card(grid, "summon", T("compendium.basic.summon.title"), T("compendium.basic.summon.text"));
			Card(grid, "storage", T("compendium.basic.monsters.title"), T("compendium.basic.monsters.text", PlayerState.CollectionCapacity, RuneInventory.Capacity));
			Card(grid, "team", T("compendium.basic.teams.title"), T("compendium.basic.teams.text", PlayerState.TeamSize));
			Card(grid, "essence", T("compendium.basic.level.title"), T("compendium.basic.level.text", Leveling.MaxLevel));
			Card(grid, "fragments", T("compendium.basic.echoes.title"), T("compendium.basic.echoes.text", Growth.MaxEchoes, Texts.Percent(Growth.SkillPowerPerEcho), Texts.Percent(Growth.FullEchoStatBonus)));
			Card(grid, "grimoire", T("compendium.basic.awaken.title"), T("compendium.basic.awaken.text",
				Texts.Percent(Awakening.HealthBonus), Texts.Percent(Awakening.AttackDefenseBonus),
				Texts.AwakeningBonus(Stat.Speed), Texts.AwakeningBonus(Stat.Crit), Texts.AwakeningBonus(Stat.Resistance), Texts.AwakeningBonus(Stat.Accuracy)));
		}

		private static void Combat(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, RuneSets.For(RuneSet.Nemesis).Glyph, T("compendium.combat.impetus.title"), T("compendium.combat.impetus.text", Texts.Impeto));
			Card(grid, "essence", T("compendium.combat.aether.title"), T("compendium.combat.aether.text", Texts.Ether, BattleRules.MaxEther, BattleRules.SpecialEtherGain, BattleRules.KillEtherGain, BattleRules.MinEnhanceCost));
			Card(grid, "campaign", T("compendium.combat.fight.title"), T("compendium.combat.fight.text", PlayerState.TeamSize, GameDatabase.MaxWaves, GameDatabase.MaxEnemiesPerWave, BattleRules.RoundLimit));
			Card(grid, Texts.GlyphOf(Stat.Defense), T("compendium.combat.damage.title"), T("compendium.combat.damage.text", Math.Round(BattleRules.DefenseConstant)));
			Card(grid, Texts.GlyphOf(Stat.Crit), T("compendium.combat.crit.title"), T("compendium.combat.crit.text"));
			Card(grid, Texts.GlyphOf(Stat.Resistance), T("compendium.combat.resistance.title"), T("compendium.combat.resistance.text", Texts.Percent(BattleRules.MinResistChance)));
			Card(grid, "team", T("compendium.combat.leader.title"), T("compendium.combat.leader.text"));
			Card(grid, "search", T("compendium.combat.auto.title"), T("compendium.combat.auto.text", Texts.Ether));

			column.AddChild(new Label { Text = T("compendium.combat.elements"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(Layout.Text(T("compendium.combat.elements_text", Texts.Percent(BattleRules.AdvantageMultiplier - 1), Texts.Percent(1 - BattleRules.DisadvantageMultiplier)), GameTheme.Faded));
			var elements = new HFlowContainer();
			elements.AddThemeConstantOverride("h_separation", 24);
			foreach (var element in Enum.GetValues<Element>())
			{
				var beats = Enum.GetValues<Element>().Where(other => ElementChart.HasAdvantage(element, other)).Select(Texts.Name);
				var row = new HBoxContainer();
				row.AddChild(Doodle.Icon(Art.Element(element), 32, Palette.Of(element)));
				var name = new Label { Text = T("compendium.combat.wins", Texts.Name(element), string.Join(T("common.and"), beats)) };
				name.AddThemeColorOverride("font_color", Palette.Of(element));
				row.AddChild(name);
				elements.AddChild(row);
			}

			column.AddChild(elements);
		}

		private static void Stats(VBoxContainer column)
		{
			column.AddChild(Layout.Text(T("compendium.stats.intro"), GameTheme.Faded));
			var grid = Cards(column);
			foreach (var stat in Enum.GetValues<Stat>())
				Card(grid, Texts.GlyphOf(stat), Texts.Name(stat), Texts.Explain(stat));
		}

		private static void Glyphs(VBoxContainer column)
		{
			column.AddChild(RichText.Label(T("compendium.glyphs.intro", Texts.Term(StatusKind.Stun)), 1150, GameTheme.Faded));
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
				text.AddChild(new Label { Text = T("compendium.glyphs.set", Texts.Name(set.Set)), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(text);
				panel.AddChild(row);
				grid.AddChild(panel);
			}

			column.AddChild(grid);
		}

		private static void Statuses(VBoxContainer column)
		{
			column.AddChild(Layout.Text(T("compendium.effects.intro", Texts.Percent(BattleRules.MinResistChance)), GameTheme.Faded));
			var grid = Cards(column);
			foreach (var status in Enum.GetValues<StatusKind>())
			{
				var tag = BattleRules.IsNegative(status) ? T("compendium.effects.negative") : T("compendium.effects.positive");
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
			Card(grid, "rune", T("compendium.runes.slots.title"), T("compendium.runes.slots.text"));
			Card(grid, "rune", T("compendium.runes.stars.title"), T("compendium.runes.stars.text", RuneRules.MaxGrade));
			Card(grid, "essence", T("compendium.runes.upgrade.title"), T("compendium.runes.upgrade.text", RuneRules.MaxLevel, RuneRules.MaxSubstats));
			Card(grid, RuneSets.For(RuneSet.Violent).Glyph, T("compendium.runes.sets.title"), T("compendium.runes.sets.text"));
			Card(grid, "grindstone", T("compendium.runes.grind.title"), T("compendium.runes.grind.text"));
			Card(grid, "gem", T("compendium.runes.gem.title"), T("compendium.runes.gem.text", RuneForge.EnchantLevel));
			Card(grid, "dungeon", T("compendium.runes.where.title"), T("compendium.runes.where.text", GameDatabase.MaxCampaignRuneGrade, Texts.Percent(Campaign.RepeatRuneChance)));
			Card(grid, "grimoire", T("compendium.runes.grimoire.title"), T("compendium.runes.grimoire.text"));
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
