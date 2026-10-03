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
	/// O Grimório: o catálogo de tudo o que existe no jogo, com as abas escritas no alto. Invocações
	/// agrupadas por família (Fênix, Diabretes...), os elementos em botões com o nome no detalhe e a
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
			page.AddChild(Layout.Header(T("destination.Grimoire"), null, () => BackRequested?.Invoke()).Header);
			var sigils = new TextTabs { Name = "Tabs" };
			page.AddChild(sigils);

			var tabs = new TabContainer { Name = "Pages", SizeFlagsVertical = SizeFlags.ExpandFill, TabsVisible = false };
			page.AddChild(tabs);
			Summons(tabs);
			Sets(Layout.Tab(tabs, "Sets", T("grimoire.tab.sets")));
			RuneTables(Layout.Tab(tabs, "RuneTables", T("grimoire.tab.runes")));
			Tools(Layout.Tab(tabs, "Grindstones", T("grimoire.tab.grindstones")), RuneToolKind.Grindstone);
			Tools(Layout.Tab(tabs, "Gems", T("grimoire.tab.gems")), RuneToolKind.Gem);

			sigils.Add(T("grimoire.tab.summons"), "", "summon").Name = "Summons";
			sigils.Add(T("grimoire.tab.sets"), "", "rune").Name = "Sets";
			sigils.Add(T("grimoire.tab.runes"), "", "grimoire").Name = "RuneTables";
			sigils.Add(T("grimoire.tab.grindstones"), "", "grindstone").Name = "Grindstones";
			sigils.Add(T("grimoire.tab.gems"), "", "gem").Name = "Gems";
			sigils.Changed += index => tabs.CurrentTab = index;
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
			foreach (var family in _database.Families.OrderByDescending(f => f.Rarity).ThenBy(f => f.BaseName))
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
				CustomMinimumSize = new Vector2(124, 160),
				MouseFilter = MouseFilterEnum.Pass,
				MouseDefaultCursorShape = CursorShape.PointingHand,
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
			art.AddChild(Doodle.Masked(Art.Creature(family.Image), Palette.Grey, MaskShape.Rounded, 6, aura: _awakened ? Element.Light : null));
			column.AddChild(art);
			var name = new Label { Name = "Name", Text = family.BaseName, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
			name.AddThemeFontSizeOverride("font_size", 15);
			column.AddChild(name);
			var count = new Label { Name = "Copies", Text = copies == 0 ? T("grimoire.not_owned") : T("grimoire.copies", copies), ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
			count.AddThemeFontSizeOverride("font_size", 13);
			column.AddChild(count);
			card.AddChild(column);

			void Choose()
			{
				if (selected)
					return;

				// Troca de família mantendo o elemento, quando a nova tem.
				_selected = Variants(family.Id).FirstOrDefault(s => s.Element == _selected.Element) ?? Variants(family.Id).First();
				Callable.From(RefreshSummons).CallDeferred();
			}

			var variant = Variants(family.Id).FirstOrDefault(s => s.Element == _selected.Element) ?? Variants(family.Id).First();
			Press.On(card, Choose, () => MonsterSummary.Open(card, variant, null));
			return card;
		}

		private IEnumerable<SummonDefinition> Variants(string familyId) =>
			_database.Summons.Where(s => s.FamilyId == familyId).OrderBy(s => s.Element);

		/// <summary>Um sigilo por elemento da família (o aceso é o da ficha), e o do Despertar, que troca a ficha para a forma desperta.</summary>
		private Control ElementPicker(SummonDefinition current)
		{
			var row = Layout.Row(10).Named("Elements");
			var variants = Variants(current.FamilyId).ToList();
			var tabs = new TextTabs(false, 48) { Name = "Tabs" };
			foreach (var variant in variants)
			{
				tabs.Add(Texts.Name(variant.Element), _player.Monsters.Count(m => m.SummonId == variant.Id) is var copies and > 0 ? T("grimoire.copies", copies) : "").Name = variant.Element.ToString();
			}

			tabs.Select(variants.IndexOf(current));
			tabs.Changed += index =>
			{
				_selected = variants[index];
				Callable.From(RefreshSummons).CallDeferred();
			};
			row.AddChild(tabs);
			row.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var awaken = GameButton.Of(_awakened ? T("grimoire.view_normal") : T("grimoire.view_awakened"), () =>
			{
				_awakened = !_awakened;
				Callable.From(RefreshSummons).CallDeferred();
			}, _awakened ? ButtonKind.Primary : ButtonKind.Secondary, "awaken", 48).Named("Awakened");
			row.AddChild(awaken);
			return row;
		}

		private void RefreshSheet()
		{
			Layout.Clear(_sheet);
			var summon = _selected;
			var family = summon.Family;
			_sheet.AddChild(new Label { Name = "Family", Text = family.BaseName, ThemeTypeVariation = GameTheme.Title });
			_sheet.AddChild(ElementPicker(summon));

			var identity = Layout.Row(12).Named("Identity");
			var portrait = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(96, 96), MouseFilter = MouseFilterEnum.Stop };
			portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 10, 6));
			portrait.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Rounded, 6, aura: _awakened ? summon.Element : null));
			Press.On(portrait, null, () => MonsterSummary.Open(portrait, summon, null));
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
			line.AddChild(Doodle.Icon(Art.Element(summon.Element), 22, Palette.Of(summon.Element)).Named("Element"));
			line.AddChild(new Label { Name = "ElementName", Text = Texts.Name(summon.Element) });
			line.AddChild(new Label { Name = "Role", Text = $"· {Texts.Name(summon.Role)}", ThemeTypeVariation = GameTheme.Faded });
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
				_sheet.AddChild(SkillRow.Build(summon.AllSkills[i], 1, _awakened, i >= skills.Count, 520, levels: true).Named($"Skill{i + 1}"));

			if (summon.Leader is { } leader)
			{
				var row = Layout.Row(10).Named("Leader");
				row.AddChild(Doodle.Icon(Art.Icon("leader"), 32, Palette.Gold).Named("Icon"));
				row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), 480).Named("Text"));
				_sheet.AddChild(row);
			}

			_sheet.AddChild(new HSeparator { Name = "AwakeningLine" });
			_sheet.AddChild(new Label { Name = "AwakeningTitle", Text = T("grimoire.awakening_title", summon.Awakening.Name), ThemeTypeVariation = GameTheme.Heading });
			var awaken = Layout.Flow(8).Named("Awakening");
			foreach (var (stat, gain) in Texts.AwakeningStats(summon))
				awaken.AddChild(Layout.Labeled(Texts.GlyphOf(stat), gain, Texts.Name(stat)).Named(stat.ToString()));
			awaken.AddChild(Layout.Labeled("essence", Texts.Number(Awakening.Cost(summon.Rarity)), T("grimoire.awaken_cost")).Named("Cost"));
			_sheet.AddChild(awaken);
		}

		// Conjuntos ---------------------------------------------------------------------------------

		private void Sets(VBoxContainer column)
		{
			foreach (var set in RuneSets.All)
			{
				var panel = new PanelContainer { Name = set.Set.ToString(), ThemeTypeVariation = GameTheme.InsetPanel };
				var row = Layout.Row(12).Named("Row");
				row.AddChild(new RuneGlyph(set.Glyph, 44, Palette.Gold) { Name = "Glyph" });
				var text = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(RichText.Label(Texts.Term(set.Set)).Named("Title"));
				text.AddChild(RichText.Label(Texts.Describe(set), 0, GameTheme.Faded).Named("Effect"));
				row.AddChild(text);

				var where = _database.Dungeons.Where(d => d.Sets.Contains(set.Set)).ToList();
				if (where.Count == 0)
				{
					row.AddChild(Layout.Labeled("region", "", T("grimoire.campaign_only")).Named("CampaignOnly"));
				}
				else
				{
					foreach (var dungeon in where)
						row.AddChild(Layout.Labeled(Art.Creature(dungeon.Image), "", dungeon.Name).Named(Layout.NodeName(dungeon.Id)));
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

		/// <summary>Título de tabela e, embaixo, o que as colunas querem dizer.</summary>
		private static Control Heading(string text, string explain)
		{
			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 2);
			column.AddChild(new Label { Name = "Title", Text = text, ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(Layout.Text(explain, GameTheme.Faded).Named("Explain"));
			return column;
		}

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
