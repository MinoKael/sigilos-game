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
	/// Runas. À esquerda o monstro escolhido com os 6 espaços em círculo — "o próprio círculo de
	/// conjuração" (GDD, seção 10) — e a ficha dele; no meio, em abas, o inventário de runas (com busca
	/// e seleção em massa para desfazer), as Pedras de Afiar e as Gemas Encantadas; à direita a runa
	/// selecionada com equipar, tirar, melhorar, afiar, encantar e desfazer. Monstros do Baú também
	/// guardam runas: aparecem no seletor e, na busca, em "No Baú".
	///
	/// O que a runa ganhou desde que a tela abriu fica em verde — subatributo novo, ou "8% → 15% | +7%".
	/// Sair da tela apaga o destaque; o histórico de verdade fica na runa (<see cref="RuneSubstat.Rolls"/>).
	/// </summary>
	public partial class RuneScreen : Control
	{
		private const float CircleRadius = 132;
		private static readonly Vector2 CircleCenter = new(180, 180);

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
		private readonly OptionButton _monsterPicker = new() { FitToLongestItem = false, ClipText = true };
		private readonly Control _circle = new() { CustomMinimumSize = new Vector2(360, 360) };
		private readonly VBoxContainer _summary = new();
		private readonly HBoxContainer _tabs = new();
		private readonly VBoxContainer _middle = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
		private readonly VBoxContainer _detail = new();

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
			page.AddChild(Layout.Header(T("runes.title"), _currencies, T("common.back"), () => BackRequested?.Invoke(_monsterId)));

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 12);
			page.AddChild(body);

			var left = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
			var leftColumn = new VBoxContainer();
			_monsterPicker.ItemSelected += index =>
			{
				_monsterId = _monsterPicker.GetItemId((int)index) is var id && id >= 0 ? id : null;
				Refresh();
			};
			leftColumn.AddChild(_monsterPicker);
			leftColumn.AddChild(_circle);
			var summaryScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			summaryScroll.AddChild(_summary);
			leftColumn.AddChild(summaryScroll);
			left.AddChild(leftColumn);
			body.AddChild(left);

			var middle = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var middleColumn = new VBoxContainer();
			_tabs.AddThemeConstantOverride("separation", 4);
			middleColumn.AddChild(_tabs);
			middleColumn.AddChild(_middle);
			middle.AddChild(middleColumn);
			body.AddChild(middle);

			var right = new PanelContainer { CustomMinimumSize = new Vector2(350, 0) };
			var detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			_detail.AddThemeConstantOverride("separation", 6);
			detailScroll.AddChild(_detail);
			right.AddChild(detailScroll);
			body.AddChild(right);

			Refresh();
		}

		public void Refresh()
		{
			if (_selectedRune is { } id && _player.Runes.All(r => r.Id != id))
				_selectedRune = null;
			_marked.RemoveWhere(runeId => _player.Runes.All(r => r.Id != runeId));

			_currencies.Refresh(_player);
			RefreshPicker();
			RefreshCircle();
			RefreshSummary();
			RefreshTabs();
			RefreshMiddle();
			RefreshDetail();
		}

		// Monstro e círculo -------------------------------------------------------------------------

		private OwnedSummon? Monster => _monsterId is { } id ? _player.Monster(id) : null;

		private void RefreshPicker()
		{
			_monsterPicker.Clear();
			_monsterPicker.AddItem(T("runes.no_monster"), -1);
			var monsters = _player.Monsters.Where(m => _database.HasSummon(m.SummonId)).OrderBy(m => m.Stored).ThenByDescending(m => m.Level);
			foreach (var monster in monsters)
			{
				var summon = _database.Summon(monster.SummonId);
				_monsterPicker.AddItem(T(monster.Stored ? "runes.monster_item_vault" : "runes.monster_item", summon.NameFor(monster.Awakened), monster.Level), monster.Id);
				if (monster.Id == _monsterId)
					_monsterPicker.Select(_monsterPicker.ItemCount - 1);
			}

			if (Monster == null)
				_monsterPicker.Select(0);
		}

		private void RefreshCircle()
		{
			Layout.Clear(_circle);
			var ring = new Doodle(Art.Icon("summon"), Palette.PanelLight) { Size = new Vector2(344, 344), Position = CircleCenter - new Vector2(172, 172) };
			_circle.AddChild(ring);

			if (Monster is not { } monster)
			{
				var hint = Layout.Text(T("runes.choose_monster"), GameTheme.Faded, 240);
				hint.HorizontalAlignment = HorizontalAlignment.Center;
				hint.Position = CircleCenter - new Vector2(120, 30);
				_circle.AddChild(hint);
				return;
			}

			var summon = _database.Summon(monster.SummonId);
			var portrait = new Doodle(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element)) { Size = new Vector2(130, 130), Position = CircleCenter - new Vector2(65, 65) };
			_circle.AddChild(portrait);

			var equipped = _player.RunesOn(monster.Id);
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var angle = Mathf.DegToRad(-90 + 60 * (slot - 1));
				var tile = new RuneTile(equipped.FirstOrDefault(r => r.Slot == slot), slot);
				tile.Position = CircleCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CircleRadius - RuneTile.TileSize / 2;
				tile.SetSelected(tile.Rune != null ? tile.Rune.Id == _selectedRune : slot == _slotFilter);
				tile.Pressed += t =>
				{
					_slotFilter = t.Slot;
					_filter = _filter with { Slot = t.Slot };
					_selectedRune = t.Rune?.Id;
					_tab = 0;
					Refresh();
				};
				_circle.AddChild(tile);
			}
		}

		private void RefreshSummary()
		{
			Layout.Clear(_summary);
			if (Monster is not { } monster)
				return;

			var summon = _database.Summon(monster.SummonId);
			var sheet = SummonStats.For(_database.Roles[summon.Role], summon, monster.Level, monster.Echoes, monster.Awakened, _player.RunesOn(monster.Id));
			_summary.AddChild(new Label { Text = T("runes.active_sets"), ThemeTypeVariation = GameTheme.Heading });
			_summary.AddChild(RichText.Label(sheet.Runes.ActiveSets.Count == 0
				? T("runes.no_sets")
				: string.Join("\n", sheet.Runes.ActiveSets.Select(s => $"{Texts.Term(s.Set)}: {Texts.Describe(s)}")), 340, GameTheme.Faded));
			var table = new StatTable();
			table.Show(sheet);
			_summary.AddChild(table);
		}

		// Abas do meio ------------------------------------------------------------------------------

		private void RefreshTabs()
		{
			Layout.Clear(_tabs);
			var grindstones = _player.Tools.Count(t => t.Kind == RuneToolKind.Grindstone);
			var gems = _player.Tools.Count(t => t.Kind == RuneToolKind.Gem);
			TabButton(0, T("runes.tab_runes", RuneInventory.Count(_player), RuneInventory.Capacity), "rune");
			TabButton(1, T("runes.tab_grindstones", grindstones), "grindstone");
			TabButton(2, T("runes.tab_gems", gems), "gem");
		}

		private void TabButton(int index, string text, string icon)
		{
			var button = Layout.IconButton(text, Art.Icon(icon), 22, _tab == index ? Palette.Background : Palette.Gold);
			button.ToggleMode = true;
			button.ButtonPressed = _tab == index;
			button.CustomMinimumSize = new Vector2(0, 38);
			button.Pressed += () =>
			{
				_tab = index;
				Refresh();
			};
			_tabs.AddChild(button);
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
			_middle.AddChild(FilterBar());

			var runes = _filter.Apply(_player.Runes.Where(InPlace)).ToList();
			var bar = new HBoxContainer();
			bar.AddChild(new Label { Text = T("runes.found", runes.Count), SizeFlagsHorizontal = SizeFlags.ExpandFill });
			var select = new CheckButton { Text = T("runes.select"), ButtonPressed = _selecting, TooltipText = T("runes.select_tip") };
			select.Toggled += on =>
			{
				_selecting = on;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			};
			bar.AddChild(select);
			_middle.AddChild(bar);

			if (_selecting)
				_middle.AddChild(SelectionBar(runes));

			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			var grid = new GridContainer { Columns = 5 };
			grid.AddThemeConstantOverride("h_separation", 6);
			grid.AddThemeConstantOverride("v_separation", 6);
			foreach (var rune in runes)
			{
				var tile = new RuneTile(rune, rune.Slot);
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

			scroll.AddChild(grid);
			_middle.AddChild(scroll);
		}

		/// <summary>Os campos de busca: conjunto, espaço, principal, subatributo, estrelas, raridade, melhora e ordem.</summary>
		private Control FilterBar()
		{
			var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			grid.AddThemeConstantOverride("h_separation", 4);
			grid.AddThemeConstantOverride("v_separation", 4);

			grid.AddChild(Picker(T("filter.set"), Enum.GetValues<RuneSet>().Select(s => (Texts.Name(s), (int)s)), _filter.Set is { } set ? (int)set : -1,
				value => _filter = _filter with { Set = value < 0 ? null : (RuneSet)value }));
			grid.AddChild(Picker(T("filter.slot"), Enumerable.Range(1, RuneRules.Slots).Select(s => (s.ToString(), s)), _filter.Slot ?? -1,
				value =>
				{
					_filter = _filter with { Slot = value < 0 ? null : value };
					_slotFilter = Math.Max(0, value);
				}));
			grid.AddChild(Picker(T("filter.main"), Enum.GetValues<RuneStat>().Select(s => (Texts.Label(s), (int)s)), _filter.Main is { } main ? (int)main : -1,
				value => _filter = _filter with { Main = value < 0 ? null : (RuneStat)value }));
			grid.AddChild(Picker(T("filter.substat"), Enum.GetValues<RuneStat>().Select(s => (Texts.Label(s), (int)s)), _filter.Substats.Count > 0 ? (int)_filter.Substats[0] : -1,
				value => _filter = _filter with { Substats = value < 0 ? Array.Empty<RuneStat>() : new[] { (RuneStat)value } }));
			grid.AddChild(Picker(T("filter.stars"), Enumerable.Range(2, RuneRules.MaxGrade - 1).Select(g => (T("filter.at_least", Texts.Stars(g)), g)), _filter.MinGrade > 1 ? _filter.MinGrade : -1,
				value => _filter = _filter with { MinGrade = Math.Max(1, value) }));
			grid.AddChild(Picker(T("filter.rarity"), Enum.GetValues<RuneRarity>().Skip(1).Select(r => (T("filter.at_least", Texts.Name(r)), (int)r)), _filter.MinRarity > RuneRarity.Normal ? (int)_filter.MinRarity : -1,
				value => _filter = _filter with { MinRarity = value < 0 ? RuneRarity.Normal : (RuneRarity)value }));
			grid.AddChild(Picker(T("filter.upgrade"), new[] { 3, 6, 9, 12, 15 }.Select(l => (T("filter.at_least", $"+{l}"), l)), _filter.MinLevel > 0 ? _filter.MinLevel : -1,
				value => _filter = _filter with { MinLevel = Math.Max(0, value) }));

			var sort = new OptionButton { TooltipText = T("filter.order_tip"), CustomMinimumSize = new Vector2(112, 0), ClipText = true, FitToLongestItem = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			sort.AddThemeFontSizeOverride("font_size", 13);
			foreach (var option in Enum.GetValues<RuneSort>())
				sort.AddItem(T("filter.order_item", Texts.Name(option)), (int)option);
			sort.Select((int)_filter.Sort);
			sort.ItemSelected += index =>
			{
				_filter = _filter with { Sort = (RuneSort)sort.GetItemId((int)index) };
				Callable.From(Refresh).CallDeferred();
			};
			grid.AddChild(sort);

			var row = new VBoxContainer();
			row.AddChild(grid);
			var options = new HBoxContainer();
			var place = Picker(T("filter.where"), Enum.GetValues<RunePlace>().Skip(1).Select(p => (T($"filter.place.{p}"), (int)p)), _place == RunePlace.Inventory ? -1 : (int)_place,
				value => _place = value < 0 ? RunePlace.Inventory : (RunePlace)value);
			place.SetItemText(0, T("filter.place.Inventory"));
			place.CustomMinimumSize = new Vector2(180, 0);
			place.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			options.AddChild(place);
			var clear = new Button { Text = T("filter.clear") };
			clear.Pressed += () =>
			{
				_filter = new RuneFilter();
				_slotFilter = 0;
				Refresh();
			};
			options.AddChild(clear);
			row.AddChild(options);
			return row;
		}

		/// <summary>Um campo de busca: "Todos" (-1) ou um valor.</summary>
		private OptionButton Picker(string title, IEnumerable<(string Text, int Value)> options, int current, Action<int> changed)
		{
			var picker = new OptionButton { TooltipText = title, CustomMinimumSize = new Vector2(112, 0), ClipText = true, FitToLongestItem = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			picker.AddThemeFontSizeOverride("font_size", 13);
			picker.AddItem(T("filter.all", title), -1);
			foreach (var (text, value) in options)
			{
				picker.AddItem(text, value);
				if (value == current)
					picker.Select(picker.ItemCount - 1);
			}

			picker.ItemSelected += index =>
			{
				changed(picker.GetItemId((int)index));
				Callable.From(Refresh).CallDeferred();
			};
			return picker;
		}

		private Control SelectionBar(IReadOnlyList<Rune> visible)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			var all = new Button { Text = T("runes.mark_all") };
			all.Pressed += () =>
			{
				foreach (var rune in visible.Where(r => r.EquippedOn == null))
					_marked.Add(rune.Id);
				Refresh();
			};
			row.AddChild(all);
			var none = new Button { Text = T("runes.unmark") };
			none.Pressed += () =>
			{
				_marked.Clear();
				Refresh();
			};
			row.AddChild(none);

			var marked = _player.Runes.Where(r => _marked.Contains(r.Id)).ToList();
			var sell = new Button { Text = T("runes.sell_marked", marked.Count, marked.Sum(RuneRules.SellValue)), Disabled = marked.Count == 0 };
			sell.Pressed += () =>
			{
				var ids = marked.Select(r => r.Id).ToList();
				_marked.Clear();
				SellManyRequested?.Invoke(ids);
			};
			row.AddChild(sell);
			return row;
		}

		private void ToolList(RuneToolKind kind)
		{
			_middle.AddChild(Layout.Text(T(kind == RuneToolKind.Grindstone ? "runes.grindstones_info" : "runes.gems_info", RuneForge.EnchantLevel), GameTheme.Faded));
			var groups = _player.Tools.Where(t => t.Kind == kind).GroupBy(t => t).OrderByDescending(g => g.Key.Grade).ThenBy(g => g.Key.Stat).ToList();
			if (groups.Count == 0)
			{
				_middle.AddChild(Layout.Text(T("runes.no_tools"), GameTheme.Faded));
				return;
			}

			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			foreach (var group in groups)
			{
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 8);
				row.AddChild(Doodle.Icon(Art.Icon(kind == RuneToolKind.Grindstone ? "grindstone" : "gem"), 28, Palette.Of(group.Key.Grade)));
				var name = new Label { Text = Texts.Name(group.Key), SizeFlagsHorizontal = SizeFlags.ExpandFill };
				name.AddThemeColorOverride("font_color", Palette.Of(group.Key.Grade));
				row.AddChild(name);
				row.AddChild(new Label { Text = Texts.Range(group.Key), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(new Label { Text = $"×{group.Count()}", CustomMinimumSize = new Vector2(40, 0), HorizontalAlignment = HorizontalAlignment.Right });
				list.AddChild(row);
			}

			scroll.AddChild(list);
			_middle.AddChild(scroll);
		}

		// Ficha da runa -----------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			var rune = _player.Runes.FirstOrDefault(r => r.Id == _selectedRune);
			if (rune == null)
			{
				_detail.AddChild(new Label { Text = T("runes.rune"), ThemeTypeVariation = GameTheme.Heading });
				_detail.AddChild(Layout.Text(T("runes.info"), GameTheme.Faded, 320));
				return;
			}

			var opened = _levelWhenOpened.GetValueOrDefault(rune.Id, rune.Level);
			if (!_levelWhenOpened.ContainsKey(rune.Id))
				_levelWhenOpened[rune.Id] = rune.Level;

			var color = Palette.Of(rune.Rarity);
			var title = new Label { Text = $"{Texts.Title(rune)}  +{rune.Level}", ThemeTypeVariation = GameTheme.Heading };
			title.AddThemeColorOverride("font_color", color);
			_detail.AddChild(title);
			var grade = new Label { Text = $"{Texts.Name(rune.Rarity)}  {Texts.Stars(rune.Grade)}" };
			grade.AddThemeColorOverride("font_color", color);
			_detail.AddChild(grade);
			_detail.AddChild(RichText.Label(T("runes.set", Texts.Term(rune.Set), Texts.Describe(RuneSets.For(rune.Set))), 320, GameTheme.Faded));

			var main = new Label { Text = Texts.Format(rune.Main, rune.MainValue) };
			main.AddThemeFontOverride("font", GameTheme.Serif);
			main.AddThemeFontSizeOverride("font_size", 20);
			_detail.AddChild(main);
			if (rune.Level > opened)
			{
				var before = RuneRules.MainValue(rune.Main, rune.Grade, opened);
				Hint(T("runes.hint_changed", Texts.Amount(rune.Main, before), Texts.Amount(rune.Main, rune.MainValue), Texts.Amount(rune.Main, rune.MainValue - before)));
			}

			if (rune.Innate is { } innate)
			{
				var label = new Label { Text = T("runes.innate", Texts.Format(innate)), TooltipText = T("runes.innate_tip"), MouseFilter = MouseFilterEnum.Stop };
				label.AddThemeColorOverride("font_color", Palette.Gold);
				_detail.AddChild(label);
			}

			for (var i = 0; i < rune.Substats.Count; i++)
			{
				_detail.AddChild(SubstatRow(rune, i));
				var substat = rune.Substats[i];
				var gained = substat.Rolls.Where(r => r.Level > opened).Sum(r => r.Amount);
				if (substat.Rolls.Count > 0 && substat.Rolls[0].Level > opened)
					Hint(T("runes.hint_new", Texts.Format(substat.Stat, substat.Value)));
				else if (gained > 0)
					Hint(T("runes.hint_changed", Texts.Amount(substat.Stat, substat.Value - gained), Texts.Amount(substat.Stat, substat.Value), Texts.Amount(substat.Stat, gained)));
			}

			_detail.AddChild(Layout.Text(rune.Level < RuneRules.MaxLevel ? T("runes.milestones") : T("runes.maxed"), GameTheme.Faded, 320));

			if (rune.EquippedOn is { } owner && owner != _monsterId && _player.Monster(owner) is { } holder)
				_detail.AddChild(Layout.Text(T(holder.Stored ? "runes.equipped_on_vault" : "runes.equipped_on", _database.Summon(holder.SummonId).NameFor(holder.Awakened)), GameTheme.Faded, 320));

			var actions = new HFlowContainer();
			actions.AddThemeConstantOverride("h_separation", 6);
			actions.AddThemeConstantOverride("v_separation", 6);
			var full = RuneInventory.IsFull(_player);
			var fullTip = full ? T("runes.inventory_full", RuneInventory.Capacity) : "";
			if (rune.EquippedOn != null && rune.EquippedOn == _monsterId)
				Add(actions, T("runes.remove"), full, () => UnequipRequested?.Invoke(rune.Id), fullTip);
			else if (Monster is { } monster)
				Add(actions, T("runes.equip_on", _database.Summon(monster.SummonId).NameFor(monster.Awakened)), false, () => EquipRequested?.Invoke(rune.Id, monster.Id));
			else if (rune.EquippedOn != null)
				Add(actions, T("runes.remove"), full, () => UnequipRequested?.Invoke(rune.Id), fullTip);

			if (rune.Level < RuneRules.MaxLevel)
			{
				var next = RuneRules.UpgradeCost(rune);
				Add(actions, T("runes.upgrade_to", rune.Level + 1, next), _player.Essence < next, () => UpgradeRequested?.Invoke(rune.Id, rune.Level + 1));

				var milestone = RuneRules.NextMilestone(rune.Level);
				if (milestone > rune.Level + 1)
				{
					var total = RuneRules.UpgradeCost(rune, milestone);
					Add(actions, T("runes.upgrade_milestone", milestone, total), _player.Essence < total, () => UpgradeRequested?.Invoke(rune.Id, milestone));
				}
			}

			if (rune.EquippedOn == null)
				Add(actions, T("runes.sell", RuneRules.SellValue(rune)), false, () => SellRequested?.Invoke(rune.Id));
			_detail.AddChild(actions);
		}

		private void Hint(string text)
		{
			var label = new Label { Text = text };
			label.AddThemeColorOverride("font_color", Palette.Positive);
			label.AddThemeFontSizeOverride("font_size", 13);
			_detail.AddChild(label);
		}

		/// <summary>Um subatributo com os botões das pedras que servem nele.</summary>
		private Control SubstatRow(Rune rune, int index)
		{
			var substat = rune.Substats[index];
			var row = new HBoxContainer();
			var label = new Label { Text = Texts.Format(substat), SizeFlagsHorizontal = SizeFlags.ExpandFill };
			if (substat.Enchanted)
			{
				label.Text += " ◆";
				label.TooltipText = T("runes.enchanted_tip");
				label.MouseFilter = MouseFilterEnum.Stop;
			}

			row.AddChild(label);

			var grindstones = Usable(t => RuneForge.CanGrind(rune, index, t));
			if (grindstones.Count > 0)
				row.AddChild(ToolMenu(T("runes.grind"), grindstones, tool => GrindRequested?.Invoke(rune.Id, index, tool)));

			var gems = Usable(t => RuneForge.CanEnchant(rune, index, t));
			if (gems.Count > 0)
				row.AddChild(ToolMenu(T("runes.enchant"), gems, tool => EnchantRequested?.Invoke(rune.Id, index, tool)));

			return row;
		}

		/// <summary>Pedras diferentes que servem, uma de cada.</summary>
		private List<RuneTool> Usable(Func<RuneTool, bool> fits) =>
			_player.Tools.Distinct().Where(fits).OrderByDescending(t => t.Grade).ToList();

		private static MenuButton ToolMenu(string text, IReadOnlyList<RuneTool> tools, Action<RuneTool> chosen)
		{
			var menu = new MenuButton { Text = text, Flat = false };
			menu.AddThemeFontSizeOverride("font_size", 13);
			var popup = menu.GetPopup();
			for (var i = 0; i < tools.Count; i++)
				popup.AddItem($"{Texts.Name(tools[i])} ({Texts.Range(tools[i])})", i);
			popup.IdPressed += id => chosen(tools[(int)id]);
			return menu;
		}

		/// <summary>Onde a runa está, para a busca.</summary>
		private bool InPlace(Rune rune) => _place switch
		{
			RunePlace.Inventory => rune.EquippedOn == null,
			RunePlace.Collection => _player.Monster(rune.EquippedOn ?? -1) is { Stored: false },
			RunePlace.Storage => _player.Monster(rune.EquippedOn ?? -1) is { Stored: true },
			_ => true,
		};

		private static void Add(HFlowContainer flow, string text, bool disabled, Action onPressed, string tooltip = "")
		{
			var button = new Button { Text = text, Disabled = disabled, TooltipText = tooltip };
			button.Pressed += onPressed;
			flow.AddChild(button);
		}
	}
}
