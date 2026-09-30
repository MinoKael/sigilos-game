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
		private readonly VBoxContainer _sheet = new() { Name = "Content" };
		private readonly GridContainer _gallery = new() { Name = "Families", Columns = 4 };

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

			var tabs = new TabContainer { Name = "Pages", SizeFlagsVertical = SizeFlags.ExpandFill, TabsVisible = false };
			page.AddChild(tabs);
			Summons(tabs);
			Sets(Layout.Tab(tabs, "Sets", T("grimoire.tab.sets")));
			RuneTables(Layout.Tab(tabs, "RuneTables", T("grimoire.tab.runes")));
			Tools(Layout.Tab(tabs, "Grindstones", T("grimoire.tab.grindstones")), RuneToolKind.Grindstone);
			Tools(Layout.Tab(tabs, "Gems", T("grimoire.tab.gems")), RuneToolKind.Gem);

			var sigils = new SigilTabs(vertical: false, 48) { Name = "Tabs" };
			sigils.Add(Art.Icon("summon"), T("grimoire.tab.summons")).Name = "Summons";
			sigils.Add(Art.Icon("rune"), T("grimoire.tab.sets")).Name = "Sets";
			sigils.Add(Art.Icon("grimoire"), T("grimoire.tab.runes")).Name = "RuneTables";
			sigils.Add(Art.Icon("grindstone"), T("grimoire.tab.grindstones")).Name = "Grindstones";
			sigils.Add(Art.Icon("gem"), T("grimoire.tab.gems")).Name = "Gems";
			sigils.Changed += index => tabs.CurrentTab = index;
			extra.AddChild(sigils);
		}

		// Invocações --------------------------------------------------------------------------------

		private void Summons(TabContainer tabs)
		{
			var body = Layout.Row(14).Named("Summons");
			tabs.AddChild(body);
			tabs.SetTabTitle(tabs.GetTabCount() - 1, T("grimoire.tab.summons"));

			_gallery.AddThemeConstantOverride("h_separation", 10);
			_gallery.AddThemeConstantOverride("v_separation", 10);
			var gallery = Layout.Scroll(_gallery).Named("Gallery");
			gallery.CustomMinimumSize = new Vector2(540, 0);
			gallery.SizeFlagsHorizontal = SizeFlags.Fill;
			body.AddChild(gallery);

			_sheet.AddThemeConstantOverride("separation", 10);
			body.AddChild(Layout.Scroll(_sheet).Named("Sheet"));

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
				Name = Layout.NodeName(family.Id),
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

			var column = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore };
			var stars = new Label { Name = "Stars", Text = Texts.Stars(family.Rarity), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
			stars.AddThemeColorOverride("font_color", Palette.Stars(_awakened));
			column.AddChild(stars);
			var art = new Control { Name = "Art", CustomMinimumSize = new Vector2(0, 80), SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			art.AddChild(Doodle.Masked(Art.Creature(_awakened ? family.AwakenedImage : family.Image), Palette.Gold, MaskShape.Rounded, 6));
			column.AddChild(art);
			var count = new Label { Name = "Copies", Text = copies.ToString(), ThemeTypeVariation = GameTheme.Number, HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = MouseFilterEnum.Ignore };
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
			var row = Layout.Row(8).Named("Elements");
			var group = new ButtonGroup();
			foreach (var variant in Variants(current.FamilyId))
			{
				var copies = _player.Monsters.Count(m => m.SummonId == variant.Id);
				var chosen = variant == current;
				var button = new SigilButton(Art.Element(variant.Element), $"{variant.NameFor(_awakened)} · {(copies == 0 ? T("grimoire.not_owned") : T("grimoire.copies", copies))}", 50)
				{
					Name = variant.Element.ToString(),
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

			row.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var awaken = new SigilButton(Art.Icon("awaken"), T("grimoire.view_awakened", current.Awakening.Name), 50, SigilShape.Diamond) { Name = "Awakened", ToggleMode = true, ButtonPressed = _awakened, Ink = Palette.Awakened };
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
			_sheet.AddChild(new Label { Name = "Family", Text = family.Name, ThemeTypeVariation = GameTheme.Title });
			_sheet.AddChild(ElementPicker(summon));

			var identity = Layout.Row(12).Named("Identity");
			var portrait = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(96, 96) };
			portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 10, 6));
			portrait.AddChild(Doodle.Masked(Art.Creature(summon.ImageFor(_awakened)), Palette.Of(summon.Element), MaskShape.Rounded, 6));
			identity.AddChild(portrait);
			var info = new VBoxContainer { Name = "Info" };
			var name = new Label { Name = "Name", Text = summon.NameFor(_awakened), ThemeTypeVariation = GameTheme.Heading };
			if (_awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			info.AddChild(name);
			var line = Layout.Row(8).Named("Line");
			var stars = new Label { Name = "Stars", Text = Texts.Stars(summon.Rarity) };
			stars.AddThemeColorOverride("font_color", Palette.Stars(_awakened));
			line.AddChild(stars);
			line.AddChild(Doodle.Icon(Art.Element(summon.Element), 20, Palette.Of(summon.Element)).Named("Element"));
			line.AddChild(new Label { Name = "Role", Text = Texts.Name(summon.Role), ThemeTypeVariation = GameTheme.Faded });
			info.AddChild(line);
			identity.AddChild(info);
			_sheet.AddChild(identity);

			var table = new GridContainer { Name = "Stats", Columns = 4 };
			table.AddThemeConstantOverride("h_separation", 22);
			table.AddThemeConstantOverride("v_separation", 2);
			table.AddChild(new Control { Name = Layout.NextCell(table) });
			Cell(table, "", true);
			Cell(table, T("common.stars_level", Texts.Stars(summon.Rarity), 1), true);
			Cell(table, T("common.stars_level", Texts.Stars(Growth.MaxStars), Growth.MaxLevel(Growth.MaxStars)), true);
			var low = SummonStats.For(summon, summon.Rarity, 1, _awakened, Array.Empty<Rune>()).Base;
			var high = SummonStats.For(summon, Growth.MaxStars, Growth.MaxLevel(Growth.MaxStars), _awakened, Array.Empty<Rune>()).Base;
			foreach (var stat in Enum.GetValues<Stat>())
			{
				table.AddChild(new RuneGlyph(Texts.GlyphOf(stat), 18, Palette.Gold) { Name = Layout.NextCell(table) });
				Cell(table, Texts.Name(stat));
				Cell(table, Texts.Value(stat, low.Get(stat)));
				Cell(table, Texts.Value(stat, high.Get(stat)));
			}

			_sheet.AddChild(table);
			_sheet.AddChild(new HSeparator { Name = "SkillsLine" });

			var skills = summon.SkillsFor(_awakened);
			for (var i = 0; i < summon.AllSkills.Count; i++)
				_sheet.AddChild(SkillRow.Build(summon.AllSkills[i], 1, _awakened, i >= skills.Count, 520).Named($"Skill{i + 1}"));

			if (summon.Leader is { } leader)
			{
				var row = Layout.Row(10).Named("Leader");
				row.AddChild(Doodle.Icon(Art.Icon("leader"), 32, Palette.Gold).Named("Icon"));
				row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), 480).Named("Text"));
				_sheet.AddChild(row);
			}

			_sheet.AddChild(new HSeparator { Name = "AwakeningLine" });
			var awaken = Layout.Flow(8).Named("Awakening");
			awaken.AddChild(Layout.Chip("awaken", summon.Awakening.Name, T("grimoire.awaken"), Palette.Awakened).Named("Name"));
			foreach (var (stat, gain) in Texts.AwakeningStats(summon))
				awaken.AddChild(Layout.Chip(Texts.GlyphOf(stat), gain, Texts.Name(stat)).Named(stat.ToString()));
			awaken.AddChild(Layout.Chip("essence", Texts.Short(Awakening.Cost(summon.Rarity)), T("grimoire.awaken_cost", Awakening.Cost(summon.Rarity))).Named("Cost"));
			_sheet.AddChild(awaken);
		}

		// Conjuntos ---------------------------------------------------------------------------------

		private void Sets(VBoxContainer column)
		{
			foreach (var set in RuneSets.All)
			{
				var panel = new PanelContainer { Name = set.Set.ToString(), ThemeTypeVariation = GameTheme.InsetPanel };
				var row = Layout.Row(12).Named("Row");
				var glyph = new RuneGlyph(set.Glyph, 44, Palette.Gold) { Name = "Glyph" };
				glyph.TooltipText = T("grimoire.glyph_of", Texts.Name(set.Glyph), Texts.Meaning(set.Glyph));
				glyph.MouseFilter = MouseFilterEnum.Stop;
				row.AddChild(glyph);
				var text = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(RichText.Label(Texts.Term(set.Set)).Named("Title"));
				text.AddChild(RichText.Label(Texts.Describe(set), 0, GameTheme.Faded).Named("Effect"));
				row.AddChild(text);

				var where = _database.Dungeons.Where(d => d.Sets.Contains(set.Set)).ToList();
				if (where.Count == 0)
				{
					row.AddChild(Layout.Chip("region", "", T("grimoire.campaign_only")).Named("CampaignOnly"));
				}
				else
				{
					foreach (var dungeon in where)
						row.AddChild(Layout.Chip(Art.Creature(dungeon.Image), "", dungeon.Name).Named(Layout.NodeName(dungeon.Id)));
				}

				panel.AddChild(row);
				column.AddChild(panel);
			}
		}

		// Tabelas das runas -------------------------------------------------------------------------

		private static void RuneTables(VBoxContainer column)
		{
			var grades = Enumerable.Range(1, RuneRules.MaxGrade).ToList();

			column.AddChild(Heading(T("grimoire.main"), T("grimoire.main_intro")).Named("MainTitle"));
			var mains = Enum.GetValues<RuneStat>();
			var main = Table(column, "Main", grades.Count + 1, new[] { "" }.Concat(grades.Select(Texts.Stars)));
			foreach (var stat in mains)
			{
				Cell(main, Texts.Label(stat));
				foreach (var grade in grades)
					Cell(main, $"{Short(stat, RuneRules.MainValue(stat, grade, 0))} · {Short(stat, RuneRules.MainValue(stat, grade, 12))} · {Short(stat, RuneRules.MainValue(stat, grade, RuneRules.MaxLevel))}");
			}

			column.AddChild(Heading(T("grimoire.sub"), T("grimoire.sub_intro")).Named("SubTitle"));
			var sub = Table(column, "Sub", grades.Count + 1, new[] { "" }.Concat(grades.Select(Texts.Stars)));
			foreach (var stat in mains)
			{
				Cell(sub, Texts.Label(stat));
				foreach (var grade in grades)
				{
					var (min, max) = RuneRules.SubstatRange(stat, grade);
					Cell(sub, $"{Short(stat, min)}–{Short(stat, max)}");
				}
			}

			column.AddChild(Heading(T("grimoire.cost"), T("grimoire.cost_intro")).Named("CostTitle"));
			var cost = Table(column, "Cost", grades.Count + 1, new[] { "" }.Concat(grades.Select(Texts.Stars)));
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
			column.AddChild(Heading(title, T(kind == RuneToolKind.Grindstone ? "grimoire.grindstones_intro" : "grimoire.gems_intro", RuneForge.EnchantLevel)).Named("Title"));
			var table = Table(column, "Ranges", grades.Length + 1, new[] { "" }.Concat(grades.Select(Texts.Name)));
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

		private static GridContainer Table(VBoxContainer column, string name, int columns, IEnumerable<string> header)
		{
			var panel = new PanelContainer { Name = name, ThemeTypeVariation = GameTheme.InsetPanel };
			var table = new GridContainer { Name = "Table", Columns = columns };
			table.AddThemeConstantOverride("h_separation", 18);
			table.AddThemeConstantOverride("v_separation", 4);
			foreach (var title in header)
				Cell(table, title, true);
			panel.AddChild(table);
			column.AddChild(panel);
			return table;
		}

		/// <summary>Uma célula de texto; o nó leva a linha e a coluna (<c>R2C3</c>).</summary>
		private static void Cell(GridContainer table, string text, bool header = false)
		{
			var label = new Label { Name = Layout.NextCell(table), Text = text };
			if (header)
				label.AddThemeColorOverride("font_color", Palette.Gold);
			table.AddChild(label);
		}

		/// <summary>Número sem sinal: "11%" ou "360".</summary>
		private static string Short(RuneStat stat, double value) => Texts.Amount(stat, value).TrimStart('+');
	}
}
