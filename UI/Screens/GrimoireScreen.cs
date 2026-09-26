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
	/// O Grimório: tudo o que existe no jogo, em detalhe. Invocações (com a ficha no nível 1 e no 40,
	/// antes e depois do Despertar), conjuntos de runa e onde caem, as tabelas das runas por estrela,
	/// e as faixas das Pedras de Afiar e das Gemas Encantadas. Os números vêm do Core e de Data/.
	/// </summary>
	public partial class GrimoireScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private SummonDefinition _selected;
		private bool _awakened;
		private readonly VBoxContainer _sheet = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly GridContainer _gallery = new() { Columns = 5 };

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
			page.AddChild(Layout.Header(T("grimorio.titulo"), null, T("geral.voltar_santuario"), () => BackRequested?.Invoke()));

			var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			page.AddChild(tabs);
			Summons(tabs);
			Sets(Layout.Tab(tabs, T("grimorio.aba.conjuntos")));
			RuneTables(Layout.Tab(tabs, T("grimorio.aba.runas")));
			Tools(Layout.Tab(tabs, T("grimorio.aba.pedras")), RuneToolKind.Grindstone);
			Tools(Layout.Tab(tabs, T("grimorio.aba.gemas")), RuneToolKind.Gem);
		}

		// Invocações --------------------------------------------------------------------------------

		private void Summons(TabContainer tabs)
		{
			var body = new HBoxContainer();
			body.AddThemeConstantOverride("separation", 12);
			tabs.AddChild(body);
			tabs.SetTabTitle(tabs.GetTabCount() - 1, T("grimorio.aba.invocacoes"));

			var galleryScroll = new ScrollContainer { CustomMinimumSize = new Vector2(560, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_gallery.AddThemeConstantOverride("h_separation", 6);
			_gallery.AddThemeConstantOverride("v_separation", 6);
			galleryScroll.AddChild(_gallery);
			body.AddChild(galleryScroll);

			var sheetScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_sheet.AddThemeConstantOverride("separation", 6);
			sheetScroll.AddChild(_sheet);
			body.AddChild(sheetScroll);

			RefreshSummons();
		}

		private void RefreshSummons()
		{
			Layout.Clear(_gallery);
			foreach (var summon in _database.Summons.OrderByDescending(s => s.Rarity).ThenBy(s => s.FamilyId).ThenBy(s => s.Element))
			{
				var copies = _player.Monsters.Count(m => m.SummonId == summon.Id);
				var card = new CreatureCard(summon, null, copies == 0 ? T("grimorio.nao_obtida") : T("grimorio.copias", copies), 104, _awakened);
				card.Modulate = copies == 0 ? new Color(1, 1, 1, 0.6f) : Colors.White;
				card.SetSelected(summon == _selected);
				card.Pressed += c =>
				{
					_selected = c.Summon;
					RefreshSummons();
				};
				_gallery.AddChild(card);
			}

			RefreshSheet();
		}

		private void RefreshSheet()
		{
			Layout.Clear(_sheet);
			var summon = _selected;
			var name = new Label { Text = summon.NameFor(_awakened), ThemeTypeVariation = GameTheme.Title };
			if (_awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			_sheet.AddChild(name);
			_sheet.AddChild(new Label { Text = T("grimorio.ficha", Texts.Stars(summon.Rarity), Texts.Name(summon.Element), Texts.Name(summon.Role), _database.Families.First(f => f.Id == summon.FamilyId).Name) });

			var toggle = new CheckButton { Text = T("grimorio.ver_desperto", summon.Awakening.Name), ButtonPressed = _awakened };
			toggle.Toggled += on =>
			{
				_awakened = on;
				Callable.From(RefreshSummons).CallDeferred();
			};
			_sheet.AddChild(toggle);

			_sheet.AddChild(new Label { Text = T("grimorio.atributos"), ThemeTypeVariation = GameTheme.Heading });
			var table = new GridContainer { Columns = 3 };
			table.AddThemeConstantOverride("h_separation", 24);
			Cell(table, "", true);
			Cell(table, T("grimorio.nivel", 1), true);
			Cell(table, T("grimorio.nivel", Growth.MaxLevel), true);
			var roleBase = _database.Roles[summon.Role];
			var low = SummonStats.For(roleBase, summon, 1, 0, _awakened, Array.Empty<Rune>()).Base;
			var high = SummonStats.For(roleBase, summon, Growth.MaxLevel, 0, _awakened, Array.Empty<Rune>()).Base;
			foreach (var stat in Enum.GetValues<Stat>())
			{
				Cell(table, Texts.Name(stat));
				Cell(table, Texts.Value(stat, low.Get(stat)));
				Cell(table, Texts.Value(stat, high.Get(stat)));
			}

			_sheet.AddChild(table);

			_sheet.AddChild(new Label { Text = T("grimorio.habilidades"), ThemeTypeVariation = GameTheme.Heading });
			_sheet.AddChild(RichText.Label(T("monstros.basico", summon.Basic.Name)));
			_sheet.AddChild(RichText.Label(Texts.Describe(summon.Basic), 0, GameTheme.Faded));
			_sheet.AddChild(RichText.Label(T("monstros.especial", summon.Special.Name, summon.Special.Cooldown)));
			_sheet.AddChild(RichText.Label(Texts.Describe(summon.Special), 0, GameTheme.Faded));
			_sheet.AddChild(RichText.Label(T("monstros.assinatura", summon.Family.Passive.Name)));
			_sheet.AddChild(RichText.Label(Texts.Describe(summon.Family.Passive, _awakened), 0, GameTheme.Faded));
			_sheet.AddChild(RichText.Label(summon.Leader is { } leader
				? T("monstros.lideranca", Texts.Percent(leader.Value), Texts.Name(leader.Stat))
				: T("grimorio.sem_lideranca")));

			_sheet.AddChild(new Label { Text = T("grimorio.despertar"), ThemeTypeVariation = GameTheme.Heading });
			_sheet.AddChild(RichText.Label(T("monstros.despertar_texto", summon.Awakening.Name, Texts.Percent(Awakening.HealthBonus), Texts.Percent(Awakening.AttackDefenseBonus),
				Texts.AwakeningBonus(summon.Awakening.Stat), Texts.Describe(summon.Family.Passive, true)), 0, GameTheme.Faded));
			_sheet.AddChild(new Label { Text = T("grimorio.custo_despertar", Awakening.Cost(summon.Rarity)), ThemeTypeVariation = GameTheme.Faded });
		}

		// Conjuntos ---------------------------------------------------------------------------------

		private void Sets(VBoxContainer column)
		{
			column.AddChild(Layout.Text(T("grimorio.conjuntos_intro"), GameTheme.Faded));
			foreach (var set in RuneSets.All)
			{
				var panel = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 12);
				row.AddChild(Doodle.Icon(Art.Glyph(set.Glyph), 44, Palette.Gold));
				var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
				text.AddChild(RichText.Label($"{Texts.Term(set.Set)}  [color=#{Palette.TextFaded.ToHtml(false)}]{T("grimorio.glifo_de", Texts.Name(set.Glyph), Texts.Meaning(set.Glyph))}[/color]"));
				text.AddChild(RichText.Label(Texts.Describe(set)));
				var where = _database.Dungeons.Where(d => d.Sets.Contains(set.Set)).Select(d => d.Name).ToList();
				text.AddChild(new Label { Text = T("grimorio.onde", where.Count == 0 ? T("grimorio.so_campanha") : string.Join(", ", where)), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(text);
				panel.AddChild(row);
				column.AddChild(panel);
			}
		}

		// Tabelas das runas -------------------------------------------------------------------------

		private static void RuneTables(VBoxContainer column)
		{
			var grades = Enumerable.Range(1, RuneRules.MaxGrade).ToList();

			column.AddChild(new Label { Text = T("grimorio.principal"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(Layout.Text(T("grimorio.principal_intro"), GameTheme.Faded));
			var mains = Enum.GetValues<RuneStat>();
			var main = Table(column, grades.Count + 1, new[] { T("grimorio.atributo") }.Concat(grades.Select(Texts.Stars)));
			foreach (var stat in mains)
			{
				Cell(main, Texts.Label(stat));
				foreach (var grade in grades)
					Cell(main, $"{Short(stat, RuneRules.MainValue(stat, grade, 0))} · {Short(stat, RuneRules.MainValue(stat, grade, 12))} · {Short(stat, RuneRules.MainValue(stat, grade, RuneRules.MaxLevel))}");
			}

			column.AddChild(new Label { Text = T("grimorio.sub"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(Layout.Text(T("grimorio.sub_intro"), GameTheme.Faded));
			var sub = Table(column, grades.Count + 1, new[] { T("grimorio.atributo") }.Concat(grades.Select(Texts.Stars)));
			foreach (var stat in mains)
			{
				Cell(sub, Texts.Label(stat));
				foreach (var grade in grades)
				{
					var (min, max) = RuneRules.SubstatRange(stat, grade);
					Cell(sub, $"{Short(stat, min)}–{Short(stat, max)}");
				}
			}

			column.AddChild(new Label { Text = T("grimorio.custo"), ThemeTypeVariation = GameTheme.Heading });
			column.AddChild(Layout.Text(T("grimorio.custo_intro"), GameTheme.Faded));
			var cost = Table(column, grades.Count + 1, new[] { T("grimorio.melhora") }.Concat(grades.Select(Texts.Stars)));
			foreach (var target in new[] { 3, 6, 9, 12, 15 })
			{
				Cell(cost, T("grimorio.ate", target));
				foreach (var grade in grades)
					Cell(cost, RuneRules.UpgradeCost(new Rune { Grade = grade }, target).ToString());
			}

			Cell(cost, T("grimorio.desfazer"));
			foreach (var grade in grades)
				Cell(cost, T("grimorio.desfazer_valor", RuneRules.SellValue(new Rune { Grade = grade }), RuneRules.SellValue(new Rune { Grade = grade, Substats = Enumerable.Range(0, 4).Select(_ => new RuneSubstat()).ToList() })));
		}

		private static void Tools(VBoxContainer column, RuneToolKind kind)
		{
			var grades = new[] { RuneRarity.Magic, RuneRarity.Rare, RuneRarity.Hero, RuneRarity.Legendary };
			column.AddChild(Layout.Text(T(kind == RuneToolKind.Grindstone ? "grimorio.pedras_intro" : "grimorio.gemas_intro", RuneForge.EnchantLevel), GameTheme.Faded));
			var table = Table(column, grades.Length + 1, new[] { T("grimorio.atributo") }.Concat(grades.Select(Texts.Name)));
			foreach (var stat in Enum.GetValues<RuneStat>())
			{
				if (kind == RuneToolKind.Grindstone && !RuneRules.IsGrindable(stat))
					continue;
				Cell(table, Texts.Label(stat));
				foreach (var grade in grades)
					Cell(table, Texts.Range(new RuneTool(kind, stat, grade)));
			}

			column.AddChild(Layout.Text(T("grimorio.pedras_onde"), GameTheme.Faded));
		}

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
