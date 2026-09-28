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
	/// Runas. À esquerda o carrossel em arco dos monstros (Baú incluído) sobre o círculo de conjuração
	/// com os 6 espaços do escolhido (GDD, seção 10), e a ficha dele. No meio, pelas abas de sigilo, o
	/// inventário de runas (a busca é uma fileira de sigilos, cada um abre o carrossel das opções; a
	/// seleção em massa desfaz de uma vez), as Pedras de Afiar e as Gemas Encantadas. À direita a runa
	/// escolhida, com os sigilos de equipar, tirar, melhorar, afiar, encantar e desfazer.
	///
	/// O que a runa ganhou desde que a tela abriu aparece em verde ao lado do valor: o quanto subiu
	/// ("HP +1215 +945", "Speed +9 +4"), ou o subatributo novo inteiro em verde, com "new". Sair da tela
	/// apaga o destaque; o histórico de verdade fica na runa (<see cref="RuneSubstat.Rolls"/>). Os sigilos
	/// de ação ficam presos embaixo da ficha, fora da rolagem.
	///
	/// Os nós, pelo nome (<c>Page/Content/Body/...</c>): <c>Left/Column</c> tem <c>Stage</c> (o <c>Ring</c>
	/// com <c>Monster</c> e <c>Slot1</c>..<c>Slot6</c>, e o carrossel <c>Monsters</c>), <c>Sets</c> e
	/// <c>Stats</c>; <c>Middle/Column</c> tem <c>TabRow/Tabs</c> e <c>List</c> (<c>Filters</c>, <c>Selection</c>
	/// e <c>Scroll/Grid</c> com <c>Rune&lt;id&gt;</c>, ou <c>Scroll/Tools</c> com <c>Tool&lt;n&gt;</c>);
	/// <c>Right/Column</c> tem a ficha da runa em <c>Scroll/Detail</c> (<c>Head</c>, <c>SetEffect</c>,
	/// <c>Main</c>, <c>Substat1</c>.., <c>Holder</c>) e, embaixo, <c>ActionsLine</c> e <c>Actions</c>.
	/// </summary>
	public partial class RuneScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private int? _monsterId;
		private int _slotFilter;
		private int? _selectedRune;
		private int _tab;
		private RuneFilter _filter = new();
		private RunePlace _place;
		private bool _selecting;
		private readonly HashSet<int> _marked = new();

		/// <summary>Nível de cada runa quando a tela abriu: o que passou disso ganha destaque.</summary>
		private readonly Dictionary<int, int> _levelWhenOpened;

		/// <summary>Onde procurar: soltas, em monstros da coleção, em monstros do Baú ou todas.</summary>
		private enum RunePlace
		{
			Inventory,
			Collection,
			Storage,
			All,
		}

		private readonly CurrencyBar _currencies = new();
		private readonly Label _count = new() { Name = "Count", ThemeTypeVariation = GameTheme.Number, VerticalAlignment = VerticalAlignment.Center };
		private readonly VBoxContainer _left = new() { Name = "Column" };
		private readonly HBoxContainer _tabs = Layout.Row(8).Named("TabRow");
		private readonly VBoxContainer _middle = new() { Name = "List", SizeFlagsVertical = SizeFlags.ExpandFill };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly HSeparator _actionsLine = new() { Name = "ActionsLine" };
		private readonly HFlowContainer _actions = Layout.Flow(10).Named("Actions");

		public RuneScreen(GameDatabase database, PlayerState player, int? monsterId)
		{
			_database = database;
			_player = player;
			_monsterId = player.Monster(monsterId ?? -1) != null ? monsterId : null;
			_levelWhenOpened = player.Runes.ToDictionary(r => r.Id, r => r.Level);
		}

		/// <summary>Runa e monstro.</summary>
		public event Action<int, int>? EquipRequested;

		public event Action<int>? UnequipRequested;

		/// <summary>Id da runa e o nível a alcançar.</summary>
		public event Action<int, int>? UpgradeRequested;

		/// <summary>Id da runa, índice do subatributo e a Pedra de Afiar.</summary>
		public event Action<int, int, RuneTool>? GrindRequested;

		/// <summary>Id da runa, índice do subatributo e a Gema Encantada.</summary>
		public event Action<int, int, RuneTool>? EnchantRequested;

		public event Action<int>? SellRequested;
		public event Action<IReadOnlyList<int>>? SellManyRequested;
		public event Action<int?>? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			var (header, extra) = Layout.Header(T("destination.Runes"), "rune", _currencies, () => BackRequested?.Invoke(_monsterId));
			extra.AddChild(_count);
			page.AddChild(header);

			var body = Layout.Row(12).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var left = new PanelContainer { Name = "Left", CustomMinimumSize = new Vector2(390, 0) };
			_left.AddThemeConstantOverride("separation", 6);
			left.AddChild(_left);
			body.AddChild(left);

			var middle = new PanelContainer { Name = "Middle", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var middleColumn = new VBoxContainer { Name = "Column" };
			middleColumn.AddThemeConstantOverride("separation", 8);
			middleColumn.AddChild(_tabs);
			middleColumn.AddChild(_middle);
			middle.AddChild(middleColumn);
			body.AddChild(middle);

			var right = new PanelContainer { Name = "Right", CustomMinimumSize = new Vector2(340, 0) };
			var rightColumn = new VBoxContainer { Name = "Column" };
			rightColumn.AddThemeConstantOverride("separation", 8);
			_detail.AddThemeConstantOverride("separation", 8);
			rightColumn.AddChild(Layout.Scroll(_detail));
			rightColumn.AddChild(_actionsLine);
			rightColumn.AddChild(_actions);
			right.AddChild(rightColumn);
			body.AddChild(right);

			Refresh();
		}

		public void Refresh()
		{
			if (_selectedRune is { } id && _player.Runes.All(r => r.Id != id))
				_selectedRune = null;
			_marked.RemoveWhere(runeId => _player.Runes.All(r => r.Id != runeId));

			_currencies.Refresh(_player);
			_count.Text = $"{RuneInventory.Count(_player)}/{RuneInventory.Capacity}";
			RefreshLeft();
			RefreshTabs();
			RefreshMiddle();
			RefreshDetail();
		}

		// Monstro e círculo -------------------------------------------------------------------------

		private OwnedSummon? Monster => _monsterId is { } id ? _player.Monster(id) : null;

		private void RefreshLeft()
		{
			Layout.Clear(_left);
			var monsters = _player.Monsters.Where(m => _database.HasSummon(m.SummonId)).OrderBy(m => m.Stored).ThenByDescending(m => m.Stars).ThenByDescending(m => m.Level).ToList();
			var items = new List<ArcItem> { new(Art.Icon("cancel"), T("runes.no_monster"), Palette.TextFaded) };
			foreach (var candidate in monsters)
			{
				var summon = _database.Summon(candidate.SummonId);
				var name = T(candidate.Stored ? "runes.monster_item_vault" : "runes.monster_item", summon.NameFor(candidate.Awakened), candidate.Level);
				items.Add(new ArcItem(Art.Creature(summon.ImageFor(candidate.Awakened)), name, Palette.Of(summon.Element), Accent: Palette.Frame(summon.Rarity)));
			}

			// O círculo sobe para dentro do arco do carrossel: as pontas do arco descem pelos lados, onde
			// o círculo ainda é estreito, e a ficha inteira cabe embaixo sem rolagem.
			var stage = new Control { Name = "Stage", CustomMinimumSize = new Vector2(0, RingTop + RingSize), MouseFilter = MouseFilterEnum.Ignore };
			_left.AddChild(stage);

			var ring = new SigilRing(RingSize) { Name = "Ring", Spread = 0.72f };
			Place(stage, ring, new Vector2(RingSize, RingSize), RingTop);

			var picker = new ArcCarousel(items, Monster == null ? 0 : monsters.FindIndex(m => m.Id == _monsterId) + 1, 50, 250, 5) { Name = "Monsters" };
			picker.Selected += index =>
			{
				_monsterId = index == 0 ? null : monsters[index - 1].Id;
				Callable.From(Refresh).CallDeferred();
			};
			Place(stage, picker, picker.CustomMinimumSize, 0);
			var tiles = new List<Control>();
			var equipped = Monster is { } holder ? _player.RunesOn(holder.Id) : Array.Empty<Rune>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(equipped.FirstOrDefault(r => r.Slot == slot), slot, 1.05f) { Name = $"Slot{slot}" };
				tile.SetSelected(tile.Rune != null ? tile.Rune.Id == _selectedRune : slot == _slotFilter);
				tile.Pressed += t =>
				{
					_slotFilter = t.Slot;
					_filter = _filter with { Slot = t.Slot };
					_selectedRune = t.Rune?.Id;
					_tab = 0;
					Refresh();
				};
				tiles.Add(tile);
			}

			Control? center = null;
			if (Monster is { } monster)
			{
				var summon = _database.Summon(monster.SummonId);
				center = Layout.Medal(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element), 88).Named("Monster");
			}

			ring.Set(center, tiles);

			if (Monster is not { } chosen)
				return;

			var chosenSummon = _database.Summon(chosen.SummonId);
			var sheet = SummonStats.For(_database.Roles[chosenSummon.Role], chosenSummon, chosen.Stars, chosen.Level, chosen.Awakened, _player.RunesOn(chosen.Id));
			if (sheet.Runes.ActiveSets.Count > 0)
			{
				var sets = Layout.Flow(6).Named("Sets");
				// Conjunto de 2 peças pode estar ativo mais de uma vez: a posição na fileira deixa o nome único.
				for (var i = 0; i < sheet.Runes.ActiveSets.Count; i++)
				{
					var set = sheet.Runes.ActiveSets[i];
					sets.AddChild(Layout.Chip(RuneSets.For(set.Set).Glyph, Texts.Name(set.Set), Texts.Plain($"{Texts.Name(set.Set)}: {Texts.Describe(set)}")).Named($"{set.Set}{i + 1}"));
				}
				_left.AddChild(sets);
			}

			var table = new StatTable { Name = "Stats" };
			table.Show(sheet);
			_left.AddChild(table);
		}

		private const float RingSize = 280;
		private const float RingTop = 56;

		/// <summary>Põe um controle centrado na largura de <paramref name="stage"/>, a <paramref name="top"/> px do alto.</summary>
		private static void Place(Control stage, Control control, Vector2 size, float top)
		{
			control.SetAnchorsPreset(LayoutPreset.CenterTop);
			control.OffsetLeft = -size.X / 2;
			control.OffsetRight = size.X / 2;
			control.OffsetTop = top;
			control.OffsetBottom = top + size.Y;
			stage.AddChild(control);
		}

		// Abas do meio ------------------------------------------------------------------------------

		private void RefreshTabs()
		{
			Layout.Clear(_tabs);
			var tabs = new SigilTabs(vertical: false, 50) { Name = "Tabs" };
			tabs.Add(Art.Icon("rune"), T("runes.tab_runes"), RuneInventory.Count(_player).ToString()).Name = "Runes";
			tabs.Add(Art.Icon("grindstone"), T("runes.tab_grindstones"), _player.Tools.Count(t => t.Kind == RuneToolKind.Grindstone).ToString()).Name = "Grindstones";
			tabs.Add(Art.Icon("gem"), T("runes.tab_gems"), _player.Tools.Count(t => t.Kind == RuneToolKind.Gem).ToString()).Name = "Gems";
			tabs.Select(_tab);
			tabs.Changed += index =>
			{
				_tab = index;
				Callable.From(Refresh).CallDeferred();
			};
			_tabs.AddChild(tabs);
		}

		private void RefreshMiddle()
		{
			Layout.Clear(_middle);
			if (_tab == 0)
				RuneList();
			else
				ToolList(_tab == 1 ? RuneToolKind.Grindstone : RuneToolKind.Gem);
		}

		private void RuneList()
		{
			var runes = _filter.Apply(_player.Runes.Where(InPlace)).ToList();
			_middle.AddChild(FilterBar(runes.Count));

			if (_selecting)
				_middle.AddChild(SelectionBar(runes));

			var grid = Layout.Flow(8).Named("Grid");
			foreach (var rune in runes)
			{
				var tile = new RuneTile(rune, rune.Slot) { Name = $"Rune{rune.Id}" };
				tile.SetSelected(rune.Id == _selectedRune);
				tile.SetMarked(_marked.Contains(rune.Id));
				if (rune.EquippedOn is { } owner && _player.Monster(owner) is { } holder)
				{
					var summon = _database.Summon(holder.SummonId);
					tile.SetOwner(Art.Creature(summon.ImageFor(holder.Awakened)), Palette.Of(summon.Element), holder.Stored, summon.NameFor(holder.Awakened));
				}

				tile.Pressed += t =>
				{
					var clicked = t.Rune!;
					if (_selecting && clicked.EquippedOn == null)
					{
						if (!_marked.Remove(clicked.Id))
							_marked.Add(clicked.Id);
					}
					else
					{
						_selectedRune = clicked.Id;
					}

					Refresh();
				};
				grid.AddChild(tile);
			}

			_middle.AddChild(Layout.Scroll(grid));
		}

		/// <summary>A busca: um sigilo por campo (conjunto, espaço, principal, subatributo, estrelas, raridade, melhora, ordem, onde), o de limpar e o de selecionar.</summary>
		private Control FilterBar(int found)
		{
			var flow = Layout.Flow(6).Named("Filters");
			// "Todos" em cada campo: o símbolo do campo, apagado; escolhido um valor, acende em ouro.
			ArcItem All(string icon) => new(Art.Icon(icon), T("filter.all"), Palette.TextFaded);
			ArcItem AllLetters(string letters) => new(null, T("filter.all"), Palette.TextFaded, letters);

			flow.AddChild(Picker("Set", T("filter.set"), Enum.GetValues<RuneSet>().Select(s => (new ArcItem(null, Texts.Name(s), Palette.Gold, Rune: RuneSets.For(s).Glyph), (int)s)), All("rune"), _filter.Set is { } set ? (int)set : -1,
				value => _filter = _filter with { Set = value < 0 ? null : (RuneSet)value }));
			flow.AddChild(Picker("Slot", T("filter.slot"), Enumerable.Range(1, RuneRules.Slots).Select(s => (Letters(s.ToString(), T("filter.slot_n", s)), s)), AllLetters("#"), _filter.Slot ?? -1,
				value =>
				{
					_filter = _filter with { Slot = value < 0 ? null : value };
					_slotFilter = Math.Max(0, value);
				}));
			flow.AddChild(Picker("Main", T("filter.main"), Enum.GetValues<RuneStat>().Select(s => (Letters(Texts.Short(s), Texts.Label(s)), (int)s)), AllLetters("◆"), _filter.Main is { } main ? (int)main : -1,
				value => _filter = _filter with { Main = value < 0 ? null : (RuneStat)value }));
			flow.AddChild(Picker("Substat", T("filter.substat"), Enum.GetValues<RuneStat>().Select(s => (Letters(Texts.Short(s), Texts.Label(s)), (int)s)), AllLetters("◇"), _filter.Substats.Count > 0 ? (int)_filter.Substats[0] : -1,
				value => _filter = _filter with { Substats = value < 0 ? Array.Empty<RuneStat>() : new[] { (RuneStat)value } }));
			flow.AddChild(Picker("Stars", T("filter.stars"), Enumerable.Range(2, RuneRules.MaxGrade - 1).Select(g => (Letters($"{g}★", T("filter.at_least", Texts.Stars(g))), g)), AllLetters("★"), _filter.MinGrade > 1 ? _filter.MinGrade : -1,
				value => _filter = _filter with { MinGrade = Math.Max(1, value) }));
			flow.AddChild(Picker("Rarity", T("filter.rarity"), Enum.GetValues<RuneRarity>().Skip(1).Select(r => (new ArcItem(Art.Icon("gem"), T("filter.at_least", Texts.Name(r)), Palette.Of(r)), (int)r)), All("gem"), _filter.MinRarity > RuneRarity.Normal ? (int)_filter.MinRarity : -1,
				value => _filter = _filter with { MinRarity = value < 0 ? RuneRarity.Normal : (RuneRarity)value }));
			flow.AddChild(Picker("Upgrade", T("filter.upgrade"), new[] { 3, 6, 9, 12, 15 }.Select(l => (Letters($"+{l}", T("filter.at_least", $"+{l}")), l)), AllLetters("+"), _filter.MinLevel > 0 ? _filter.MinLevel : -1,
				value => _filter = _filter with { MinLevel = Math.Max(0, value) }));

			var sort = new SigilPicker(T("filter.sort"), Enum.GetValues<RuneSort>().Select(s => (SortItem(s), (int)s)).ToList(), (int)_filter.Sort) { Name = "Sort" };
			sort.Changed += value =>
			{
				_filter = _filter with { Sort = (RuneSort)value };
				Callable.From(Refresh).CallDeferred();
			};
			flow.AddChild(sort);

			var place = new SigilPicker(T("filter.where"), Enum.GetValues<RunePlace>().Select(p => (PlaceItem(p), (int)p)).ToList(), (int)_place) { Name = "Place" };
			place.Changed += value =>
			{
				_place = (RunePlace)value;
				Callable.From(Refresh).CallDeferred();
			};
			flow.AddChild(place);

			var clear = SigilButton.Of("cancel", T("filter.clear"), () =>
			{
				_filter = new RuneFilter();
				_slotFilter = 0;
				Refresh();
			}, 44).Named("Clear");
			flow.AddChild(clear);

			flow.AddChild(Layout.Chip("search", found.ToString(), T("runes.found")).Named("Found"));
			var select = new SigilButton(Art.Icon("select"), T("runes.select"), 50, SigilShape.Square) { Name = "Select", ToggleMode = true, ButtonPressed = _selecting };
			select.Toggled += on =>
			{
				_selecting = on;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			};
			flow.AddChild(select);
			return flow;
		}

		/// <summary>Um campo de busca, com o nome de nó <paramref name="name"/>: a primeira opção é "todos" (-1).</summary>
		private SigilPicker Picker(string name, string title, IEnumerable<(ArcItem Item, int Value)> options, ArcItem all, int current, Action<int> changed)
		{
			var list = new List<(ArcItem Item, int Value)> { (all, -1) };
			list.AddRange(options);
			var picker = new SigilPicker(title, list, current) { Name = name };
			picker.Changed += value =>
			{
				changed(value);
				Callable.From(Refresh).CallDeferred();
			};
			return picker;
		}

		private static ArcItem Letters(string letters, string tooltip) => new(null, tooltip, Palette.Gold, letters);

		private static ArcItem SortItem(RuneSort sort) => sort switch
		{
			RuneSort.Grade => new ArcItem(Art.Icon("evolve"), Texts.Name(sort), Palette.Gold),
			RuneSort.Level => new ArcItem(Art.Icon("level_max"), Texts.Name(sort), Palette.Gold),
			RuneSort.Rarity => new ArcItem(Art.Icon("gem"), Texts.Name(sort), Palette.Gold),
			RuneSort.Set => new ArcItem(Art.Icon("rune"), Texts.Name(sort), Palette.Gold),
			RuneSort.Newest => new ArcItem(Art.Icon("collect"), Texts.Name(sort), Palette.Gold),
			_ => Letters("#", Texts.Name(sort)),
		};

		private static ArcItem PlaceItem(RunePlace place) => place switch
		{
			RunePlace.Inventory => new ArcItem(Art.Icon("rune"), T("filter.place.Inventory"), Palette.Gold),
			RunePlace.Collection => new ArcItem(Art.Icon("storage"), T("filter.place.Collection"), Palette.Gold),
			RunePlace.Storage => new ArcItem(Art.Icon("chest"), T("filter.place.Storage"), Palette.Gold),
			_ => new ArcItem(Art.Icon("bag"), T("filter.place.All"), Palette.Gold),
		};

		private Control SelectionBar(IReadOnlyList<Rune> visible)
		{
			var row = Layout.Row(8).Named("Selection");
			row.AddChild(SigilButton.Of("copies", T("runes.mark_all"), () =>
			{
				foreach (var rune in visible.Where(r => r.EquippedOn == null))
					_marked.Add(rune.Id);
				Refresh();
			}, 48).Named("MarkAll"));
			row.AddChild(SigilButton.Of("cancel", T("runes.unmark"), () =>
			{
				_marked.Clear();
				Refresh();
			}, 48).Named("Unmark"));

			var marked = _player.Runes.Where(r => _marked.Contains(r.Id)).ToList();
			var value = marked.Sum(RuneRules.SellValue);
			var sell = SigilButton.Of("dismantle", T("runes.sell_marked", marked.Count, value), () =>
			{
				var ids = marked.Select(r => r.Id).ToList();
				_marked.Clear();
				SellManyRequested?.Invoke(ids);
			}, 56, SigilShape.Diamond).Named("SellMarked");
			sell.Disabled = marked.Count == 0;
			sell.Badge = marked.Count > 0 ? marked.Count.ToString() : "";
			row.AddChild(sell);
			return row;
		}

		private void ToolList(RuneToolKind kind)
		{
			var groups = _player.Tools.Where(t => t.Kind == kind).GroupBy(t => t).OrderByDescending(g => g.Key.Grade).ThenBy(g => g.Key.Stat).ToList();
			if (groups.Count == 0)
			{
				var empty = Doodle.Icon(Art.Icon(kind == RuneToolKind.Grindstone ? "grindstone" : "gem"), 96, new Color(Palette.GoldDark, 0.5f)).Named("Icon");
				empty.TooltipText = T("runes.no_tools");
				empty.MouseFilter = MouseFilterEnum.Stop;
				var center = new CenterContainer { Name = "Empty", SizeFlagsVertical = SizeFlags.ExpandFill };
				center.AddChild(empty);
				_middle.AddChild(center);
				return;
			}

			var list = new VBoxContainer { Name = "Tools" };
			list.AddThemeConstantOverride("separation", 6);
			for (var i = 0; i < groups.Count; i++)
			{
				var group = groups[i];
				var row = Layout.Row(10).Named($"Tool{i + 1}");
				row.AddChild(Doodle.Icon(Art.Icon(kind == RuneToolKind.Grindstone ? "grindstone" : "gem"), 30, Palette.Of(group.Key.Grade)).Named("Icon"));
				row.AddChild(new RuneGlyph(Texts.GlyphOf(group.Key.Stat), 22, Palette.Gold) { Name = "Glyph" });
				var name = new Label { Name = "Stat", Text = Texts.Label(group.Key.Stat), SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = Texts.Name(group.Key), MouseFilter = MouseFilterEnum.Stop };
				name.AddThemeColorOverride("font_color", Palette.Of(group.Key.Grade));
				row.AddChild(name);
				row.AddChild(new Label { Name = "Range", Text = Texts.Range(group.Key), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(new Label { Name = "Count", Text = $"×{group.Count()}", ThemeTypeVariation = GameTheme.Number, CustomMinimumSize = new Vector2(44, 0), HorizontalAlignment = HorizontalAlignment.Right });
				list.AddChild(row);
			}

			_middle.AddChild(Layout.Scroll(list));
		}

		// Ficha da runa -----------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			Layout.Clear(_actions);
			var rune = _player.Runes.FirstOrDefault(r => r.Id == _selectedRune);
			_actionsLine.Visible = _actions.Visible = rune != null;
			if (rune == null)
			{
				var center = new CenterContainer { Name = "Empty", CustomMinimumSize = new Vector2(0, 300) };
				center.AddChild(Doodle.Icon(Art.Icon("rune"), 110, new Color(Palette.GoldDark, 0.45f)).Named("Icon"));
				_detail.AddChild(center);
				return;
			}

			var opened = _levelWhenOpened.GetValueOrDefault(rune.Id, rune.Level);
			if (!_levelWhenOpened.ContainsKey(rune.Id))
				_levelWhenOpened[rune.Id] = rune.Level;

			var color = Palette.Of(rune.Rarity);
			var head = Layout.Row(10).Named("Head");
			head.AddChild(new RuneTile(rune, rune.Slot, 1.4f) { Name = "Tile", MouseFilter = MouseFilterEnum.Ignore });
			var titles = new VBoxContainer { Name = "Titles", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var title = new Label { Name = "Title", Text = Texts.Title(rune), ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			title.AddThemeColorOverride("font_color", color);
			titles.AddChild(title);
			var grade = new Label { Name = "Grade", Text = $"{Texts.Stars(rune.Grade)}  +{rune.Level}", TooltipText = Texts.Name(rune.Rarity), MouseFilter = MouseFilterEnum.Stop };
			grade.AddThemeColorOverride("font_color", color);
			titles.AddChild(grade);
			head.AddChild(titles);
			_detail.AddChild(head);
			_detail.AddChild(RichText.Label(Texts.Describe(RuneSets.For(rune.Set)), 300, GameTheme.Faded, 13).Named("SetEffect"));

			var main = Layout.Row(10).Named("Main");
			var mainValue = new Label { Name = "Value", Text = Texts.Format(rune.Main, rune.MainValue) };
			mainValue.AddThemeFontOverride("font", GameTheme.Serif);
			mainValue.AddThemeFontSizeOverride("font_size", 20);
			main.AddChild(mainValue);
			if (rune.Level > opened)
				main.AddChild(Gain("Gain", Texts.Amount(rune.Main, rune.MainValue - RuneRules.MainValue(rune.Main, rune.Grade, opened)), 20));
			_detail.AddChild(main);

			if (rune.Innate is { } innate)
			{
				var label = new Label { Name = "Innate", Text = Texts.Format(innate), TooltipText = T("runes.innate"), MouseFilter = MouseFilterEnum.Stop };
				label.AddThemeColorOverride("font_color", Palette.Gold);
				_detail.AddChild(label);
			}

			_detail.AddChild(new HSeparator { Name = "SubstatsLine" });
			for (var i = 0; i < rune.Substats.Count; i++)
				_detail.AddChild(SubstatRow(rune, i, opened).Named($"Substat{i + 1}"));

			if (rune.EquippedOn is { } owner && owner != _monsterId && _player.Monster(owner) is { } holder)
			{
				var summon = _database.Summon(holder.SummonId);
				var row = Layout.Row(6).Named("Holder");
				var who = Layout.Medal(Art.Creature(summon.ImageFor(holder.Awakened)), Palette.Of(summon.Element), 32);
				who.TooltipText = T(holder.Stored ? "runes.equipped_on_vault" : "runes.equipped_on", summon.NameFor(holder.Awakened));
				who.MouseFilter = MouseFilterEnum.Stop;
				row.AddChild(who);
				if (holder.Stored)
					row.AddChild(Doodle.Icon(Art.Icon("chest"), 22, Palette.TextFaded).Named("Vault"));
				_detail.AddChild(row);
			}

			var full = RuneInventory.IsFull(_player);
			var fullTip = full ? T("runes.inventory_full", RuneInventory.Capacity) : T("runes.remove");
			if (rune.EquippedOn != null && rune.EquippedOn == _monsterId)
				_actions.AddChild(Act("Unequip", "cancel", fullTip, full, () => UnequipRequested?.Invoke(rune.Id)));
			else if (Monster is { } monster)
				_actions.AddChild(Act("Equip", "confirm", T("runes.equip_on", _database.Summon(monster.SummonId).NameFor(monster.Awakened)), false, () => EquipRequested?.Invoke(rune.Id, monster.Id)));
			else if (rune.EquippedOn != null)
				_actions.AddChild(Act("Unequip", "cancel", fullTip, full, () => UnequipRequested?.Invoke(rune.Id)));

			if (rune.Level < RuneRules.MaxLevel)
			{
				var next = RuneRules.UpgradeCost(rune);
				var up = Act("Upgrade", "essence", T("runes.upgrade_to", rune.Level + 1, next), _player.Essence < next, () => UpgradeRequested?.Invoke(rune.Id, rune.Level + 1));
				up.Badge = $"+{rune.Level + 1}";
				_actions.AddChild(up);

				var milestone = RuneRules.NextMilestone(rune.Level);
				if (milestone > rune.Level + 1)
				{
					var total = RuneRules.UpgradeCost(rune, milestone);
					var jump = Act("UpgradeToMilestone", "level_max", T("runes.upgrade_milestone", milestone, total), _player.Essence < total, () => UpgradeRequested?.Invoke(rune.Id, milestone));
					jump.Badge = $"+{milestone}";
					_actions.AddChild(jump);
				}
			}

			if (rune.EquippedOn == null)
			{
				var sell = Act("Sell", "dismantle", T("runes.sell", RuneRules.SellValue(rune)), false, () => SellRequested?.Invoke(rune.Id));
				sell.Badge = RuneRules.SellValue(rune).ToString();
				_actions.AddChild(sell);
			}
		}

		/// <summary>O que a runa ganhou desde que a tela abriu, em verde, ao lado do valor.</summary>
		private static Label Gain(string name, string text, int size = 0)
		{
			var label = new Label { Name = name, Text = text, VerticalAlignment = VerticalAlignment.Center };
			label.AddThemeColorOverride("font_color", Palette.Positive);
			if (size > 0)
			{
				label.AddThemeFontOverride("font", GameTheme.Serif);
				label.AddThemeFontSizeOverride("font_size", size);
			}

			return label;
		}

		/// <summary>
		/// Um subatributo com os sigilos das pedras que servem nele. O que mudou desde <paramref name="opened"/>
		/// (o nível da runa quando a tela abriu) fica em verde: a linha inteira com "new" se ele nasceu
		/// depois, ou o quanto subiu ao lado do valor.
		/// </summary>
		private Control SubstatRow(Rune rune, int index, int opened)
		{
			var substat = rune.Substats[index];
			var row = Layout.Row(6);
			var isNew = substat.Rolls.Count > 0 && substat.Rolls[0].Level > opened;
			var gained = substat.Rolls.Where(r => r.Level > opened).Sum(r => r.Amount);
			row.AddChild(new RuneGlyph(Texts.GlyphOf(substat.Stat), 18, isNew ? Palette.Positive : Palette.GoldDark.Lightened(0.3f)) { Name = "Glyph" });
			var label = new Label { Name = "Value", Text = Texts.Format(substat), VerticalAlignment = VerticalAlignment.Center };
			if (substat.Enchanted)
			{
				label.Text += " ◆";
				label.TooltipText = T("runes.enchanted_tip");
				label.MouseFilter = MouseFilterEnum.Stop;
			}

			row.AddChild(label);
			if (isNew)
			{
				label.AddThemeColorOverride("font_color", Palette.Positive);
				row.AddChild(Gain("New", T("runes.new")));
			}
			else if (gained > 0)
			{
				row.AddChild(Gain("Gain", Texts.Amount(substat.Stat, gained)));
			}

			row.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

			var grindstones = Usable(t => RuneForge.CanGrind(rune, index, t));
			if (grindstones.Count > 0)
				row.AddChild(ToolMenu("grindstone", T("runes.grind"), grindstones, tool => GrindRequested?.Invoke(rune.Id, index, tool)).Named("Grind"));

			var gems = Usable(t => RuneForge.CanEnchant(rune, index, t));
			if (gems.Count > 0)
				row.AddChild(ToolMenu("gem", T("runes.enchant"), gems, tool => EnchantRequested?.Invoke(rune.Id, index, tool)).Named("Enchant"));

			return row;
		}

		/// <summary>Pedras diferentes que servem, uma de cada.</summary>
		private List<RuneTool> Usable(Func<RuneTool, bool> fits) =>
			_player.Tools.Distinct().Where(fits).OrderByDescending(t => t.Grade).ToList();

		/// <summary>O sigilo da pedra: abre o carrossel das pedras que servem neste subatributo.</summary>
		private SigilButton ToolMenu(string icon, string tooltip, IReadOnlyList<RuneTool> tools, Action<RuneTool> chosen)
		{
			var button = new SigilButton(Art.Icon(icon), tooltip, 38);
			button.Pressed += () => ArcPicker.Open(this,
				tools.Select(t => new ArcItem(Art.Icon(icon), $"{Texts.Name(t)} ({Texts.Range(t)})", Palette.Of(t.Grade))).ToList(),
				0, button.GetGlobalRect().GetCenter(), index => chosen(tools[index]));
			return button;
		}

		/// <summary>Onde a runa está, para a busca.</summary>
		private bool InPlace(Rune rune) => _place switch
		{
			RunePlace.Inventory => rune.EquippedOn == null,
			RunePlace.Collection => _player.Monster(rune.EquippedOn ?? -1) is { Stored: false },
			RunePlace.Storage => _player.Monster(rune.EquippedOn ?? -1) is { Stored: true },
			_ => true,
		};

		/// <summary>Um sigilo de ação da ficha; <paramref name="name"/> é o que ele faz (<c>Equip</c>, <c>Sell</c>), não o símbolo.</summary>
		private static SigilButton Act(string name, string icon, string tooltip, bool disabled, Action onPressed)
		{
			var button = SigilButton.Of(icon, tooltip, onPressed, 60, SigilShape.Diamond).Named(name);
			button.Disabled = disabled;
			return button;
		}
	}
}
