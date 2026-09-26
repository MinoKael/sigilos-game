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
	/// O que a runa ganhou desde que a tela abriu fica em verde — subatributo novo, ou "8% → 15% | +7%".
	/// Sair da tela apaga o destaque; o histórico de verdade fica na runa (<see cref="RuneSubstat.Rolls"/>).
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
		private readonly Label _count = new() { ThemeTypeVariation = GameTheme.Number, VerticalAlignment = VerticalAlignment.Center };
		private readonly VBoxContainer _left = new();
		private readonly HBoxContainer _tabs = Layout.Row(8);
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
			var (header, extra) = Layout.Header(T("destination.Runes"), "rune", _currencies, () => BackRequested?.Invoke(_monsterId));
			extra.AddChild(_count);
			page.AddChild(header);

			var body = Layout.Row(12);
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var left = new PanelContainer { CustomMinimumSize = new Vector2(390, 0) };
			_left.AddThemeConstantOverride("separation", 6);
			left.AddChild(_left);
			body.AddChild(left);

			var middle = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var middleColumn = new VBoxContainer();
			middleColumn.AddThemeConstantOverride("separation", 8);
			middleColumn.AddChild(_tabs);
			middleColumn.AddChild(_middle);
			middle.AddChild(middleColumn);
			body.AddChild(middle);

			var right = new PanelContainer { CustomMinimumSize = new Vector2(340, 0) };
			_detail.AddThemeConstantOverride("separation", 8);
			right.AddChild(Layout.Scroll(_detail));
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

			var picker = new ArcCarousel(items, Monster == null ? 0 : monsters.FindIndex(m => m.Id == _monsterId) + 1, 50, 250, 5) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			picker.Selected += index =>
			{
				_monsterId = index == 0 ? null : monsters[index - 1].Id;
				Callable.From(Refresh).CallDeferred();
			};
			_left.AddChild(picker);

			var ring = new SigilRing(300) { Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			var tiles = new List<Control>();
			var equipped = Monster is { } holder ? _player.RunesOn(holder.Id) : Array.Empty<Rune>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(equipped.FirstOrDefault(r => r.Slot == slot), slot, 1.15f);
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
				center = Layout.Medal(Art.Creature(summon.ImageFor(monster.Awakened)), Palette.Of(summon.Element), 96);
			}

			ring.Set(center, tiles);
			_left.AddChild(ring);

			if (Monster is not { } chosen)
				return;

			var chosenSummon = _database.Summon(chosen.SummonId);
			var sheet = SummonStats.For(_database.Roles[chosenSummon.Role], chosenSummon, chosen.Stars, chosen.Level, chosen.Awakened, _player.RunesOn(chosen.Id));
			var summary = new VBoxContainer();
			summary.AddThemeConstantOverride("separation", 4);
			foreach (var set in sheet.Runes.ActiveSets)
			{
				var row = Layout.Row(6);
				row.AddChild(Doodle.Icon(Art.Glyph(RuneSets.For(set.Set).Glyph), 20, Palette.Gold));
				row.AddChild(RichText.Label($"{Texts.Term(set.Set)}: {Texts.Describe(set)}", 320, GameTheme.Faded, 13));
				summary.AddChild(row);
			}

			var table = new StatTable();
			table.Show(sheet);
			summary.AddChild(table);
			_left.AddChild(Layout.Scroll(summary));
		}

		// Abas do meio ------------------------------------------------------------------------------

		private void RefreshTabs()
		{
			Layout.Clear(_tabs);
			var tabs = new SigilTabs(vertical: false, 50);
			tabs.Add(Art.Icon("rune"), T("runes.tab_runes"), RuneInventory.Count(_player).ToString());
			tabs.Add(Art.Icon("grindstone"), T("runes.tab_grindstones"), _player.Tools.Count(t => t.Kind == RuneToolKind.Grindstone).ToString());
			tabs.Add(Art.Icon("gem"), T("runes.tab_gems"), _player.Tools.Count(t => t.Kind == RuneToolKind.Gem).ToString());
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

			var grid = new GridContainer { Columns = 8 };
			grid.AddThemeConstantOverride("h_separation", 8);
			grid.AddThemeConstantOverride("v_separation", 8);
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

			_middle.AddChild(Layout.Scroll(grid));
		}

		/// <summary>A busca: um sigilo por campo (conjunto, espaço, principal, subatributo, estrelas, raridade, melhora, ordem, onde), o de limpar e o de selecionar.</summary>
		private Control FilterBar(int found)
		{
			var flow = Layout.Flow(6);
			// "Todos" em cada campo: o símbolo do campo, apagado; escolhido um valor, acende em ouro.
			ArcItem All(string icon) => new(Art.Icon(icon), T("filter.all"), Palette.TextFaded);
			ArcItem AllLetters(string letters) => new(null, T("filter.all"), Palette.TextFaded, letters);

			flow.AddChild(Picker(T("filter.set"), Enum.GetValues<RuneSet>().Select(s => (new ArcItem(Art.Glyph(RuneSets.For(s).Glyph), Texts.Name(s), Palette.Gold), (int)s)), All("rune"), _filter.Set is { } set ? (int)set : -1,
				value => _filter = _filter with { Set = value < 0 ? null : (RuneSet)value }));
			flow.AddChild(Picker(T("filter.slot"), Enumerable.Range(1, RuneRules.Slots).Select(s => (Letters(s.ToString(), T("filter.slot_n", s)), s)), AllLetters("#"), _filter.Slot ?? -1,
				value =>
				{
					_filter = _filter with { Slot = value < 0 ? null : value };
					_slotFilter = Math.Max(0, value);
				}));
			flow.AddChild(Picker(T("filter.main"), Enum.GetValues<RuneStat>().Select(s => (Letters(Texts.Short(s), Texts.Label(s)), (int)s)), AllLetters("◆"), _filter.Main is { } main ? (int)main : -1,
				value => _filter = _filter with { Main = value < 0 ? null : (RuneStat)value }));
			flow.AddChild(Picker(T("filter.substat"), Enum.GetValues<RuneStat>().Select(s => (Letters(Texts.Short(s), Texts.Label(s)), (int)s)), AllLetters("◇"), _filter.Substats.Count > 0 ? (int)_filter.Substats[0] : -1,
				value => _filter = _filter with { Substats = value < 0 ? Array.Empty<RuneStat>() : new[] { (RuneStat)value } }));
			flow.AddChild(Picker(T("filter.stars"), Enumerable.Range(2, RuneRules.MaxGrade - 1).Select(g => (Letters($"{g}★", T("filter.at_least", Texts.Stars(g))), g)), AllLetters("★"), _filter.MinGrade > 1 ? _filter.MinGrade : -1,
				value => _filter = _filter with { MinGrade = Math.Max(1, value) }));
			flow.AddChild(Picker(T("filter.rarity"), Enum.GetValues<RuneRarity>().Skip(1).Select(r => (new ArcItem(Art.Icon("gem"), T("filter.at_least", Texts.Name(r)), Palette.Of(r)), (int)r)), All("gem"), _filter.MinRarity > RuneRarity.Normal ? (int)_filter.MinRarity : -1,
				value => _filter = _filter with { MinRarity = value < 0 ? RuneRarity.Normal : (RuneRarity)value }));
			flow.AddChild(Picker(T("filter.upgrade"), new[] { 3, 6, 9, 12, 15 }.Select(l => (Letters($"+{l}", T("filter.at_least", $"+{l}")), l)), AllLetters("+"), _filter.MinLevel > 0 ? _filter.MinLevel : -1,
				value => _filter = _filter with { MinLevel = Math.Max(0, value) }));

			var sort = new SigilPicker(T("filter.sort"), Enum.GetValues<RuneSort>().Select(s => (SortItem(s), (int)s)).ToList(), (int)_filter.Sort);
			sort.Changed += value =>
			{
				_filter = _filter with { Sort = (RuneSort)value };
				Callable.From(Refresh).CallDeferred();
			};
			flow.AddChild(sort);

			var place = new SigilPicker(T("filter.where"), Enum.GetValues<RunePlace>().Select(p => (PlaceItem(p), (int)p)).ToList(), (int)_place);
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
			}, 44);
			flow.AddChild(clear);

			flow.AddChild(Layout.Chip("search", found.ToString(), T("runes.found")));
			var select = new SigilButton(Art.Icon("select"), T("runes.select"), 50, SigilShape.Square) { ToggleMode = true, ButtonPressed = _selecting };
			select.Toggled += on =>
			{
				_selecting = on;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			};
			flow.AddChild(select);
			return flow;
		}

		/// <summary>Um campo de busca: a primeira opção é "todos" (-1).</summary>
		private SigilPicker Picker(string title, IEnumerable<(ArcItem Item, int Value)> options, ArcItem all, int current, Action<int> changed)
		{
			var list = new List<(ArcItem Item, int Value)> { (all, -1) };
			list.AddRange(options);
			var picker = new SigilPicker(title, list, current);
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
			var row = Layout.Row(8);
			row.AddChild(SigilButton.Of("copies", T("runes.mark_all"), () =>
			{
				foreach (var rune in visible.Where(r => r.EquippedOn == null))
					_marked.Add(rune.Id);
				Refresh();
			}, 48));
			row.AddChild(SigilButton.Of("cancel", T("runes.unmark"), () =>
			{
				_marked.Clear();
				Refresh();
			}, 48));

			var marked = _player.Runes.Where(r => _marked.Contains(r.Id)).ToList();
			var value = marked.Sum(RuneRules.SellValue);
			var sell = SigilButton.Of("dismantle", T("runes.sell_marked", marked.Count, value), () =>
			{
				var ids = marked.Select(r => r.Id).ToList();
				_marked.Clear();
				SellManyRequested?.Invoke(ids);
			}, 56, SigilShape.Diamond);
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
				var empty = Doodle.Icon(Art.Icon(kind == RuneToolKind.Grindstone ? "grindstone" : "gem"), 96, new Color(Palette.GoldDark, 0.5f));
				empty.TooltipText = T("runes.no_tools");
				empty.MouseFilter = MouseFilterEnum.Stop;
				var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
				center.AddChild(empty);
				_middle.AddChild(center);
				return;
			}

			var list = new VBoxContainer();
			list.AddThemeConstantOverride("separation", 6);
			foreach (var group in groups)
			{
				var row = Layout.Row(10);
				row.AddChild(Doodle.Icon(Art.Icon(kind == RuneToolKind.Grindstone ? "grindstone" : "gem"), 30, Palette.Of(group.Key.Grade)));
				row.AddChild(Doodle.Icon(Art.Glyph(Texts.GlyphOf(group.Key.Stat)), 22, Palette.Gold));
				var name = new Label { Text = Texts.Label(group.Key.Stat), SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = Texts.Name(group.Key), MouseFilter = MouseFilterEnum.Stop };
				name.AddThemeColorOverride("font_color", Palette.Of(group.Key.Grade));
				row.AddChild(name);
				row.AddChild(new Label { Text = Texts.Range(group.Key), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(new Label { Text = $"×{group.Count()}", ThemeTypeVariation = GameTheme.Number, CustomMinimumSize = new Vector2(44, 0), HorizontalAlignment = HorizontalAlignment.Right });
				list.AddChild(row);
			}

			_middle.AddChild(Layout.Scroll(list));
		}

		// Ficha da runa -----------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			var rune = _player.Runes.FirstOrDefault(r => r.Id == _selectedRune);
			if (rune == null)
			{
				var center = new CenterContainer { CustomMinimumSize = new Vector2(0, 300) };
				center.AddChild(Doodle.Icon(Art.Icon("rune"), 110, new Color(Palette.GoldDark, 0.45f)));
				_detail.AddChild(center);
				return;
			}

			var opened = _levelWhenOpened.GetValueOrDefault(rune.Id, rune.Level);
			if (!_levelWhenOpened.ContainsKey(rune.Id))
				_levelWhenOpened[rune.Id] = rune.Level;

			var color = Palette.Of(rune.Rarity);
			var head = Layout.Row(10);
			head.AddChild(new RuneTile(rune, rune.Slot, 1.4f) { MouseFilter = MouseFilterEnum.Ignore });
			var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var title = new Label { Text = Texts.Title(rune), ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			title.AddThemeColorOverride("font_color", color);
			titles.AddChild(title);
			var grade = new Label { Text = $"{Texts.Stars(rune.Grade)}  +{rune.Level}", TooltipText = Texts.Name(rune.Rarity), MouseFilter = MouseFilterEnum.Stop };
			grade.AddThemeColorOverride("font_color", color);
			titles.AddChild(grade);
			head.AddChild(titles);
			_detail.AddChild(head);
			_detail.AddChild(RichText.Label(Texts.Describe(RuneSets.For(rune.Set)), 300, GameTheme.Faded, 13));

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
				var label = new Label { Text = Texts.Format(innate), TooltipText = T("runes.innate"), MouseFilter = MouseFilterEnum.Stop };
				label.AddThemeColorOverride("font_color", Palette.Gold);
				_detail.AddChild(label);
			}

			_detail.AddChild(new HSeparator());
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

			if (rune.EquippedOn is { } owner && owner != _monsterId && _player.Monster(owner) is { } holder)
			{
				var summon = _database.Summon(holder.SummonId);
				var row = Layout.Row(6);
				var who = Layout.Medal(Art.Creature(summon.ImageFor(holder.Awakened)), Palette.Of(summon.Element), 32);
				who.TooltipText = T(holder.Stored ? "runes.equipped_on_vault" : "runes.equipped_on", summon.NameFor(holder.Awakened));
				who.MouseFilter = MouseFilterEnum.Stop;
				row.AddChild(who);
				if (holder.Stored)
					row.AddChild(Doodle.Icon(Art.Icon("chest"), 22, Palette.TextFaded));
				_detail.AddChild(row);
			}

			_detail.AddChild(new HSeparator());
			var actions = Layout.Flow(10);
			var full = RuneInventory.IsFull(_player);
			var fullTip = full ? T("runes.inventory_full", RuneInventory.Capacity) : T("runes.remove");
			if (rune.EquippedOn != null && rune.EquippedOn == _monsterId)
				actions.AddChild(Act("cancel", fullTip, full, () => UnequipRequested?.Invoke(rune.Id)));
			else if (Monster is { } monster)
				actions.AddChild(Act("confirm", T("runes.equip_on", _database.Summon(monster.SummonId).NameFor(monster.Awakened)), false, () => EquipRequested?.Invoke(rune.Id, monster.Id)));
			else if (rune.EquippedOn != null)
				actions.AddChild(Act("cancel", fullTip, full, () => UnequipRequested?.Invoke(rune.Id)));

			if (rune.Level < RuneRules.MaxLevel)
			{
				var next = RuneRules.UpgradeCost(rune);
				var up = Act("essence", T("runes.upgrade_to", rune.Level + 1, next), _player.Essence < next, () => UpgradeRequested?.Invoke(rune.Id, rune.Level + 1));
				up.Badge = $"+{rune.Level + 1}";
				actions.AddChild(up);

				var milestone = RuneRules.NextMilestone(rune.Level);
				if (milestone > rune.Level + 1)
				{
					var total = RuneRules.UpgradeCost(rune, milestone);
					var jump = Act("level_max", T("runes.upgrade_milestone", milestone, total), _player.Essence < total, () => UpgradeRequested?.Invoke(rune.Id, milestone));
					jump.Badge = $"+{milestone}";
					actions.AddChild(jump);
				}
			}

			if (rune.EquippedOn == null)
			{
				var sell = Act("dismantle", T("runes.sell", RuneRules.SellValue(rune)), false, () => SellRequested?.Invoke(rune.Id));
				sell.Badge = RuneRules.SellValue(rune).ToString();
				actions.AddChild(sell);
			}

			_detail.AddChild(actions);
		}

		private void Hint(string text)
		{
			var label = new Label { Text = text };
			label.AddThemeColorOverride("font_color", Palette.Positive);
			label.AddThemeFontSizeOverride("font_size", 13);
			_detail.AddChild(label);
		}

		/// <summary>Um subatributo com os sigilos das pedras que servem nele.</summary>
		private Control SubstatRow(Rune rune, int index)
		{
			var substat = rune.Substats[index];
			var row = Layout.Row(6);
			row.AddChild(Doodle.Icon(Art.Glyph(Texts.GlyphOf(substat.Stat)), 18, Palette.GoldDark.Lightened(0.3f)));
			var label = new Label { Text = Texts.Format(substat), SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
			if (substat.Enchanted)
			{
				label.Text += " ◆";
				label.TooltipText = T("runes.enchanted_tip");
				label.MouseFilter = MouseFilterEnum.Stop;
			}

			row.AddChild(label);

			var grindstones = Usable(t => RuneForge.CanGrind(rune, index, t));
			if (grindstones.Count > 0)
				row.AddChild(ToolMenu("grindstone", T("runes.grind"), grindstones, tool => GrindRequested?.Invoke(rune.Id, index, tool)));

			var gems = Usable(t => RuneForge.CanEnchant(rune, index, t));
			if (gems.Count > 0)
				row.AddChild(ToolMenu("gem", T("runes.enchant"), gems, tool => EnchantRequested?.Invoke(rune.Id, index, tool)));

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

		private static SigilButton Act(string icon, string tooltip, bool disabled, Action onPressed)
		{
			var button = SigilButton.Of(icon, tooltip, onPressed, 60, SigilShape.Diamond);
			button.Disabled = disabled;
			return button;
		}
	}
}
