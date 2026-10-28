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
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Sigilos, em duas colunas:
	///
	/// - À esquerda, o homúnculo que recebe os sigilos (Trocar abre a grade de todos), a ficha de atributos
	///   (agora e com a prova, a diferença em ▲▼), os conjuntos e os 6 espaços em duas fileiras: Antes, como
	///   ele está, e Depois, com a prova (<see cref="RuneSwapStrip"/>). Presos embaixo: Auto-equipar (abre a
	///   <see cref="AutoEquipDialog"/>), Limpar a prova e Equipar a prova inteira de uma vez. Tocar num espaço
	///   filtra a lista por ele e, se há sigilo ali, abre a comparação dele.
	/// - À direita, pelas abas escritas, o inventário (o espaço, o principal e o subatributo à vista; Filtros,
	///   Ordem, Onde e Marcar), as Pedras de Afiar e as Gemas Encantadas. Na ordem por um subatributo, o valor
	///   dele fica escrito no alto de cada pedra.
	///
	/// Tocar num sigilo abre a comparação (RuneScreen.Compare.cs): o que o homúnculo usa no mesmo espaço ao
	/// lado do escolhido, cada um com Equipar ou Remover, Provar, Aprimorar, Bloquear e Vender. Aprimorar abre
	/// as abas Melhorar, Afiar, Encantar e Reavaliar (RuneScreen.Upgrade.cs). Marcar deixa tocar em vários:
	/// Melhorar leva os marcados até um nível de uma vez (a melhoria seletiva); Vender vende os soltos e
	/// desbloqueados entre eles.
	///
	/// Provar guarda o sigilo numa prova, um por espaço: a ficha e a fileira Depois somam a prova, para montar a
	/// combinação inteira antes de equipar. As janelas abertas se refazem a cada <see cref="Refresh"/>; a grade,
	/// que pode ter centenas, é remontada só quando a busca muda (o toque troca o destaque nas pedras). O que o
	/// sigilo ganhou desde que a tela abriu aparece em verde ao lado do valor. A Batalha automática segue rodando
	/// enquanto esta tela está aberta.
	/// </summary>
	public partial class RuneScreen : Control
	{
		private const float LeftWidth = 420;

		/// <summary>As pedras do Antes e do Depois: os dois círculos, lado a lado, cabem na coluna da esquerda.</summary>
		private const float StripScale = 0.65f;

		/// <summary>Os espaços vazios que escolhem o espaço da busca: da altura dos botões ao lado.</summary>
		private const float SlotScale = 0.7f;

		private const float BarHeight = 44;
		private const float ToolbarHeight = 40;

		/// <summary>A fileira do espaço e dos atributos: os campos "Subatributo: Velocidade" cabem em duas linhas.</summary>
		private const float FieldHeight = 48;

		/// <summary>Na escolha da ordem, as por subatributo vêm depois das outras: o valor é este mais o atributo.</summary>
		private const int SubstatOrder = 100;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		/// <summary>A melhora que acabou de ser pedida (sigilo, nível de antes e o quadro): a ficha dele a refaz na tela.</summary>
		private (int Rune, int Level, ulong Frame)? _upgrading;
		private int? _monsterId;
		private int? _selectedRune;
		private int _tab;
		private RuneFilter _filter = new();
		private RunePlace _place;
		private bool _selecting;
		private readonly HashSet<int> _marked = new();

		/// <summary>A prova: espaço → sigilo que entraria nele. Some ao trocar de homúnculo.</summary>
		private readonly SortedDictionary<int, int> _trial = new();

		/// <summary>As pedras da grade, pelo sigilo: o toque troca o destaque nelas, sem remontar a grade.</summary>
		private readonly Dictionary<int, RuneTile> _tiles = new();

		/// <summary>Os sigilos que a grade mostra agora (Marcar todas marca estes).</summary>
		private IReadOnlyList<Rune> _visible = Array.Empty<Rune>();

		/// <summary>Nível de cada sigilo quando a tela abriu: o que passou disso ganha destaque.</summary>
		private readonly Dictionary<int, int> _levelWhenOpened;

		/// <summary>Onde procurar: soltos, em homúnculos da coleção, em homúnculos do Baú ou todos.</summary>
		private enum RunePlace
		{
			Inventory,
			Collection,
			Storage,
			All,
		}

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _left = new() { Name = "Column" };
		private readonly VBoxContainer _head = new() { Name = "Monster" };
		private readonly StatTable _stats = new() { Name = "Stats" };
		private readonly HFlowContainer _sets = Layout.Flow(Space.Small).Named("Sets");
		private readonly RuneSwapStrip _strip = new(StripScale);
		private readonly GridContainer _bar = Layout.Grid(3, Space.Medium).Named("Trial");
		private GridContainer? _selection;
		private readonly HBoxContainer _tabs = Layout.Row(Space.Medium).Named("TabRow");
		private readonly VBoxContainer _middle = new() { Name = "List", SizeFlagsVertical = SizeFlags.ExpandFill };

		/// <summary>A rolagem da lista e de que aba ela é: a lista é remontada a cada busca, e a posição passa para a nova.</summary>
		private ScrollContainer? _listScroll;
		private int _listTab;

		public RuneScreen(GameDatabase database, PlayerState player, int? monsterId)
		{
			_database = database;
			_player = player;
			_monsterId = player.Monster(monsterId ?? -1) != null ? monsterId : null;
			_levelWhenOpened = player.Runes.ToDictionary(r => r.Id, r => r.Level);
		}

		/// <summary>Sigilo e homúnculo.</summary>
		public event Action<int, int>? EquipRequested;

		/// <summary>Os sigilos (a prova ou o Auto-equipar) e o homúnculo: todos de uma vez.</summary>
		public event Action<IReadOnlyList<int>, int>? EquipManyRequested;

		public event Action<int>? UnequipRequested;

		/// <summary>Id do sigilo e o nível a alcançar.</summary>
		public event Func<int, int, System.Threading.Tasks.Task>? UpgradeRequested;

		/// <summary>Os sigilos marcados e o nível a alcançar: sobem os que estão abaixo dele, todos ou nenhum.</summary>
		public event Action<IReadOnlyList<int>, int>? UpgradeManyRequested;

		/// <summary>Id do sigilo, índice do subatributo e a Pedra de Afiar.</summary>
		public event Action<int, int, RuneTool>? GrindRequested;

		/// <summary>Id do sigilo, índice do subatributo e a Gema Encantada.</summary>
		public event Action<int, int, RuneTool>? EnchantRequested;

		public event Action<int>? SellRequested;
		public event Action<IReadOnlyList<int>>? SellManyRequested;

		/// <summary>Bloquear ou desbloquear o sigilo (bloqueado não se vende).</summary>
		public event Action<int>? LockRequested;

		/// <summary>Gastar uma Gema de Reavaliação: o sigilo volta ao estado em que caiu.</summary>
		public event Action<int>? ReappraiseRequested;
		public event Action<int?>? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Runes"), _currencies, () => BackRequested?.Invoke(_monsterId), this,
				T("runes.help_screen"), T("runes.help_compare"), T("runes.auto_help"), T("runes.help_mark"),
				T("runes.grind_hint"), T("runes.enchant_hint", RuneForge.EnchantLevel)).Header);

			var body = Layout.Row(Space.Loose).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var left = new PanelContainer { Name = "Left", CustomMinimumSize = new Vector2(LeftWidth, 0) };
			_left.AddThemeConstantOverride("separation", Space.Large);
			_head.AddThemeConstantOverride("separation", Space.Medium);
			_strip.OpenedLevel = OpenedLevel;
			_strip.SlotPressed += PressSlot;
			var spacer = new Control { Name = "Spacer", SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
			foreach (var part in new Control[] { _head, _stats, _sets, spacer, _strip, _bar })
				_left.AddChild(part);
			left.AddChild(_left);
			body.AddChild(left);

			var right = new PanelContainer { Name = "Right", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", Space.Medium);
			_middle.AddThemeConstantOverride("separation", Space.Medium);
			column.AddChild(_tabs);
			column.AddChild(_middle);
			right.AddChild(column);
			body.AddChild(right);

			Refresh();
		}

		public void Refresh()
		{
			if (_selectedRune is { } id && _player.Runes.All(r => r.Id != id))
				_selectedRune = null;
			_marked.RemoveWhere(runeId => _player.Runes.All(r => r.Id != runeId));
			PruneTrial();

			_currencies.Refresh(_player);
			RefreshLeft();
			RefreshTabs();
			RefreshMiddle();
			RefreshCompare();
			RefreshUpgrade();
			RefreshBulk();
		}

		// Homúnculo, ficha e espaços --------------------------------------------------------------

		private OwnedSummon? Monster => _monsterId is { } id ? _player.Monster(id) : null;

		private void RefreshLeft()
		{
			RefreshHead();
			RefreshBar();
			var monster = Monster;
			var equipped = monster != null ? _player.RunesOn(monster.Id) : Array.Empty<Rune>();
			var incoming = monster != null ? Incoming() : Array.Empty<Rune>();
			var next = RuneSwap.Apply(equipped, incoming);
			_strip.Show(equipped, next);

			Layout.Clear(_sets);
			_sets.Visible = false;
			_stats.Visible = monster != null;
			if (monster == null)
				return;

			var summon = _database.Summon(monster.SummonId);
			var now = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, equipped);
			var after = incoming.Count > 0 ? SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, next) : null;
			_stats.Compare(now, after);
			RefreshSets(now, after);
		}

		/// <summary>O homúnculo numa linha: rosto, nome, estrelas e nível, e Trocar. Sem homúnculo, o convite para escolher.</summary>
		private void RefreshHead()
		{
			Layout.Clear(_head);
			if (Monster is not { } monster)
			{
				_head.AddChild(GameButton.Of(T("runes.choose_monster"), ChooseMonster, ButtonKind.Primary, "monster", BarHeight).Named("Choose"));
				_head.AddChild(Layout.Text(T("runes.no_monster_text"), GameTheme.Faded, LeftWidth - 40).Named("Empty"));
				return;
			}

			var summon = _database.Summon(monster.SummonId);
			var row = Layout.Row(Space.Regular).Named("Row");
			row.AddChild(Portrait(summon, monster, 52));
			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddThemeConstantOverride("separation", -2); // Negativo: as linhas da ficha coladas, uma peça só.
			info.AddChild(new Label { Name = "Name", Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis });
			info.AddChild(new Label { Name = "Level", Text = T("common.stars_level", Texts.Stars(monster.Stars), monster.Level) + (monster.Stored ? " · " + T("monsters.vault") : ""), ThemeTypeVariation = GameTheme.Faded });
			row.AddChild(info);
			var change = GameButton.Of(T("runes.change_monster"), ChooseMonster, ButtonKind.Secondary, null, 40).Named("Choose");
			change.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			change.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			row.AddChild(change);
			_head.AddChild(row);
		}

		private void ChooseMonster() => MonsterPicker.Open(this, _database, _player, _monsterId, true, id =>
		{
			if (id != _monsterId)
				_trial.Clear();
			_monsterId = id;
			RefreshLeft();
		});

		/// <summary>
		/// Os botões presos embaixo da coluna, sempre nas mesmas células: Auto-equipar, Limpar e Equipar a prova.
		/// O que não vale agora fica no lugar, apagado, e o toque diz o motivo.
		/// </summary>
		private void RefreshBar()
		{
			Layout.Clear(_bar);
			var monster = Monster;
			var empty = _trial.Count == 0;
			_bar.AddChild(GameButton.Of(T("runes.auto"), OpenAutoEquip, ButtonKind.Secondary, null, BarHeight)
				.Block(monster == null ? T("runes.why_no_monster") : monster.IsInfusionCore ? T("runes.why_auto_core") : null, T("runes.auto"))
				.FixedHeight().Named("AutoEquip"));
			_bar.AddChild(GameButton.Of(T("runes.trial_clear"), () =>
				{
					_trial.Clear();
					RefreshLeft();
				}, ButtonKind.Secondary, null, BarHeight)
				.Block(empty ? T("runes.why_trial_empty") : null, T("runes.trial_clear")).FixedHeight().Named("Clear"));
			_bar.AddChild(GameButton.Of(empty ? T("runes.auto_equip") : T("runes.trial_equip", _trial.Count), () => EquipManyRequested?.Invoke(_trial.Values.ToList(), monster!.Id), ButtonKind.Primary, null, BarHeight)
				.Block(monster == null ? T("runes.why_no_monster") : empty ? T("runes.why_trial_empty") : null, T("runes.auto_equip"))
				.FixedHeight().Named("Equip"));
		}

		/// <summary>Auto-equipar: a janela da melhor combinação (<see cref="AutoEquipDialog"/>); Equipar lá equipa tudo de uma vez.</summary>
		private void OpenAutoEquip()
		{
			if (Monster is not { } monster)
				return;

			AutoEquipDialog.Open(this, _database, _player, monster, ids =>
			{
				_trial.Clear();
				EquipManyRequested?.Invoke(ids, monster.Id);
			});
		}

		/// <summary>Um espaço do Antes ou do Depois: a lista passa a mostrar só esse espaço e, se há sigilo nele, a comparação abre.</summary>
		private void PressSlot(RuneTile tile)
		{
			var rune = tile.Rune;
			_filter = _filter with { Slots = new[] { tile.Slot } };
			_tab = 0;
			_selectedRune = rune?.Id ?? _selectedRune;
			Refresh();
			if (rune != null)
				OpenCompare(rune);
		}

		/// <summary>
		/// Os conjuntos: os que ficam, os que a troca fecha (+, em verde) e os que ela abre (−, em vermelho,
		/// apagados). Um conjunto de 2 peças pode valer mais de uma vez: conta cada vez.
		/// </summary>
		private void RefreshSets(StatSheet now, StatSheet? next)
		{
			var before = now.Runes.ActiveSets.Select(s => s.Set).ToList();
			var after = next?.Runes.ActiveSets.Select(s => s.Set).ToList() ?? before;
			var kept = new List<RuneSet>();
			var lost = new List<RuneSet>(before);
			var gained = new List<RuneSet>();
			foreach (var set in after)
			{
				if (lost.Remove(set))
					kept.Add(set);
				else
					gained.Add(set);
			}

			var chips = kept.Select(set => (Set: set, Sign: "", Ink: (Color?)null))
				.Concat(gained.Select(set => (Set: set, Sign: "+", Ink: (Color?)Palette.Positive)))
				.Concat(lost.Select(set => (Set: set, Sign: "−", Ink: (Color?)Palette.Negative)))
				.ToList();
			_sets.Visible = chips.Count > 0;
			// Conjunto de 2 peças pode estar ativo mais de uma vez: a posição na fileira deixa o nome único.
			for (var i = 0; i < chips.Count; i++)
			{
				var (set, sign, ink) = chips[i];
				var chip = Layout.Labeled(RuneSets.PlanetOf(set), sign, Texts.Name(set), ink).Named($"{set}{i + 1}");
				if (ink is { } color)
					chip.GetNode<Label>("Row/Value").AddThemeColorOverride("font_color", color);
				if (sign == "−")
					chip.Modulate = new Color(1, 1, 1, 0.6f);
				_sets.AddChild(chip);
			}
		}

		/// <summary>O que chegaria ao homúnculo: a prova (Provar). Tocar num sigilo da lista só abre a comparação.</summary>
		private IReadOnlyList<Rune> Incoming() => _trial.Values.Select(id => _player.Runes.First(r => r.Id == id)).ToList();

		/// <summary>Tira da prova o sigilo que sumiu, que já está no homúnculo, ou tudo, sem homúnculo.</summary>
		private void PruneTrial()
		{
			foreach (var (slot, id) in _trial.ToList())
			{
				if (Monster is not { } monster || _player.Runes.FirstOrDefault(r => r.Id == id) is not { } rune || rune.EquippedOn == monster.Id)
					_trial.Remove(slot);
			}
		}

		// Abas e lista ----------------------------------------------------------------------------

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

			// Quantos sigilos a busca achou, à direita das abas: as fileiras de baixo ficam só com botões.
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
			_selection = null;
			_tiles.Clear();
			_visible = Array.Empty<Rune>();
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
			_visible = runes;
			_middle.AddChild(SlotRow());
			_middle.AddChild(Toolbar());

			if (_selecting)
			{
				_selection = Layout.Grid(4, Space.Medium).Named("Selection");
				_middle.AddChild(_selection);
				RefreshSelection();
			}

			// Só as pedras que aparecem na rolagem existem como nós (o inventário passa de 800): as outras são
			// montadas quando chegam e soltas quando saem; o estado (escolhida, marcada) vem do que a tela guarda.
			var grid = new TileGrid(Space.Small) { Name = "Grid" };
			grid.Released += (index, _) => _tiles.Remove(runes[index].Id);
			grid.Virtualize(runes.Count, RuneTile.TileSize, index => NewTile(runes[index]));

			if (runes.Count == 0)
				_middle.AddChild(Layout.Text(T("runes.none_found"), GameTheme.Faded, 400).Named("Empty"));
			_listScroll = Layout.Scroll(grid);
			_middle.AddChild(_listScroll);
		}

		/// <summary>
		/// O nível do sigilo quando a tela abriu: o que ele ganhou desde então fica em verde. O sigilo que voltou
		/// atrás (a Gema de Reavaliação o devolve ao estado em que caiu) e o que chegou depois partem de agora.
		/// </summary>
		private int Opened(Rune rune)
		{
			if (!_levelWhenOpened.TryGetValue(rune.Id, out var opened) || rune.Level < opened)
				_levelWhenOpened[rune.Id] = opened = rune.Level;
			return opened;
		}

		private int? OpenedLevel(Rune rune) => Opened(rune);

		private RuneTile NewTile(Rune rune)
		{
			var tile = new RuneTile(rune, rune.Slot) { Name = $"Rune{rune.Id}", OpenedLevel = OpenedLevel };
			tile.SetSelected(rune.Id == _selectedRune);
			tile.SetMarked(_marked.Contains(rune.Id));
			// Na ordem por subatributo, o valor dele fica escrito na pedra: a comparação sem abrir uma por uma.
			if (_filter.Sort == RuneSort.Substat && RuneFilter.SubstatValue(rune, _filter.SortStat) is var amount and > 0)
				tile.SetValue(Texts.Amount(_filter.SortStat, amount));
			if (rune.EquippedOn is { } owner && _player.Monster(owner) is { } holder)
			{
				var summon = _database.Summon(holder.SummonId);
				tile.SetOwner(Art.Creature(summon.Image), Palette.Of(summon.Element), holder.Stored);
			}

			tile.Pressed += t => Tap(t.Rune!);
			_tiles[rune.Id] = tile;
			return tile;
		}

		/// <summary>
		/// O toque num sigilo da grade: marcando, marca ou desmarca (qualquer um: os equipados e os bloqueados
		/// também sobem na melhoria seletiva); senão, escolhe e abre a comparação. A grade fica como está.
		/// </summary>
		private void Tap(Rune rune)
		{
			if (_selecting)
			{
				if (!_marked.Remove(rune.Id))
					_marked.Add(rune.Id);
				if (_tiles.TryGetValue(rune.Id, out var marked))
					marked.SetMarked(_marked.Contains(rune.Id));
				RefreshSelection();
				return;
			}

			if (_selectedRune is { } previous && _tiles.TryGetValue(previous, out var before))
				before.SetSelected(false);
			_selectedRune = rune.Id;
			if (_tiles.TryGetValue(rune.Id, out var chosen))
				chosen.SetSelected(true);
			OpenCompare(rune);
		}

		/// <summary>Marca ou desmarca vários de uma vez, nas pedras que já estão na grade.</summary>
		private void Mark(IEnumerable<Rune> runes, bool marked)
		{
			foreach (var rune in runes.ToList())
			{
				if (marked)
					_marked.Add(rune.Id);
				else
					_marked.Remove(rune.Id);
				if (_tiles.TryGetValue(rune.Id, out var tile))
					tile.SetMarked(marked);
			}

			RefreshSelection();
		}

		/// <summary>
		/// O espaço da busca (Todos, ou os 6 selos: tocar no aceso volta a Todos) e os campos do principal e do
		/// subatributo, à vista: são os filtros mais usados. O resto fica em Filtros.
		/// </summary>
		private Control SlotRow()
		{
			var row = Layout.Row(Space.Medium).Named("SlotRow");
			var slots = Layout.Row(Space.Small).Named("Slots");
			slots.AddChild(GameButton.Of(T("filter.all"), () => SetSlot(null), _filter.Slots.Count == 0 ? ButtonKind.Primary : ButtonKind.Secondary, null, FieldHeight).FixedHeight().Named("All"));
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(null, slot, SlotScale) { Name = $"Slot{slot}", SizeFlagsVertical = SizeFlags.ShrinkCenter };
				tile.SetSelected(_filter.Slots.Contains(slot));
				tile.Pressed += t => SetSlot(_filter.Slots.Count == 1 && _filter.Slots[0] == t.Slot ? null : t.Slot);
				slots.AddChild(tile);
			}

			row.AddChild(slots);

			var fields = Layout.Grid(2, Space.Medium).Named("Fields");
			fields.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			var main = new MultiChoiceButton(T("filter.main"), StatOptions(), _filter.Mains.Select(s => (int)s), FieldHeight) { Name = "Main" };
			main.Changed += values =>
			{
				_filter = _filter with { Mains = values.Select(v => (RuneStat)v).ToList() };
				Callable.From(Refresh).CallDeferred();
			};
			fields.AddChild(main.FixedHeight());

			// Vários subatributos: o sigilo precisa ter todos. Procurar já ordena pelo primeiro, o maior primeiro (só
			// ao começar a procurar ou se o que ordenava saiu); a Ordem ainda muda depois.
			var substat = new MultiChoiceButton(T("filter.substat"), StatOptions(), _filter.Substats.Select(s => (int)s), FieldHeight) { Name = "Substat" };
			substat.Changed += values =>
			{
				var stats = values.Select(v => (RuneStat)v).ToList();
				var sorting = _filter.Sort == RuneSort.Substat && stats.Contains(_filter.SortStat);
				_filter = stats.Count == 0 || sorting || (_filter.Substats.Count > 0 && _filter.Sort != RuneSort.Substat)
					? _filter with { Substats = stats }
					: _filter with { Substats = stats, Sort = RuneSort.Substat, SortStat = stats[0] };
				Callable.From(Refresh).CallDeferred();
			};
			fields.AddChild(substat.FixedHeight());
			row.AddChild(fields);
			return row;
		}

		private void SetSlot(int? slot)
		{
			_filter = _filter with { Slots = slot is { } only ? new[] { only } : Array.Empty<int>() };
			Callable.From(Refresh).CallDeferred();
		}

		private static IReadOnlyList<(Choice Choice, int Value)> StatOptions() =>
			Enum.GetValues<RuneStat>().Select(s => (new Choice(Texts.Label(s), Symbol: Texts.PlanetOf(s)), (int)s)).ToList();

		/// <summary>Os botões do inventário numa fileira de 4: Filtros (com quantos ligados), Ordem, Onde e Marcar.</summary>
		private Control Toolbar()
		{
			var grid = Layout.Grid(4, Space.Medium).Named("Toolbar");
			grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			var active = ActiveFilters();
			grid.AddChild(GameButton.Of(active == 0 ? T("filter.button") : T("filter.button_active", active), OpenFilters, active == 0 ? ButtonKind.Secondary : ButtonKind.Primary, "search", ToolbarHeight)
				.FixedHeight().Named("Filters"));

			var current = _filter.Sort == RuneSort.Substat ? SubstatOrder + (int)_filter.SortStat : (int)_filter.Sort;
			var sort = new ChoiceButton(T("filter.sort"), SortOptions(), current, ToolbarHeight) { Name = "Sort" };
			sort.Changed += value =>
			{
				_filter = value >= SubstatOrder
					? _filter with { Sort = RuneSort.Substat, SortStat = (RuneStat)(value - SubstatOrder) }
					: _filter with { Sort = (RuneSort)value };
				Callable.From(Refresh).CallDeferred();
			};
			grid.AddChild(sort.FixedHeight());

			var place = new ChoiceButton(T("filter.where"), Enum.GetValues<RunePlace>().Select(p => (new Choice(T($"filter.place.{p}")), (int)p)).ToList(), (int)_place, ToolbarHeight) { Name = "Place" };
			place.Changed += value =>
			{
				_place = (RunePlace)value;
				Callable.From(Refresh).CallDeferred();
			};
			grid.AddChild(place.FixedHeight());

			grid.AddChild(GameButton.Of(_selecting ? T("runes.select_done") : T("runes.select"), () =>
			{
				_selecting = !_selecting;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			}, _selecting ? ButtonKind.Primary : ButtonKind.Secondary, "select", ToolbarHeight).FixedHeight().Named("Select"));
			return grid;
		}

		/// <summary>Os filtros ligados na janela (o espaço, o principal e o subatributo ficam à vista, fora da conta).</summary>
		private int ActiveFilters() =>
			(_filter.Sets.Count > 0 ? 1 : 0) + (_filter.MinGrade > 1 ? 1 : 0) + (_filter.MinRarity > RuneRarity.Normal ? 1 : 0)
			+ (_filter.MinLevel > 0 ? 1 : 0) + (_filter.Condition != null ? 1 : 0);

		/// <summary>As ordens da grade e, depois, uma por subatributo (o valor dele fica escrito em cada pedra).</summary>
		private static IReadOnlyList<(Choice Choice, int Value)> SortOptions() =>
			Enum.GetValues<RuneSort>().Where(s => s != RuneSort.Substat).Select(s => (new Choice(Texts.Name(s)), (int)s))
				.Concat(Enum.GetValues<RuneStat>().Select(s => (new Choice(T("filter.order_substat", Texts.Label(s).ToLower(Culture)), Symbol: Texts.PlanetOf(s)), SubstatOrder + (int)s)))
				.ToList();

		/// <summary>
		/// A janela dos filtros (<see cref="FilterDialog"/>): um campo por linha, e muda na hora. Limpar desfaz só
		/// os campos dela: o espaço, o principal e o subatributo ficam como estão na tela.
		/// </summary>
		private void OpenFilters() => FilterDialog.Open(this, T("filter.title"), dialog =>
		{
			dialog.MultiField("Set", T("filter.set"), RuneSets.ActiveSets.Select(s => (new Choice(Texts.Name(s), Symbol: RuneSets.PlanetOf(s)), (int)s)), _filter.Sets.Select(s => (int)s),
				values => _filter = _filter with { Sets = values.Select(v => (RuneSet)v).ToList() });
			dialog.Field("Stars", T("filter.stars"), Enumerable.Range(2, RuneRules.MaxGrade - 1).Select(g => (new Choice(T("filter.at_least", Texts.Stars(g))), g)), _filter.MinGrade > 1 ? _filter.MinGrade : FilterDialog.All,
				value => _filter = _filter with { MinGrade = Math.Max(1, value) });
			dialog.Field("Rarity", T("filter.rarity"), Enum.GetValues<RuneRarity>().Skip(1).Select(r => (new Choice(T("filter.at_least", Texts.Name(r)), Art.Icon("gem"), Palette.Of(r)), (int)r)), _filter.MinRarity > RuneRarity.Normal ? (int)_filter.MinRarity : FilterDialog.All,
				value => _filter = _filter with { MinRarity = value < 0 ? RuneRarity.Normal : (RuneRarity)value });
			dialog.Field("Upgrade", T("filter.upgrade"), new[] { 3, 6, 9, 12, 15 }.Select(l => (new Choice(T("filter.at_least", $"+{l}")), l)), _filter.MinLevel > 0 ? _filter.MinLevel : FilterDialog.All,
				value => _filter = _filter with { MinLevel = Math.Max(0, value) });
			dialog.Field("Condition", T("filter.condition"), Enum.GetValues<RuneCondition>().Select(c => (new Choice(T($"filter.rune_condition_kind.{c}")), (int)c)), _filter.Condition is { } condition ? (int)condition : FilterDialog.All,
				value => _filter = _filter with { Condition = value < 0 ? null : (RuneCondition)value });
		}, Refresh, () => _filter = new RuneFilter
		{
			Sort = _filter.Sort,
			SortStat = _filter.SortStat,
			Slots = _filter.Slots,
			Mains = _filter.Mains,
			Substats = _filter.Substats,
		});

		/// <summary>
		/// A fileira da marcação, sempre nas mesmas células: Marcar todas, Desmarcar, Melhorar (a melhoria
		/// seletiva) e Vender. Vender conta só os soltos e desbloqueados entre os marcados.
		/// </summary>
		private void RefreshSelection()
		{
			if (_selection == null || !IsInstanceValid(_selection))
				return;

			Layout.Clear(_selection);
			var marked = MarkedRunes();
			var sellable = marked.Where(r => r.EquippedOn == null && !r.Locked).ToList();
			_selection.AddChild(GameButton.Of(T("runes.mark_all"), () => Mark(_visible, true), ButtonKind.Secondary, null, ActionHeight)
				.FixedHeight().Named("MarkAll"));
			_selection.AddChild(GameButton.Of(T("runes.unmark"), () => Mark(marked, false), ButtonKind.Secondary, null, ActionHeight)
				.Block(marked.Count == 0 ? T("runes.why_marked") : null, T("runes.unmark")).FixedHeight().Named("Unmark"));
			_selection.AddChild(GameButton.Of(T("runes.upgrade_marked", marked.Count), OpenBulk, ButtonKind.Primary, "level_max", ActionHeight)
				.Block(marked.Count == 0 ? T("runes.why_marked") : null, T("runes.bulk_title")).FixedHeight().Named("UpgradeMarked"));

			var value = sellable.Sum(RuneRules.SellValue);
			var sell = GameButton.Of(T("runes.sell_marked", sellable.Count), () => Dialog.Confirm(this,
				T("runes.sell_title"),
				T("runes.sell_many_confirm", sellable.Count, value),
				T("runes.sell_button_short"),
				() =>
				{
					var ids = sellable.Select(r => r.Id).ToList();
					_marked.ExceptWith(ids);
					SellManyRequested?.Invoke(ids);
				}, ButtonKind.Danger), ButtonKind.Danger, "dismantle", ActionHeight).Named("SellMarked");
			sell.Block(marked.Count == 0 ? T("runes.why_marked") : sellable.Count == 0 ? T("runes.why_sell_none") : null, T("runes.sell_button_short"));
			_selection.AddChild(sell.FixedHeight());
		}

		private void ToolList(RuneToolKind kind)
		{
			var groups = Tools(kind, _ => true);
			if (groups.Count == 0)
			{
				_middle.AddChild(Layout.Text(T("runes.no_tools"), GameTheme.Faded).Named("Empty"));
				return;
			}

			var list = new VBoxContainer { Name = "Tools" };
			list.AddThemeConstantOverride("separation", Space.Small);
			for (var i = 0; i < groups.Count; i++)
				list.AddChild(ToolRow(groups[i].Tool, groups[i].Count).Named($"Tool{i + 1}"));
			_listScroll = Layout.Scroll(list);
			_middle.AddChild(_listScroll);
		}

		/// <summary>As pedras de um tipo que passam em <paramref name="fits"/>, iguais juntas, a melhor primeiro.</summary>
		private List<(RuneTool Tool, int Count)> Tools(RuneToolKind kind, Func<RuneTool, bool> fits) =>
			_player.Tools.Where(t => t.Kind == kind && fits(t)).GroupBy(t => t).OrderByDescending(g => g.Key.Grade).ThenBy(g => g.Key.Stat)
				.Select(g => (g.Key, g.Count())).ToList();

		/// <summary>Uma pedra numa linha: o símbolo e o nome na cor do grau, a faixa do que ela dá e quantas há.</summary>
		private static HBoxContainer ToolRow(RuneTool tool, int count)
		{
			var row = Layout.Row(Space.Regular);
			row.AddChild(Doodle.Icon(Art.Icon(tool.Kind == RuneToolKind.Grindstone ? "grindstone" : "gem"), 32, Palette.Of(tool.Grade)).Named("Icon"));
			var name = new Label { Name = "Name", Text = Texts.Name(tool), CustomMinimumSize = new Vector2(10, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			name.AddThemeColorOverride("font_color", Palette.Of(tool.Grade));
			row.AddChild(name);
			row.AddChild(new Label { Name = "Range", Text = Texts.Range(tool), ThemeTypeVariation = GameTheme.Faded });
			row.AddChild(new Label { Name = "Count", Text = $"×{count}", ThemeTypeVariation = GameTheme.Number, CustomMinimumSize = new Vector2(48, 0), HorizontalAlignment = HorizontalAlignment.Right });
			return row;
		}

		/// <summary>Quem usa o sigilo: o rosto dele (toque longo abre o resumo) e "Equipado em ...".</summary>
		private Control OwnerRow(OwnedSummon holder, float width)
		{
			var summon = _database.Summon(holder.SummonId);
			var row = Layout.Row(Space.Regular).Named("Owner");
			row.AddChild(Portrait(summon, holder, 36));
			var name = Layout.Text(T(holder.Stored ? "rune.owner_vault" : "rune.owner", summon.NameFor(holder.Awakened)), GameTheme.Faded, width - 46).Named("Name");
			name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			row.AddChild(name);
			return row;
		}

		/// <summary>O rosto do homúnculo num círculo com a moldura da raridade; toque longo abre o resumo dele.</summary>
		private static Control Portrait(SummonDefinition summon, OwnedSummon monster, float size)
		{
			var portrait = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Stop, SizeFlagsVertical = SizeFlags.ShrinkCenter };
			portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 2, (int)(size / 2), 3));
			portrait.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: monster.Awakened ? summon.Element : null));
			Press.On(portrait, null, () => MonsterSummary.Open(portrait, summon, monster));
			return portrait;
		}

		/// <summary>
		/// Onde o sigilo está, para a busca. Sigilo de conjunto fora do jogo (RuneSets.ActiveSets) segue no
		/// save mas não aparece em lista nenhuma.
		/// </summary>
		private bool InPlace(Rune rune) => RuneSets.IsActive(rune.Set) && _place switch
		{
			RunePlace.Inventory => rune.EquippedOn == null,
			RunePlace.Collection => _player.Monster(rune.EquippedOn ?? -1) is { Stored: false },
			RunePlace.Storage => _player.Monster(rune.EquippedOn ?? -1) is { Stored: true },
			_ => true,
		};
	}
}
