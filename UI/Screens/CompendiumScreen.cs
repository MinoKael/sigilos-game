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
	/// O Compêndio: as regras do jogo por inteiro, em abas escritas — como jogar, combate, atributos,
	/// Glifos, efeitos e runas —, cada tópico um cartão curto com o seu símbolo. As telas explicam o que
	/// o jogador precisa na hora (textos curtos e janelas ao tocar); aqui fica o porquê completo. Todo
	/// número vem das regras do Core, então a explicação acompanha o balanceamento. As cores de raridade
	/// (a moldura dos monstros, a cor das runas) têm legenda com exemplos de verdade. O que existe no
	/// jogo (invocações, tabelas de runa, pedras) fica no Grimório.
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
			page.AddChild(Layout.Header(T("destination.Compendium"), null, () => BackRequested?.Invoke()).Header);
			var sigils = new TextTabs { Name = "Tabs" };
			page.AddChild(sigils);

			var tabs = new TabContainer { Name = "Pages", SizeFlagsVertical = SizeFlags.ExpandFill, TabsVisible = false };
			page.AddChild(tabs);
			Basics(Layout.Tab(tabs, "Basics", T("compendium.tab.basic")));
			Combat(Layout.Tab(tabs, "Combat", T("compendium.tab.combat")));
			Stats(Layout.Tab(tabs, "Stats", T("compendium.tab.stats")));
			Glyphs(Layout.Tab(tabs, "Glyphs", T("compendium.tab.glyphs")));
			Statuses(Layout.Tab(tabs, "Effects", T("compendium.tab.effects")));
			Runes(Layout.Tab(tabs, "Runes", T("compendium.tab.runes")));

			sigils.Add(T("compendium.tab.basic"), "", "region").Name = "Basics";
			sigils.Add(T("compendium.tab.combat"), "", "fight").Name = "Combat";
			sigils.Add(T("compendium.tab.stats"), "", "stats").Name = "Stats";
			sigils.Add(T("compendium.tab.glyphs"), "", "rune").Name = "Glyphs";
			sigils.Add(T("compendium.tab.effects"), "", "effects").Name = "Effects";
			sigils.Add(T("compendium.tab.runes"), "", "grindstone").Name = "Runes";
			sigils.Changed += index => tabs.CurrentTab = index;
		}

		private static void Basics(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, "Sanctuary", "collect", T("compendium.basic.sanctuary.title"), T("compendium.basic.sanctuary.text", Idle.CapHours));
			Card(grid, "Campaign", "campaign", T("compendium.basic.campaign.title"), T("compendium.basic.campaign.text", GameDatabase.MaxCampaignRuneGrade));
			Card(grid, "Dungeons", "dungeon", T("compendium.basic.dungeons.title"), T("compendium.basic.dungeons.text"));
			Card(grid, "Exploration", "star_exploration", T("compendium.basic.exploration.title"), T("compendium.basic.exploration.text", Exploration.Rotation));
			Card(grid, "Mana", "mana", T("compendium.basic.mana.title"), T("compendium.basic.mana.text", Mana.BaseMax, Mana.BaseMax + Mana.MaxFromLevels, Mana.PerHour, Account.MaxLevel));
			Card(grid, "Gold", "gold", T("compendium.basic.gold.title"), T("compendium.basic.gold.text", Account.LevelUpGold));
			Card(grid, "Summon", "summon", T("compendium.basic.summon.title"), T("compendium.basic.summon.text"));
			Card(grid, "Monsters", "monster", T("compendium.basic.monsters.title"), T("compendium.basic.monsters.text", PlayerState.StartingCollectionCapacity, RuneInventory.Capacity, Account.MaxCollectionCapacity));
			Card(grid, "Teams", "team", T("compendium.basic.teams.title"), T("compendium.basic.teams.text", PlayerState.TeamSize));
			Card(grid, "Level", "essence", T("compendium.basic.level.title"), T("compendium.basic.level.text", Growth.MaxLevel(3), Growth.MaxLevel(Growth.MaxStars), Leveling.ExperiencePerEssence));
			var f3 = Evolution.Cost(3);
			var f4 = Evolution.Cost(4);
			var f5 = Evolution.Cost(5);
			Card(grid, "Stars", "summon", T("compendium.basic.stars.title"), T("compendium.basic.stars.text", Growth.MaxStars, f3, f4, f5));
			Card(grid, "Skills", "fragments", T("compendium.basic.skills.title"), T("compendium.basic.skills.text"));
			Card(grid, "Awaken", "grimoire", T("compendium.basic.awaken.title"), T("compendium.basic.awaken.text",
				Awakening.Cost(3), Awakening.Cost(4), Awakening.Cost(5),
				Texts.AwakeningBonus(Stat.Speed), Texts.AwakeningBonus(Stat.Crit), Texts.AwakeningBonus(Stat.Resistance), Texts.AwakeningBonus(Stat.Accuracy)));
			MonsterRarity(column);
		}

		/// <summary>
		/// A legenda das cores de raridade dos monstros, com cartões de verdade: a moldura de cada estrela
		/// natural (apagada, bronze, prata, ouro) e, por último, as estrelas roxas do desperto.
		/// </summary>
		private static void MonsterRarity(VBoxContainer column)
		{
			column.AddChild(new Label { Name = "RarityTitle", Text = T("compendium.rarity.title"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(RichText.Label(T("compendium.rarity.text"), 1150, GameTheme.Faded).Named("RarityText"));
			var row = Layout.Flow(24).Named("Rarities");
			if (UiSession.Database is { } database)
			{
				for (var stars = 2; stars <= 5; stars++)
				{
					if (database.Summons.FirstOrDefault(s => s.Rarity == stars) is { } summon)
						row.AddChild(Swatch($"Rarity{stars}", new CreatureCard(summon, null, 84), Texts.Stars(stars), T($"compendium.rarity.frame{stars}"), Palette.Frame(stars)));
				}

				if (database.Summons.FirstOrDefault(s => s.Rarity == 5) is { } awakened)
					row.AddChild(Swatch("Awakened", new CreatureCard(awakened, null, 84, awakenedPreview: true), Texts.Stars(5), T("compendium.rarity.awakened"), Palette.Awakened));
			}

			column.AddChild(row);
		}

		/// <summary>Um exemplo da legenda: o desenho, as estrelas e o que a cor quer dizer, na cor dela.</summary>
		private static VBoxContainer Swatch(string name, Control sample, string stars, string meaning, Color color)
		{
			var swatch = new VBoxContainer { Name = name, Alignment = BoxContainer.AlignmentMode.Center };
			swatch.AddThemeConstantOverride("separation", Space.Tight);
			sample.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			swatch.AddChild(sample.Named("Sample"));
			var top = new Label { Name = "Stars", Text = stars, HorizontalAlignment = HorizontalAlignment.Center };
			top.AddThemeColorOverride("font_color", color);
			swatch.AddChild(top);
			var text = new Label { Name = "Meaning", Text = meaning, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(150, 0) };
			text.AddThemeColorOverride("font_color", color);
			swatch.AddChild(text);
			return swatch;
		}

		private static void Combat(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, "Impetus", RuneSets.For(RuneSet.Bane).Glyph, T("compendium.combat.impetus.title"), T("compendium.combat.impetus.text", Texts.Impeto));
			Card(grid, "Fight", "campaign", T("compendium.combat.fight.title"), T("compendium.combat.fight.text", PlayerState.TeamSize, GameDatabase.MaxWaves, GameDatabase.MaxEnemiesPerWave, BattleRules.RoundLimit));
			Card(grid, "Damage", Texts.GlyphOf(Stat.Defense), T("compendium.combat.damage.title"), T("compendium.combat.damage.text", Math.Round(BattleRules.DefenseConstant)));
			Card(grid, "Crit", Texts.GlyphOf(Stat.Crit), T("compendium.combat.crit.title"), T("compendium.combat.crit.text"));
			Card(grid, "Resistance", Texts.GlyphOf(Stat.Resistance), T("compendium.combat.resistance.title"), T("compendium.combat.resistance.text", Texts.Percent(BattleRules.MinResistChance)));
			Card(grid, "Leader", "team", T("compendium.combat.leader.title"), T("compendium.combat.leader.text"));
			Card(grid, "Auto", "search", T("compendium.combat.auto.title"), T("compendium.combat.auto.text", AutoBattle.RepeatRuns));

			column.AddChild(new Label { Name = "ElementsTitle", Text = T("compendium.combat.elements"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(Layout.Text(T("compendium.combat.elements_text", Texts.Percent(BattleRules.AdvantageMultiplier - 1), Texts.Percent(1 - BattleRules.DisadvantageMultiplier)), GameTheme.Faded).Named("ElementsText"));
			var elements = new HFlowContainer { Name = "Elements" };
			elements.AddThemeConstantOverride("h_separation", 24);
			foreach (var element in Enum.GetValues<Element>())
			{
				var beats = Enum.GetValues<Element>().Where(other => ElementChart.HasAdvantage(element, other)).Select(Texts.Name);
				var row = new HBoxContainer { Name = element.ToString() };
				row.AddChild(Doodle.Icon(Art.Element(element), 32, Palette.Of(element)).Named("Icon"));
				var name = new Label { Name = "Wins", Text = T("compendium.combat.wins", Texts.Name(element), string.Join(T("common.and"), beats)) };
				name.AddThemeColorOverride("font_color", Palette.Of(element));
				row.AddChild(name);
				elements.AddChild(row);
			}

			column.AddChild(elements);
		}

		private static void Stats(VBoxContainer column)
		{
			column.AddChild(Layout.Text(T("compendium.stats.intro"), GameTheme.Faded).Named("Intro"));
			var grid = Cards(column);
            Stat[] ordemDesejada = [Stat.Health, Stat.Crit, Stat.Attack, Stat.CritDamage, Stat.Defense, Stat.Resistance, Stat.Speed, Stat.Accuracy];

            foreach (var stat in ordemDesejada)
            {
                Card(grid, stat.ToString(), Texts.GlyphOf(stat), Texts.Name(stat), Texts.Explain(stat));
            };
		}

		private static void Glyphs(VBoxContainer column)
		{
			column.AddChild(RichText.Label(T("compendium.glyphs.intro"), 1150, GameTheme.Faded).Named("Intro"));
			var grid = new GridContainer { Name = "Glyphs", Columns = 4 };
			grid.AddThemeConstantOverride("h_separation", Space.Regular);
			grid.AddThemeConstantOverride("v_separation", Space.Regular);
			foreach (var set in RuneSets.All)
			{
				var panel = new PanelContainer { Name = set.Glyph.ToString(), ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(284, 0) };
				var row = new HBoxContainer { Name = "Row" };
				row.AddThemeConstantOverride("separation", Space.Regular);
				row.AddChild(new RuneGlyph(set.Glyph, 56, Palette.Gold) { Name = "Glyph" });
				var text = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(new Label { Name = "Title", Text = Texts.Name(set.Glyph), ThemeTypeVariation = GameTheme.Heading });
				var meaning = new Label { Name = "Meaning", Text = Texts.Meaning(set.Glyph) };
				meaning.AddThemeColorOverride("font_color", Palette.Gold);
				text.AddChild(meaning);
				text.AddChild(new Label { Name = "Set", Text = T("compendium.glyphs.set", Texts.Name(set.Set)), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(text);
				panel.AddChild(row);
				grid.AddChild(panel);
			}

			column.AddChild(grid);
		}

		/// <summary>Os efeitos lado a lado: os positivos na primeira coluna, os negativos na segunda.</summary>
		private static void Statuses(VBoxContainer column)
		{
			column.AddChild(Layout.Text(T("compendium.effects.intro", Texts.Percent(BattleRules.MinResistChance)), GameTheme.Faded).Named("Intro"));
			var grid = Cards(column);
			var statuses = Enum.GetValues<StatusKind>();
			var positives = statuses.Where(s => !BattleRules.IsNegative(s)).ToList();
			var negatives = statuses.Where(BattleRules.IsNegative).ToList();
			for (var i = 0; i < Math.Max(positives.Count, negatives.Count); i++)
			{
				StatusCard(grid, i < positives.Count ? positives[i] : null, false, i);
				StatusCard(grid, i < negatives.Count ? negatives[i] : null, true, i);
			}
		}

		/// <summary>Um efeito, ou uma vaga vazia quando a coluna dele já acabou (a grade segue alinhada).</summary>
		private static void StatusCard(GridContainer grid, StatusKind? status, bool negative, int index)
		{
			if (status is not { } kind)
			{
				grid.AddChild(new Control { Name = $"Empty{(negative ? "Negative" : "Positive")}{index}" });
				return;
			}

			var tag = negative ? T("compendium.effects.negative") : T("compendium.effects.positive");
			var panel = new PanelContainer { Name = kind.ToString(), ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(CardWidth, 0) };
			var row = new HBoxContainer { Name = "Row" };
			row.AddThemeConstantOverride("separation", Space.Regular);
			row.AddChild(Doodle.Icon(Art.Effect(kind), 44, negative ? Palette.Negative : Palette.Positive).Named("Icon"));
			row.AddChild(RichText.Label($"{Texts.Term(kind)}  [color=#{Palette.TextFaded.ToHtml(false)}]{tag}[/color]\n{Texts.Explain(kind)}", CardWidth - 80).Named("Text"));
			panel.AddChild(row);
			grid.AddChild(panel);
		}

		private static void Runes(VBoxContainer column)
		{
			var grid = Cards(column);
			Card(grid, "Slots", "rune", T("compendium.runes.slots.title"), T("compendium.runes.slots.text"));
			Card(grid, "Stars", "rune", T("compendium.runes.stars.title"), T("compendium.runes.stars.text", RuneRules.MaxGrade));
			Card(grid, "Upgrade", "essence", T("compendium.runes.upgrade.title"), T("compendium.runes.upgrade.text", RuneRules.MaxLevel, RuneRules.MaxSubstats));
			Card(grid, "Sets", RuneSets.For(RuneSet.Frenzy).Glyph, T("compendium.runes.sets.title"), T("compendium.runes.sets.text"));
			Card(grid, "Grind", "grindstone", T("compendium.runes.grind.title"), T("compendium.runes.grind.text"));
			Card(grid, "Gem", "gem", T("compendium.runes.gem.title"), T("compendium.runes.gem.text", RuneForge.EnchantLevel));
			Card(grid, "Where", "dungeon", T("compendium.runes.where.title"), T("compendium.runes.where.text", GameDatabase.MaxCampaignRuneGrade, Texts.Percent(Campaign.RepeatRuneChance)));
			Card(grid, "Grimoire", "grimoire", T("compendium.runes.grimoire.title"), T("compendium.runes.grimoire.text"));

			column.AddChild(new Label { Name = "RarityTitle", Text = T("compendium.runes.rarity.title"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(RichText.Label(T("compendium.runes.rarity.text"), 1150, GameTheme.Faded).Named("RarityText"));
			var row = Layout.Flow(24).Named("Rarities");
			foreach (var rarity in Enum.GetValues<RuneRarity>())
			{
				var stone = new PanelContainer { Name = "Stone", CustomMinimumSize = RuneTile.TileSize };
				stone.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Of(rarity), 2, 9, 0));
				var glyph = new RuneGlyph(RuneSets.For(RuneSet.Vigor).Glyph, 40, Palette.Of(rarity), outline: true) { Name = "Glyph" };
				stone.AddChild(glyph);
				row.AddChild(Swatch(rarity.ToString(), stone, Texts.Name(rarity), T("compendium.runes.rarity.subs", (int)rarity), Palette.Of(rarity)));
			}

			column.AddChild(row);
		}

		private static GridContainer Cards(VBoxContainer column)
		{
			var grid = new GridContainer { Name = "Cards", Columns = 2 };
			grid.AddThemeConstantOverride("h_separation", Space.Large);
			grid.AddThemeConstantOverride("v_separation", Space.Large);
			column.AddChild(grid);
			return grid;
		}

		private static void Card(GridContainer grid, string name, string icon, string title, string text) => Card(grid, name, Doodle.Icon(Art.Icon(icon), 44, Palette.Gold), title, text);

		private static void Card(GridContainer grid, string name, Glyph glyph, string title, string text) => Card(grid, name, new RuneGlyph(glyph, 44, Palette.Gold), title, text);

		/// <summary>Um tópico: símbolo à esquerda, título e texto rico.</summary>
		private static void Card(GridContainer grid, string name, Control icon, string title, string text)
		{
			var panel = new PanelContainer { Name = name, ThemeTypeVariation = GameTheme.InsetPanel, CustomMinimumSize = new Vector2(CardWidth, 0) };
			var row = new HBoxContainer { Name = "Row" };
			row.AddThemeConstantOverride("separation", Space.Large);
			row.AddChild(icon.Named("Icon"));
			var content = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			content.AddChild(new Label { Name = "Title", Text = title, ThemeTypeVariation = GameTheme.Heading });
			content.AddChild(RichText.Label(text, CardWidth - 76).Named("Description"));
			row.AddChild(content);
			panel.AddChild(row);
			grid.AddChild(panel);
		}
	}
}
