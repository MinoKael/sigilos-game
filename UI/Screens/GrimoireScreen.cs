using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
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
	/// O Grimório: o catálogo de tudo o que existe no jogo, com as abas em sigilos no cabeçalho.
	/// Invocações agrupadas por família (Fênix, Diabretes...), os elementos em sigilos no detalhe e a
	/// ficha nas estrelas naturais nível 1 e em 6★ nível 40, antes e depois do Despertar; conjuntos de
	/// runa e onde caem, as tabelas das runas por estrela e as faixas das Pedras de Afiar e das Gemas
	/// Encantadas. Os números vêm do Core e de Data/; o porquê de cada regra fica no Compêndio.
	/// </summary>
	public partial class GrimoireScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private SummonDefinition _selected;
		private bool _awakened;
		private readonly VBoxContainer _sheet = new();
		private readonly GridContainer _gallery = new() { Columns = 4 };

		public GrimoireScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
			_selected = database.Summons.OrderByDescending(s => s.Rarity).First();
		}

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			var (header, extra) = Layout.Header(T("destination.Grimoire"), "grimoire", null, () => BackRequested?.Invoke());
			page.AddChild(header);

			var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill, TabsVisible = false };
			page.AddChild(tabs);
			Summons(tabs);
			Sets(Layout.Tab(tabs, T("grimoire.tab.sets")));
			RuneTables(Layout.Tab(tabs, T("grimoire.tab.runes")));
			Tools(Layout.Tab(tabs, T("grimoire.tab.grindstones")), RuneToolKind.Grindstone);
			Tools(Layout.Tab(tabs, T("grimoire.tab.gems")), RuneToolKind.Gem);

			var sigils = new SigilTabs(vertical: false, 48);
			sigils.Add(Art.Icon("summon"), T("grimoire.tab.summons"));
			sigils.Add(Art.Icon("rune"), T("grimoire.tab.sets"));
			sigils.Add(Art.Icon("grimoire"), T("grimoire.tab.runes"));
			sigils.Add(Art.Icon("grindstone"), T("grimoire.tab.grindstones"));
			sigils.Add(Art.Icon("gem"), T("grimoire.tab.gems"));
			sigils.Changed += index => tabs.CurrentTab = index;
			extra.AddChild(sigils);
		}

		// Invocações --------------------------------------------------------------------------------

		private void Summons(TabContainer tabs)
		{
			var body = Layout.Row(14);
			tabs.AddChild(body);
			tabs.SetTabTitle(tabs.GetTabCount() - 1, T("grimoire.tab.summons"));

			_gallery.AddThemeConstantOverride("h_separation", 10);
			_gallery.AddThemeConstantOverride("v_separation", 10);
			var gallery = Layout.Scroll(_gallery);
			gallery.CustomMinimumSize = new Vector2(540, 0);
			gallery.SizeFlagsHorizontal = SizeFlags.Fill;
			body.AddChild(gallery);

			_sheet.AddThemeConstantOverride("separation", 10);
			body.AddChild(Layout.Scroll(_sheet));

			RefreshSummons();
		}

		private void RefreshSummons()
		{
			Layout.Clear(_gallery);
			foreach (var family in _database.Families.OrderByDescending(f => f.Rarity).ThenBy(f => f.Name))
				_gallery.AddChild(FamilyCard(family));

			RefreshSheet();
		}

		/// <summary>Uma família: estrelas naturais, desenho e, na plaquinha, quantas cópias o jogador tem somando os elementos.</summary>
		private Control FamilyCard(FamilyDefinition family)
		{
			var copies = _player.Monsters.Count(m => _database.HasSummon(m.SummonId) && _database.Summon(m.SummonId).FamilyId == family.Id);
			var selected = _selected.FamilyId == family.Id;
			var card = new PanelContainer
			{
				CustomMinimumSize = new Vector2(120, 140),
				MouseFilter = MouseFilterEnum.Stop,
				MouseDefaultCursorShape = CursorShape.PointingHand,
				TooltipText = $"{family.Name} · {(copies == 0 ? T("grimoire.not_owned") : T("grimoire.copies", copies))}",
			};
			var box = GameTheme.Box(Palette.Inset, selected ? Palette.Arcane : Palette.Frame(family.Rarity), selected ? 4 : 3, 10, 6);
			if (selected)
			{
				box.ShadowColor = new Color(Palette.Arcane, 0.4f);
				box.ShadowSize = 7;
			}

			card.AddThemeStyleboxOverride("panel", box);
			card.Modulate = copies == 0 ? new Color(1, 1, 1, 0.55f) : Colors.White;

			var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			var stars = new Label { Text = Texts.Stars(family.Rarity), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
			stars.AddThemeColorOverride("font_color", Palette.Stars(_awakened));
			column.AddChild(stars);
			var art = new Control { CustomMinimumSize = new Vector2(0, 80), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			art.AddChild(Doodle.Masked(Art.Creature(_awakened ? family.AwakenedImage : family.Image), Palette.Gold, MaskShape.Rounded, 6));
			column.AddChild(art);
			var count = new Label { Text = copies.ToString(), ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = MouseFilterEnum.Ignore };
			count.AddThemeFontSizeOverride("font_size", 14);
			column.AddChild(count);
			card.AddChild(column);

			card.GuiInput += input =>
			{
				if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || selected)
					return;

				// Troca de família mantendo o elemento, quando a nova tem.
				_selected = Variants(family.Id).FirstOrDefault(s => s.Element == _selected.Element) ?? Variants(family.Id).First();
				Callable.From(RefreshSummons).CallDeferred();
			};
			return card;
		}

		private IEnumerable<SummonDefinition> Variants(string familyId) =>
			_database.Summons.Where(s => s.FamilyId == familyId).OrderBy(s => s.Element);

		/// <summary>Um sigilo por elemento da família (o aceso é o da ficha), e o do Despertar, que troca a ficha para a forma desperta.</summary>
		private Control ElementPicker(SummonDefinition current)
		{
			var row = Layout.Row(8);
			var group = new ButtonGroup();
			foreach (var variant in Variants(current.FamilyId))
			{
				var copies = _player.Monsters.Count(m => m.SummonId == variant.Id);
				var chosen = variant == current;
				var button = new SigilButton(Art.Element(variant.Element), $"{variant.NameFor(_awakened)} · {(copies == 0 ? T("grimoire.not_owned") : T("grimoire.copies", copies))}", 50)
				{
					ToggleMode = true,
					ButtonGroup = group,
					ButtonPressed = chosen,
					Ink = Palette.Of(variant.Element),
					Badge = copies > 0 ? copies.ToString() : "",
				};
				button.Pressed += () =>
				{
					_selected = variant;
					Callable.From(RefreshSummons).CallDeferred();
				};
				row.AddChild(button);
			}

			row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var awaken = new SigilButton(Art.Icon("awaken"), T("grimoire.view_awakened", current.Awakening.Name), 50, SigilShape.Diamond) { ToggleMode = true, ButtonPressed = _awakened, Ink = Palette.Awakened };
			awaken.Toggled += on =>
			{
				_awakened = on;
				Callable.From(RefreshSummons).CallDeferred();
			};
			row.AddChild(awaken);
			return row;
		}

		private void RefreshSheet()
		{
			Layout.Clear(_sheet);
			var summon = _selected;
			var family = summon.Family;
			_sheet.AddChild(new Label { Text = family.Name, ThemeTypeVariation = GameTheme.Title });
			_sheet.AddChild(ElementPicker(summon));

			var identity = Layout.Row(12);
			var portrait = new PanelContainer { CustomMinimumSize = new Vector2(96, 96) };
			portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 10, 6));
			portrait.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(_awakened)), Palette.Of(summon.Element), MaskShape.Rounded, 6));
			identity.AddChild(portrait);
			var info = new VBoxContainer();
			var name = new Label { Text = summon.NameFor(_awakened), ThemeTypeVariation = GameTheme.Heading };
			if (_awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			info.AddChild(name);
			var line = Layout.Row(8);
			var stars = new Label { Text = Texts.Stars(summon.Rarity) };
			stars.AddThemeColorOverride("font_color", Palette.Stars(_awakened));
			line.AddChild(stars);
			line.AddChild(Doodle.Icon(Art.Element(summon.Element), 20, Palette.Of(summon.Element)));
			line.AddChild(new Label { Text = Texts.Name(summon.Role), ThemeTypeVariation = GameTheme.Faded });
			info.AddChild(line);
			identity.AddChild(info);
			_sheet.AddChild(identity);

			var table = new GridContainer { Columns = 4 };
			table.AddThemeConstantOverride("h_separation", 22);
			table.AddThemeConstantOverride("v_separation", 2);
			table.AddChild(new Control());
			Cell(table, "", true);
			Cell(table, T("common.stars_level", Texts.Stars(summon.Rarity), 1), true);
			Cell(table, T("common.stars_level", Texts.Stars(Growth.MaxStars), Growth.MaxLevel(Growth.MaxStars)), true);
			var roleBase = _database.Roles[summon.Role];
			var low = SummonStats.For(roleBase, summon, summon.Rarity, 1, _awakened, Array.Empty<Rune>()).Base;
			var high = SummonStats.For(roleBase, summon, Growth.MaxStars, Growth.MaxLevel(Growth.MaxStars), _awakened, Array.Empty<Rune>()).Base;
			foreach (var stat in Enum.GetValues<Stat>())
			{
				table.AddChild(new RuneGlyph(Texts.GlyphOf(stat), 18, Palette.Gold));
				Cell(table, Texts.Name(stat));
				Cell(table, Texts.Value(stat, low.Get(stat)));
				Cell(table, Texts.Value(stat, high.Get(stat)));
			}

			_sheet.AddChild(table);
			_sheet.AddChild(new HSeparator());

			var skills = summon.SkillsFor(_awakened);
			for (var i = 0; i < summon.AllSkills.Count; i++)
				_sheet.AddChild(SkillRow.Build(summon.AllSkills[i], 1, _awakened, i >= skills.Count, 520));

			if (summon.Leader is { } leader)
			{
				var row = Layout.Row(10);
				row.AddChild(Doodle.Icon(Art.Icon("leader"), 32, Palette.Gold));
				row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), 480));
				_sheet.AddChild(row);
			}

			_sheet.AddChild(new HSeparator());
			var awaken = Layout.Flow(8);
			awaken.AddChild(Layout.Chip("awaken", summon.Awakening.Name, T("grimoire.awaken"), Palette.Awakened));
			awaken.AddChild(Layout.Chip(Texts.GlyphOf(Stat.Health), $"+{Texts.Percent(Awakening.HealthBonus)}", Texts.Name(Stat.Health)));
			awaken.AddChild(Layout.Chip(Texts.GlyphOf(Stat.Attack), $"+{Texts.Percent(Awakening.AttackDefenseBonus)}", Texts.Name(Stat.Attack)));
			awaken.AddChild(Layout.Chip(Texts.GlyphOf(Stat.Defense), $"+{Texts.Percent(Awakening.AttackDefenseBonus)}", Texts.Name(Stat.Defense)));
			if (summon.Awakening.Stat is { } bonus)
				awaken.AddChild(Layout.Chip(Texts.GlyphOf(bonus), Texts.AwakeningAmount(bonus), Texts.Name(bonus)));
			awaken.AddChild(Layout.Chip("essence", Texts.Short(Awakening.Cost(summon.Rarity)), T("grimoire.awaken_cost", Awakening.Cost(summon.Rarity))));
			_sheet.AddChild(awaken);
		}

		// Conjuntos ---------------------------------------------------------------------------------

		private void Sets(VBoxContainer column)
		{
			foreach (var set in RuneSets.All)
			{
				var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
				var row = Layout.Row(12);
				var glyph = new RuneGlyph(set.Glyph, 44, Palette.Gold);
				glyph.TooltipText = T("grimoire.glyph_of", Texts.Name(set.Glyph), Texts.Meaning(set.Glyph));
				glyph.MouseFilter = MouseFilterEnum.Stop;
				row.AddChild(glyph);
				var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(RichText.Label(Texts.Term(set.Set)));
				text.AddChild(RichText.Label(Texts.Describe(set), 0, GameTheme.Faded));
				row.AddChild(text);

				var where = _database.Dungeons.Where(d => d.Sets.Contains(set.Set)).ToList();
				if (where.Count == 0)
				{
					row.AddChild(Layout.Chip("region", "", T("grimoire.campaign_only")));
				}
				else
				{
					foreach (var dungeon in where)
						row.AddChild(Layout.Chip(Art.Creature(dungeon.Image), "", dungeon.Name));
				}

				panel.AddChild(row);
				column.AddChild(panel);
			}
		}

		// Tabelas das runas -------------------------------------------------------------------------

		private static void RuneTables(VBoxContainer column)
		{
			var grades = Enumerable.Range(1, RuneRules.MaxGrade).ToList();

			column.AddChild(Heading(T("grimoire.main"), T("grimoire.main_intro")));
			var mains = Enum.GetValues<RuneStat>();
			var main = Table(column, grades.Count + 1, new[] { "" }.Concat(grades.Select(Texts.Stars)));
			foreach (var stat in mains)
			{
				Cell(main, Texts.Label(stat));
				foreach (var grade in grades)
					Cell(main, $"{Short(stat, RuneRules.MainValue(stat, grade, 0))} · {Short(stat, RuneRules.MainValue(stat, grade, 12))} · {Short(stat, RuneRules.MainValue(stat, grade, RuneRules.MaxLevel))}");
			}

			column.AddChild(Heading(T("grimoire.sub"), T("grimoire.sub_intro")));
			var sub = Table(column, grades.Count + 1, new[] { "" }.Concat(grades.Select(Texts.Stars)));
			foreach (var stat in mains)
			{
				Cell(sub, Texts.Label(stat));
				foreach (var grade in grades)
				{
					var (min, max) = RuneRules.SubstatRange(stat, grade);
					Cell(sub, $"{Short(stat, min)}–{Short(stat, max)}");
				}
			}

			column.AddChild(Heading(T("grimoire.cost"), T("grimoire.cost_intro")));
			var cost = Table(column, grades.Count + 1, new[] { "" }.Concat(grades.Select(Texts.Stars)));
			foreach (var target in new[] { 3, 6, 9, 12, 15 })
			{
				Cell(cost, $"+{target}");
				foreach (var grade in grades)
					Cell(cost, RuneRules.UpgradeCost(new Rune { Grade = grade }, target).ToString());
			}

			Cell(cost, T("grimoire.sell"));
			foreach (var grade in grades)
				Cell(cost, T("grimoire.sell_value", RuneRules.SellValue(new Rune { Grade = grade }), RuneRules.SellValue(new Rune { Grade = grade, Substats = Enumerable.Range(0, 4).Select(_ => new RuneSubstat()).ToList() })));
		}

		private static void Tools(VBoxContainer column, RuneToolKind kind)
		{
			var grades = new[] { RuneRarity.Magic, RuneRarity.Rare, RuneRarity.Hero, RuneRarity.Legendary };
			var title = kind == RuneToolKind.Grindstone ? T("grimoire.tab.grindstones") : T("grimoire.tab.gems");
			column.AddChild(Heading(title, T(kind == RuneToolKind.Grindstone ? "grimoire.grindstones_intro" : "grimoire.gems_intro", RuneForge.EnchantLevel)));
			var table = Table(column, grades.Length + 1, new[] { "" }.Concat(grades.Select(Texts.Name)));
			foreach (var stat in Enum.GetValues<RuneStat>())
			{
				if (kind == RuneToolKind.Grindstone && !RuneRules.IsGrindable(stat))
					continue;
				Cell(table, Texts.Label(stat));
				foreach (var grade in grades)
					Cell(table, Texts.Range(new RuneTool(kind, stat, grade)));
			}
		}

		/// <summary>Título de tabela; o que as colunas querem dizer vem na dica.</summary>
		private static Label Heading(string text, string tooltip) =>
			new() { Text = text, ThemeTypeVariation = GameTheme.Heading, TooltipText = tooltip, MouseFilter = MouseFilterEnum.Stop };

		private static GridContainer Table(VBoxContainer column, int columns, IEnumerable<string> header)
		{
			var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var table = new GridContainer { Columns = columns };
			table.AddThemeConstantOverride("h_separation", 18);
			table.AddThemeConstantOverride("v_separation", 4);
			foreach (var title in header)
				Cell(table, title, true);
			panel.AddChild(table);
			column.AddChild(panel);
			return table;
		}

		private static void Cell(GridContainer table, string text, bool header = false)
		{
			var label = new Label { Text = text };
			if (header)
				label.AddThemeColorOverride("font_color", Palette.Gold);
			table.AddChild(label);
		}

		/// <summary>Número sem sinal: "11%" ou "360".</summary>
		private static string Short(RuneStat stat, double value) => Texts.Amount(stat, value).TrimStart('+');
	}
}
