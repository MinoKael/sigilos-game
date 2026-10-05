using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Runas, em três colunas:
	///
	/// - À esquerda, o monstro que recebe as runas (Escolher monstro abre a grade de todos), o círculo
	///   de conjuração com os 6 espaços dele (tocar num espaço filtra a lista por ele), os conjuntos
	///   ativos e a ficha de atributos.
	/// - No meio, pelas abas escritas, o inventário de runas (com os botões Filtros, Ordenar e Onde, e
	///   Vender várias para marcar e vender de uma vez), as Pedras de Afiar e as Gemas Encantadas.
	/// - À direita, a runa escolhida, com os botões presos embaixo: Equipar, Remover, Melhorar, Vender,
	///   cada um dizendo o custo; e, na linha de cada subatributo, Afiar e Encantar quando alguma pedra
	///   serve.
	///
	/// O que a runa ganhou desde que a tela abriu aparece em verde ao lado do valor. A Batalha automática
	/// segue rodando enquanto esta tela está aberta; o aviso no alto continua contando.
	/// </summary>
	public partial class RuneScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private int? _monsterId;
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
		private readonly VBoxContainer _left = new() { Name = "Column" };
		private readonly HBoxContainer _tabs = Layout.Row(8).Named("TabRow");
		private readonly VBoxContainer _middle = new() { Name = "List", SizeFlagsVertical = SizeFlags.ExpandFill };

		/// <summary>A rolagem da lista do meio e de que aba ela é: a lista é remontada a cada toque, e a posição passa para a nova.</summary>
		private ScrollContainer? _listScroll;
		private int _listTab;
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
        private readonly GridContainer _actions = new()
        {
            Name = "Actions",
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };


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

		/// <summary>Bloquear ou desbloquear a runa (bloqueada não se vende).</summary>
		public event Action<int>? LockRequested;

		/// <summary>Gastar uma Gema de Reavaliação: a runa volta ao estado em que caiu.</summary>
		public event Action<int>? ReappraiseRequested;
		public event Action<int?>? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Runes"), _currencies, () => BackRequested?.Invoke(_monsterId)).Header);

			var body = Layout.Row(12).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var left = new PanelContainer { Name = "Left", CustomMinimumSize = new Vector2(310, 0) };
			_left.AddThemeConstantOverride("separation", 8);
			left.AddChild(Layout.Scroll(_left));
			body.AddChild(left);

			var middle = new PanelContainer { Name = "Middle", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var middleColumn = new VBoxContainer { Name = "Column" };
			middleColumn.AddThemeConstantOverride("separation", 8);
			middleColumn.AddChild(_tabs);
			middleColumn.AddChild(_middle);
			middle.AddChild(middleColumn);
			body.AddChild(middle);

			var right = new PanelContainer { Name = "Right", CustomMinimumSize = new Vector2(380, 0) };
			var rightColumn = new VBoxContainer { Name = "Column" };
			rightColumn.AddThemeConstantOverride("separation", 8);
			_detail.AddThemeConstantOverride("separation", 8);
			rightColumn.AddChild(Layout.Scroll(_detail));
			ConfigureGrid(_actions, 10);
			rightColumn.AddChild(_actions);
			right.AddChild(rightColumn);
			body.AddChild(right);

			Refresh();
		}

		public void Refresh()
		{
			if (_selectedRune is { } id && _player.Runes.All(r => r.Id != id))
				_selectedRune = null;
			_marked.RemoveWhere(runeId => _player.Runes.FirstOrDefault(r => r.Id == runeId) is not { Locked: false });

			_currencies.Refresh(_player);
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
			var head = Layout.Row(10).Named("Monster");
			if (Monster is { } monster)
			{
				var summon = _database.Summon(monster.SummonId);
				var portrait = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(64, 64), MouseFilter = MouseFilterEnum.Stop };
				portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 2, 32, 3));
				portrait.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: monster.Awakened ? summon.Element : null));
				Press.On(portrait, null, () => MonsterSummary.Open(portrait, summon, monster));
				head.AddChild(portrait);
				var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
				info.AddChild(new Label { Name = "Name", Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading });
				info.AddChild(new Label { Name = "Level", Text = T("common.stars_level", Texts.Stars(monster.Stars), monster.Level) + (monster.Stored ? " · " + T("monsters.vault") : ""), ThemeTypeVariation = GameTheme.Faded });
				head.AddChild(info);
			}
			else
			{
				head.AddChild(Layout.Text(T("runes.no_monster_text"), GameTheme.Faded, 320).Named("None"));
			}

			_left.AddChild(head);
			_left.AddChild(GameButton.Of(Monster == null ? T("runes.choose_monster") : T("runes.change_monster"), () =>
				MonsterPicker.Open(this, _database, _player, _monsterId, true, id =>
				{
					_monsterId = id;
					Refresh();
				}), Monster == null ? ButtonKind.Primary : ButtonKind.Secondary, "monster", 48).Named("Choose"));

			var ring = new SigilRing(250) { Name = "Ring", Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			var tiles = new List<Control>();
			var equipped = Monster is { } holder ? _player.RunesOn(holder.Id) : Array.Empty<Rune>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(equipped.FirstOrDefault(r => r.Slot == slot), slot, 1.1f) { Name = $"Slot{slot}" };
				tile.SetSelected(tile.Rune != null ? tile.Rune.Id == _selectedRune : _filter.Slot == slot);
				tile.Pressed += t =>
				{
					_filter = _filter with { Slot = t.Slot };
					_selectedRune = t.Rune?.Id;
					_tab = 0;
					Refresh();
				};
				tiles.Add(tile);
			}

			ring.Set(null, tiles);
			_left.AddChild(ring);
			_left.AddChild(Layout.Text(T("runes.ring_hint"), GameTheme.Faded, 320).Named("RingHint"));

			if (Monster is not { } chosen)
				return;

			var chosenSummon = _database.Summon(chosen.SummonId);
			var sheet = SummonStats.For(chosenSummon, chosen.Stars, chosen.Level, chosen.Awakened, _player.RunesOn(chosen.Id));
			if (sheet.Runes.ActiveSets.Count > 0)
			{
				var sets = Layout.Flow(6).Named("Sets");
				// Conjunto de 2 peças pode estar ativo mais de uma vez: a posição na fileira deixa o nome único.
				for (var i = 0; i < sheet.Runes.ActiveSets.Count; i++)
				{
					var set = sheet.Runes.ActiveSets[i];
					sets.AddChild(Layout.Labeled(RuneSets.For(set.Set).Glyph, "", Texts.Name(set.Set)).Named($"{set.Set}{i + 1}"));
				}

				_left.AddChild(sets);
			}

			var table = new StatTable { Name = "Stats" };
			table.Show(sheet);
			_left.AddChild(table);
		}

		// Abas do meio ------------------------------------------------------------------------------

		private void RefreshTabs()
		{
			Layout.Clear(_tabs);
			var tabs = new TextTabs(compact: true) { Name = "Tabs" };
			tabs.Add(T("runes.tab_runes"), $"{RuneInventory.Count(_player)}/{RuneInventory.Capacity}").Name = "Runes";
			tabs.Add(T("runes.tab_grindstones"), _player.Tools.Count(t => t.Kind == RuneToolKind.Grindstone).ToString()).Name = "Grindstones";
			tabs.Add(T("runes.tab_gems"), _player.Tools.Count(t => t.Kind == RuneToolKind.Gem).ToString()).Name = "Gems";
			tabs.Select(_tab);
			tabs.Changed += index =>
			{
				_tab = index;
				Callable.From(Refresh).CallDeferred();
			};
			_tabs.AddChild(tabs);

			// Quantas runas a busca achou, à direita das abas: a barra de ferramentas fica só com botões.
			if (_tab == 0)
			{
				var found = _filter.Apply(_player.Runes.Where(InPlace)).Count();
				_tabs.AddChild(new Label { Name = "Found", Text = T("runes.found", found), ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			}
		}

		private void RefreshMiddle()
		{
			var keep = _listScroll != null && IsInstanceValid(_listScroll) && _listTab == _tab ? _listScroll.ScrollVertical : 0;
			Layout.Clear(_middle);
			_listScroll = null;
			_listTab = _tab;
			if (_tab == 0)
				RuneList();
			else
				ToolList(_tab == 1 ? RuneToolKind.Grindstone : RuneToolKind.Gem);
			if (_listScroll != null)
				Layout.KeepScroll(_listScroll, keep);
		}

		private void RuneList()
		{
			var runes = _filter.Apply(_player.Runes.Where(InPlace)).ToList();
			_middle.AddChild(Toolbar());

			if (_selecting)
				_middle.AddChild(SelectionBar(runes));

			var grid = new TileGrid(8) { Name = "Grid" };
			foreach (var rune in runes)
			{
				var tile = new RuneTile(rune, rune.Slot) { Name = $"Rune{rune.Id}" };
				tile.SetSelected(rune.Id == _selectedRune);
				tile.SetMarked(_marked.Contains(rune.Id));
				if (rune.EquippedOn is { } owner && _player.Monster(owner) is { } holder)
				{
					var summon = _database.Summon(holder.SummonId);
					tile.SetOwner(Art.Creature(summon.Image), Palette.Of(summon.Element), holder.Stored);
				}

				tile.Pressed += t =>
				{
					var clicked = t.Rune!;
					if (_selecting && clicked.EquippedOn == null && !clicked.Locked)
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

			if (runes.Count == 0)
				_middle.AddChild(Layout.Text(T("runes.none_found"), GameTheme.Faded, 400).Named("Empty"));
			_listScroll = Layout.Scroll(grid);
			_middle.AddChild(_listScroll);
		}

        /// <summary>
        /// Os botões do inventário numa grade de 2: Filtros (com quantos ligados) e Ordenar; Onde e Vender
        /// várias. Duas colunas para "Ordem: estrelas" caber numa linha; quantas runas a busca achou fica na
        /// fileira das abas.
        /// </summary>
        private Control Toolbar()
        {
            var grid = new GridContainer() { Name = "Toolbar", SizeFlagsHorizontal = SizeFlags.ExpandFill, Columns = 2 };
            ConfigureGrid(grid, 8);
            var active = ActiveFilters();
            var filters = GameButton.Of(active == 0 ? T("filter.button") : T("filter.button_active", active), OpenFilters, active == 0 ? ButtonKind.Secondary : ButtonKind.Primary, "search", 44).Named("Filters");
            grid.AddChild(filters);

            var sort = new ChoiceButton(T("filter.sort"), Enum.GetValues<RuneSort>().Select(s => (new Choice(Texts.Name(s)), (int)s)).ToList(), (int)_filter.Sort, 44) { Name = "Sort" };
            sort.Changed += value =>
            {
                _filter = _filter with { Sort = (RuneSort)value };
                Callable.From(Refresh).CallDeferred();
            };
            grid.AddChild(sort);

            var place = new ChoiceButton(T("filter.where"), Enum.GetValues<RunePlace>().Select(p => (new Choice(T($"filter.place.{p}")), (int)p)).ToList(), (int)_place, 44) { Name = "Place" };
            place.Changed += value =>
            {
                _place = (RunePlace)value;
                Callable.From(Refresh).CallDeferred();
            };
            grid.AddChild(place);

            grid.AddChild(GameButton.Of(_selecting ? T("runes.select_done") : T("runes.select"), () =>
            {
                _selecting = !_selecting;
                _marked.Clear();
                Callable.From(Refresh).CallDeferred();
            }, _selecting ? ButtonKind.Primary : ButtonKind.Secondary, "select", 44).Named("Select"));
            return grid;
        }

        private int ActiveFilters() =>
			(_filter.Set != null ? 1 : 0) + (_filter.Slot != null ? 1 : 0) + (_filter.Main != null ? 1 : 0) + (_filter.Substats.Count > 0 ? 1 : 0)
			+ (_filter.MinGrade > 1 ? 1 : 0) + (_filter.MinRarity > RuneRarity.Normal ? 1 : 0) + (_filter.MinLevel > 0 ? 1 : 0);

		/// <summary>A janela dos filtros (<see cref="FilterDialog"/>): um campo por linha. Muda na hora.</summary>
		private void OpenFilters() => FilterDialog.Open(this, T("filter.title"), dialog =>
		{
			dialog.Field("Set", T("filter.set"), Enum.GetValues<RuneSet>().Select(s => (new Choice(Texts.Name(s), Rune: RuneSets.For(s).Glyph), (int)s)), _filter.Set is { } set ? (int)set : FilterDialog.All,
				value => _filter = _filter with { Set = value < 0 ? null : (RuneSet)value });
			dialog.Field("Slot", T("filter.slot"), Enumerable.Range(1, RuneRules.Slots).Select(s => (new Choice(T("filter.slot_n", s)), s)), _filter.Slot ?? FilterDialog.All,
				value => _filter = _filter with { Slot = value < 0 ? null : value });
			dialog.Field("Main", T("filter.main"), Enum.GetValues<RuneStat>().Select(s => (new Choice(Texts.Label(s), Rune: Texts.GlyphOf(s)), (int)s)), _filter.Main is { } main ? (int)main : FilterDialog.All,
				value => _filter = _filter with { Main = value < 0 ? null : (RuneStat)value });
			dialog.Field("Substat", T("filter.substat"), Enum.GetValues<RuneStat>().Select(s => (new Choice(Texts.Label(s), Rune: Texts.GlyphOf(s)), (int)s)), _filter.Substats.Count > 0 ? (int)_filter.Substats[0] : FilterDialog.All,
				value => _filter = _filter with { Substats = value < 0 ? Array.Empty<RuneStat>() : new[] { (RuneStat)value } });
			dialog.Field("Stars", T("filter.stars"), Enumerable.Range(2, RuneRules.MaxGrade - 1).Select(g => (new Choice(T("filter.at_least", Texts.Stars(g))), g)), _filter.MinGrade > 1 ? _filter.MinGrade : FilterDialog.All,
				value => _filter = _filter with { MinGrade = Math.Max(1, value) });
			dialog.Field("Rarity", T("filter.rarity"), Enum.GetValues<RuneRarity>().Skip(1).Select(r => (new Choice(T("filter.at_least", Texts.Name(r)), Art.Icon("gem"), Palette.Of(r)), (int)r)), _filter.MinRarity > RuneRarity.Normal ? (int)_filter.MinRarity : FilterDialog.All,
				value => _filter = _filter with { MinRarity = value < 0 ? RuneRarity.Normal : (RuneRarity)value });
			dialog.Field("Upgrade", T("filter.upgrade"), new[] { 3, 6, 9, 12, 15 }.Select(l => (new Choice(T("filter.at_least", $"+{l}")), l)), _filter.MinLevel > 0 ? _filter.MinLevel : FilterDialog.All,
				value => _filter = _filter with { MinLevel = Math.Max(0, value) });
		}, Refresh, () => _filter = new RuneFilter { Sort = _filter.Sort });

		/// <summary>A faixa da seleção: a explicação numa linha só dela (quebra em vez de alargar a tela) e os botões embaixo.</summary>
		private Control SelectionBar(IReadOnlyList<Rune> visible)
		{
			var bar = new VBoxContainer { Name = "Selection" };
			bar.AddThemeConstantOverride("separation", 6);
			var marked = _player.Runes.Where(r => _marked.Contains(r.Id)).ToList();
			bar.AddChild(Layout.Text(T("runes.select_hint", marked.Count)).Named("Hint"));
			var row = Layout.Grid(3, 8).Named("Buttons");
			bar.AddChild(row);
			row.AddChild(GameButton.Of(T("runes.mark_all"), () =>
			{
				foreach (var rune in visible.Where(r => r.EquippedOn == null && !r.Locked))
					_marked.Add(rune.Id);
				Refresh();
			}, ButtonKind.Secondary, "copies", 48).Named("MarkAll"));
			var unmark = GameButton.Of(T("runes.unmark"), () =>
			{
				_marked.Clear();
				Refresh();
			}, ButtonKind.Secondary, null, 48).Named("Unmark");
			unmark.Disabled = marked.Count == 0;
			row.AddChild(unmark);

			var value = marked.Sum(RuneRules.SellValue);
			var sell = GameButton.Of(T("runes.sell_marked", marked.Count), () => Dialog.Confirm(this,
				T("runes.sell_title"),
				T("runes.sell_many_confirm", marked.Count, value),
				T("runes.sell_button_short"),
				() =>
				{
					var ids = marked.Select(r => r.Id).ToList();
					_marked.Clear();
					SellManyRequested?.Invoke(ids);
				}, ButtonKind.Danger), ButtonKind.Danger, "dismantle", 48).Named("SellMarked");
			if (marked.Count > 0)
				sell.WithCost("essence", $"+{value}");
			sell.Disabled = marked.Count == 0;
			row.AddChild(sell);
			return bar;
		}

		private void ToolList(RuneToolKind kind)
		{
			_middle.AddChild(Layout.Text(T(kind == RuneToolKind.Grindstone ? "runes.grind_hint" : "runes.enchant_hint", RuneForge.EnchantLevel), GameTheme.Faded).Named("Hint"));
			var groups = _player.Tools.Where(t => t.Kind == kind).GroupBy(t => t).OrderByDescending(g => g.Key.Grade).ThenBy(g => g.Key.Stat).ToList();
			if (groups.Count == 0)
			{
				_middle.AddChild(Layout.Text(T("runes.no_tools"), GameTheme.Faded).Named("Empty"));
				return;
			}

			var list = new VBoxContainer { Name = "Tools" };
			list.AddThemeConstantOverride("separation", 6);
			for (var i = 0; i < groups.Count; i++)
			{
				var group = groups[i];
				var row = Layout.Row(10).Named($"Tool{i + 1}");
				row.AddChild(Doodle.Icon(Art.Icon(kind == RuneToolKind.Grindstone ? "grindstone" : "gem"), 32, Palette.Of(group.Key.Grade)).Named("Icon"));
				var name = new Label { Name = "Name", Text = Texts.Name(group.Key), SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
				name.AddThemeColorOverride("font_color", Palette.Of(group.Key.Grade));
				row.AddChild(name);
				row.AddChild(new Label { Name = "Range", Text = Texts.Range(group.Key), ThemeTypeVariation = GameTheme.Faded });
				row.AddChild(new Label { Name = "Count", Text = $"×{group.Count()}", ThemeTypeVariation = GameTheme.Number, CustomMinimumSize = new Vector2(48, 0), HorizontalAlignment = HorizontalAlignment.Right });
				list.AddChild(row);
			}

			_listScroll = Layout.Scroll(list);
			_middle.AddChild(_listScroll);
		}

		// Ficha da runa -----------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			Layout.Clear(_actions);
			var rune = _player.Runes.FirstOrDefault(r => r.Id == _selectedRune);
			_actions.Visible = rune != null;
			if (rune == null)
			{
				_detail.AddChild(Layout.Text(T("runes.pick_one"), GameTheme.Faded, 300).Named("Empty"));
				return;
			}

			var opened = _levelWhenOpened.GetValueOrDefault(rune.Id, rune.Level);
			if (!_levelWhenOpened.ContainsKey(rune.Id))
				_levelWhenOpened[rune.Id] = rune.Level;

			string? note = null;
			if (rune.EquippedOn is { } owner && _player.Monster(owner) is { } holder)
				note = T(holder.Stored ? "rune.owner_vault" : "rune.owner", _database.Summon(holder.SummonId).NameFor(holder.Awakened));
			_detail.AddChild(new RuneCard(rune, 300, opened, index => SubstatTools(rune, index), note));

			var full = RuneInventory.IsFull(_player);
			if (rune.EquippedOn != null && (rune.EquippedOn == _monsterId || Monster == null))
			{
				var remove = GameButton.Of(T("runes.remove"), () => UnequipRequested?.Invoke(rune.Id), ButtonKind.Secondary, "cancel").Named("Unequip");
				remove.Disabled = full;
				_actions.AddChild(remove);
				if (full)
					_detail.AddChild(Layout.Text(T("runes.inventory_full", RuneInventory.Capacity), GameTheme.Faded).Named("Full"));
			}
			else if (Monster is { } monster)
			{
				_actions.AddChild(GameButton.Of(T("runes.equip_on"), () => EquipRequested?.Invoke(rune.Id, monster.Id), ButtonKind.Primary, "confirm").Named("Equip"));
			}

			if (rune.Level < RuneRules.MaxLevel)
			{
				var next = RuneRules.UpgradeCost(rune);
				var up = GameButton.Of(T("runes.upgrade_one", 1), () => UpgradeRequested?.Invoke(rune.Id, rune.Level + 1), ButtonKind.Primary, "level_max").WithCost("essence", Texts.Number(next)).Named("Upgrade");
				up.Disabled = _player.Essence < next;
				_actions.AddChild(up);

				var milestone = RuneRules.NextMilestone(rune.Level);
				if (milestone > rune.Level + 1)
				{
					var total = RuneRules.UpgradeCost(rune, milestone);
					var jump = GameButton.Of(T("runes.upgrade_button", milestone), () => RuneDialog.ConfirmUpgrade(this, rune, milestone, _player.Essence, () => UpgradeRequested?.Invoke(rune.Id, milestone)),
						ButtonKind.Secondary, "level_max").WithCost("essence", Texts.Number(total)).Named("UpgradeToMilestone");
					jump.Disabled = _player.Essence < total;
					_actions.AddChild(jump);
				}
			}

			_actions.AddChild(GameButton.Of(rune.Locked ? T("lock.unlock") : T("lock.lock"), () => LockRequested?.Invoke(rune.Id), ButtonKind.Secondary, rune.Locked ? "unlock" : "lock").Named("Lock"));
			Reappraise(rune);
            var value = RuneRules.SellValue(rune);
			var sellBtn = GameButton.Of(T("runes.sell_button_short"), () => Dialog.Confirm(this, T("runes.sell_title"), T("runes.sell_confirm", value), T("runes.sell_button_short"),
				() => SellRequested?.Invoke(rune.Id), ButtonKind.Danger), ButtonKind.Danger, "dismantle").WithCost("essence", $"+{value}").Named("Sell");
            if (rune.Locked)
			{
				_detail.AddChild(Layout.Text(T("runes.locked_note"), GameTheme.Faded).Named("Locked"));
				sellBtn.Disabled = true;
                _actions.AddChild(sellBtn);
            }
            else if (rune.EquippedOn == null)
			{
                _actions.AddChild(sellBtn);
            }
        }

		/// <summary>
		/// Reavaliar: só aparece numa runa que mudou desde que caiu. Sem gema, fica apagado e diz onde
		/// comprar; com, pergunta antes, listando tudo o que vai ser desfeito.
		/// </summary>
		private void Reappraise(Rune rune)
		{
			var changes = RuneReappraisal.Preview(rune);
			if (!changes.Any)
				return;

			var gems = _player.ReappraisalGems;
			var button = GameButton.Of(T("runes.reappraise", gems), () => Dialog.Confirm(this, T("runes.reappraise_title"), ReappraisalText(changes, gems),
				T("runes.reappraise_button"), () => ReappraiseRequested?.Invoke(rune.Id), ButtonKind.Danger), ButtonKind.Secondary, "gem").Named("Reappraise");
			button.Disabled = gems == 0;
			_actions.AddChild(button);
			if (gems == 0)
				_detail.AddChild(Layout.Text(T("runes.reappraise_none"), GameTheme.Faded, 300).Named("ReappraiseNone"));
		}

		/// <summary>O que a gema vai desfazer, uma linha por mudança, e o custo.</summary>
		private static string ReappraisalText(ReappraisalChanges changes, int gems)
		{
			var lines = new List<string>();
			if (changes.Level > 0)
				lines.Add(T("runes.reappraise_level", changes.Level));
			if (changes.RemovedSubstats.Count > 0)
				lines.Add(T("runes.reappraise_removed", string.Join(", ", changes.RemovedSubstats.Select(Texts.Name))));
			if (changes.ExtraRolls > 0)
				lines.Add(T("runes.reappraise_rolls", changes.ExtraRolls));
			if (changes.Grinds > 0)
				lines.Add(T("runes.reappraise_grinds", changes.Grinds));
			if (changes.Enchant is { } enchant)
				lines.Add(T("runes.reappraise_enchant", Texts.Name(enchant.To), Texts.Name(enchant.From)));
			if (changes.EnchantKept)
				lines.Add(T("runes.reappraise_enchant_kept"));
			return string.Join("\n", lines.Select(line => "• " + line)) + "\n\n" + T("runes.reappraise_cost", gems);
		}

		/// <summary>Os botões das pedras que servem num subatributo, no fim da linha dele na ficha.</summary>
		private IEnumerable<Control> SubstatTools(Rune rune, int index)
		{
			var grindstones = Usable(t => RuneForge.CanGrind(rune, index, t));
			if (grindstones.Count > 0)
				yield return ToolButton(T("runes.grind"), "grindstone", grindstones, tool => GrindRequested?.Invoke(rune.Id, index, tool)).Named("Grind");

			var gems = Usable(t => RuneForge.CanEnchant(rune, index, t));
			if (gems.Count > 0)
				yield return ToolButton(T("runes.enchant"), "gem", gems, tool => EnchantRequested?.Invoke(rune.Id, index, tool)).Named("Enchant");
		}

		/// <summary>Pedras diferentes que servem, uma de cada.</summary>
		private List<RuneTool> Usable(Func<RuneTool, bool> fits) =>
			_player.Tools.Distinct().Where(fits).OrderByDescending(t => t.Grade).ToList();

		/// <summary>O botão da pedra: abre a lista das pedras que servem neste subatributo.</summary>
		private GameButton ToolButton(string text, string icon, IReadOnlyList<RuneTool> tools, Action<RuneTool> chosen)
		{
			var button = new GameButton(text, ButtonKind.Secondary, icon, 40);
			button.Pressed += () => Choices.Open(button, text,
				tools.Select(t => new Choice($"{Texts.Name(t)} ({Texts.Range(t)})", Art.Icon(icon), Palette.Of(t.Grade))).ToList(),
				-1, index => chosen(tools[index]));
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

        private void ConfigureGrid(GridContainer container, int separation)
        {
            container.AddThemeConstantOverride("h_separation", separation);
            container.AddThemeConstantOverride("v_separation", separation);
        }
    }
}
