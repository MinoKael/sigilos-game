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
using System.Reflection;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// Runas, em três colunas:
	///
	/// - À esquerda, o monstro que recebe as runas (Trocar abre a grade de todos), o círculo de
	///   conjuração com os 6 espaços dele (tocar num espaço filtra a lista por ele), os conjuntos e a
	///   ficha de atributos, sempre à vista.
	/// - No meio, pelas abas escritas, o inventário de runas (com os botões Filtros, Ordenar e Onde, e
	///   Vender várias para marcar e vender de uma vez), as Pedras de Afiar e as Gemas Encantadas. Na
	///   ordem por um subatributo (a maior primeiro), o valor dele fica escrito no alto de cada pedra.
	/// - À direita, a runa escolhida, com os botões presos embaixo: Equipar, Provar, Remover, Melhorar,
	///   Vender, cada um dizendo o custo; e, quando alguma pedra serve, Afiar e Encantar, que abre a ficha
	///   numa janela com as pedras na linha de cada subatributo (na ficha da tela, elas a alargavam e
	///   empurravam os botões).
	///
	/// A prévia: escolher uma runa que não está no monstro já mostra, sem equipar, a ficha de agora ao
	/// lado da que ele teria com ela (<see cref="StatTable.Compare"/>), os conjuntos que fecham (+) e os
	/// que abrem (−), e a runa no espaço dela no círculo, em azul. Provar guarda a runa numa prova, uma
	/// por espaço, e a prévia soma a prova à runa escolhida: dá para montar a combinação inteira antes de
	/// equipar tudo de uma vez (Equipar N) ou largar (Limpar). A coluna "Agora" segue sendo o monstro como
	/// ele está.
	///
	/// Tocar numa runa só troca o destaque e refaz a coluna da esquerda e a ficha da runa: a grade, que
	/// pode ter centenas, fica como está. O que a runa ganhou desde que a tela abriu aparece em verde ao
	/// lado do valor (na ficha ao lado e na do toque longo). A Batalha automática segue rodando enquanto
	/// esta tela está aberta; o aviso no alto continua contando.
	///
	/// Melhorar: "Evoluir +1" e "Até +3/+6/+9/+15" (o próximo marco) valem na hora, sem pergunta. "Até +12"
	/// pergunta antes (só o gasto: o sorteio acontece depois) e, feita a melhora, abre a runa antes e depois
	/// (<see cref="RuneDialog.ShowUpgraded"/>). Tocar num espaço livre da tela desfaz o filtro de espaço que o
	/// toque no círculo pôs.
	/// </summary>
	public partial class RuneScreen : Control
	{
		private const int DirectLevel = RuneDialog.DirectLevel;

		/// <summary>Quanto o toque pode andar, em px, e ainda ser um toque (e não um arrasto de rolagem).</summary>
		private const float TapSlop = 12;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private Vector2 _pressedAt;

		/// <summary>A melhora que acabou de ser pedida (runa, nível de antes e o quadro): a ficha dela a refaz na tela.</summary>
		private (int Rune, int Level, ulong Frame)? _upgrading;
		private int? _monsterId;
		private int? _selectedRune;
		private int _tab;
		private RuneFilter _filter = new();
		private RunePlace _place;
		private bool _selecting;
		private readonly HashSet<int> _marked = new();

		/// <summary>A prova: espaço → runa que entraria nele. Some ao trocar de monstro.</summary>
		private readonly SortedDictionary<int, int> _trial = new();

		/// <summary>As pedras da grade, pela runa: o toque troca o destaque nelas, sem remontar a grade.</summary>
		private readonly Dictionary<int, RuneTile> _tiles = new();

		/// <summary>As runas que a grade mostra agora (Marcar todas marca estas).</summary>
		private IReadOnlyList<Rune> _visible = Array.Empty<Rune>();

		/// <summary>Os botões da runa escolhida: mais baixos que o toque padrão, para a ficha caber inteira.</summary>
		private const float ActionHeight = 50;

		/// <summary>Na escolha da ordem, as por subatributo vêm depois das outras: o valor é este mais o atributo.</summary>
		private const int SubstatOrder = 100;

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
		private readonly VBoxContainer _head = new() { Name = "Monster" };
		private readonly SigilRing _ring = new(220) { Name = "Ring", Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		private readonly HFlowContainer _sets = Layout.Flow(Space.Small).Named("Sets");
		private readonly HBoxContainer _trialBar = Layout.Row(Space.Medium).Named("Trial");
		private readonly StatTable _stats = new() { Name = "Stats" };
		private VBoxContainer? _selection;
		private readonly HBoxContainer _tabs = Layout.Row(Space.Medium).Named("TabRow");
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

		/// <summary>A janela de Afiar e Encantar, se aberta: cada pedra usada refaz a ficha dentro dela.</summary>
		private Dialog? _forge;

        public RuneScreen(GameDatabase database, PlayerState player, int? monsterId)
		{
			_database = database;
			_player = player;
			_monsterId = player.Monster(monsterId ?? -1) != null ? monsterId : null;
			_levelWhenOpened = player.Runes.ToDictionary(r => r.Id, r => r.Level);
		}

		/// <summary>Runa e monstro.</summary>
		public event Action<int, int>? EquipRequested;

		/// <summary>As runas da prova e o monstro: todas de uma vez.</summary>
		public event Action<IReadOnlyList<int>, int>? EquipManyRequested;

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
			page.AddChild(Layout.Header(T("destination.Runes"), _currencies, () => BackRequested?.Invoke(_monsterId), this, T("runes.no_monster_text"), T("runes.pick_one")).Header);

			var body = Layout.Row(Space.Large).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var left = new PanelContainer { Name = "Left", CustomMinimumSize = new Vector2(340, 0) };
			_left.AddThemeConstantOverride("separation", Space.Medium);
			_head.AddThemeConstantOverride("separation", Space.Medium);
			foreach (var part in new Control[] { _head, _ring, _sets, _trialBar, _stats })
				_left.AddChild(part);
			left.AddChild(Layout.Scroll(_left));
			body.AddChild(left);

			var middle = new PanelContainer { Name = "Middle", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var middleColumn = new VBoxContainer { Name = "Column" };
			middleColumn.AddThemeConstantOverride("separation", Space.Medium);
			middleColumn.AddChild(_tabs);
			middleColumn.AddChild(_middle);
			middle.AddChild(middleColumn);
			body.AddChild(middle);

			var right = new PanelContainer { Name = "Right", CustomMinimumSize = new Vector2(360, 0) };
			var rightColumn = new VBoxContainer { Name = "Column" };
			rightColumn.AddThemeConstantOverride("separation", Space.Medium);
			_detail.AddThemeConstantOverride("separation", Space.Medium);
			rightColumn.AddChild(Layout.Scroll(_detail));
			ConfigureGrid(_actions, 6);
			rightColumn.AddChild(_actions);
			right.AddChild(rightColumn);
			body.AddChild(right);

			Refresh();
		}

		/// <summary>
		/// Um toque num espaço livre (fora de botão, runa, janela, campo ou o que tem toque próprio) desfaz o filtro
		/// de espaço do círculo. Arrastar para rolar não conta.
		/// </summary>
		public override void _Input(InputEvent @event)
		{
			if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
				return;

			if (click.Pressed)
			{
				_pressedAt = click.Position;
				return;
			}

			if (_filter.Slots.Count == 0 || (click.Position - _pressedAt).Length() > TapSlop || !IsFree(click.Position))
				return;

			_filter = _filter with { Slots = Array.Empty<int>() };
			Refresh();
		}

		/// <summary>O ponto está sobre a tela e sobre nada que reaja ao toque.</summary>
		private bool IsFree(Vector2 point)
		{
			var hit = TouchScroll.TopControlAt(GetTree().Root, point);
			if (hit == null || !IsAncestorOf(hit))
				return false;

			for (Node? node = hit; node != null && node != this; node = node.GetParent())
			{
				if (node is BaseButton or Godot.Range or LineEdit or TextEdit or RuneTile or Dialog || HandlesInput(node))
					return false;
			}

			return true;
		}

		/// <summary>O nó trata o próprio toque (um cartão ou retrato com toque curto e longo): tem <c>_GuiInput</c> nosso.</summary>
		private static bool HandlesInput(Node node) =>
			node.GetType().GetMethod("_GuiInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				?.DeclaringType?.Namespace?.StartsWith("Sigilos", StringComparison.Ordinal) == true;

		public void Refresh()
		{
			if (_selectedRune is { } id && _player.Runes.All(r => r.Id != id))
				_selectedRune = null;
			_marked.RemoveWhere(runeId => _player.Runes.FirstOrDefault(r => r.Id == runeId) is not { Locked: false });
			PruneTrial();

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
			RefreshHead();
			var monster = Monster;
			var equipped = monster != null ? _player.RunesOn(monster.Id) : Array.Empty<Rune>();
			var incoming = monster != null ? Incoming() : Array.Empty<Rune>();
			RefreshRing(RuneSwap.Apply(equipped, incoming), incoming);
			RefreshTrialBar();

			Layout.Clear(_sets);
			_stats.Visible = monster != null;
			if (monster == null)
			{
				_sets.Visible = false;
				return;
			}

			var summon = _database.Summon(monster.SummonId);
			var now = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, equipped);
			var next = incoming.Count > 0 ? SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, RuneSwap.Apply(equipped, incoming)) : null;
			_stats.Compare(now, next);
			RefreshSets(now, next);
		}

		/// <summary>O monstro numa linha: rosto, nome, estrelas e nível, e Trocar. Sem monstro, o convite para escolher.</summary>
		private void RefreshHead()
		{
			Layout.Clear(_head);
			if (Monster is not { } monster)
			{
				_head.AddChild(GameButton.Of(T("runes.choose_monster"), ChooseMonster, ButtonKind.Primary, "monster", 44).Named("Choose"));
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
			RefreshDetail();
		});

		/// <summary>
		/// Os 6 espaços com as runas que o monstro teria: as que chegam (a prova e a escolhida) ficam
		/// destacadas no lugar das que sairiam. Tocar num espaço filtra a lista por ele.
		/// </summary>
		private void RefreshRing(IReadOnlyList<Rune> shown, IReadOnlyCollection<Rune> incoming)
		{
			var tiles = new List<Control>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(shown.FirstOrDefault(r => r.Slot == slot), slot) { Name = $"Slot{slot}", OpenedLevel = OpenedLevel };
				tile.SetSelected(tile.Rune != null ? tile.Rune.Id == _selectedRune || incoming.Contains(tile.Rune) : _filter.Slots.Contains(slot));
				tile.Pressed += t =>
				{
					_filter = _filter with { Slots = new[] { t.Slot } };
					_selectedRune = t.Rune?.Id;
					_tab = 0;
					Refresh();
				};
				tiles.Add(tile);
			}

			_ring.Set(null, tiles);
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

		/// <summary>A faixa da prova, só quando há runas nela: Equipar todas de uma vez, ou Limpar.</summary>
		private void RefreshTrialBar()
		{
			Layout.Clear(_trialBar);
			_trialBar.Visible = _trial.Count > 0 && Monster != null;
			if (!_trialBar.Visible)
				return;

			var label = new Label { Name = "Text", Text = T("runes.trial"), ThemeTypeVariation = GameTheme.Faded, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			_trialBar.AddChild(label);
			var monsterId = Monster!.Id;
			var equip = GameButton.Of(T("runes.trial_equip", _trial.Count), () => EquipManyRequested?.Invoke(_trial.Values.ToList(), monsterId), ButtonKind.Primary, null, 36).Wide(108).Named("Equip");
			equip.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			_trialBar.AddChild(equip);
			var clear = GameButton.Of(T("runes.trial_clear"), () =>
			{
				_trial.Clear();
				RefreshLeft();
				RefreshDetail();
			}, ButtonKind.Secondary, null, 36).Wide(84).Named("Clear");
			clear.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			_trialBar.AddChild(clear);
		}

		/// <summary>
		/// O que chegaria ao monstro: só a prova (Provar). Escolher uma runa na lista só mostra a ficha dela; ela
		/// só entra no espaço com Equipar ou Provar.
		/// </summary>
		private IReadOnlyList<Rune> Incoming() => _trial.Values.Select(id => _player.Runes.First(r => r.Id == id)).ToList();

		private Rune? Selected => _selectedRune is { } id ? _player.Runes.FirstOrDefault(r => r.Id == id) : null;

		/// <summary>Tira da prova a runa que sumiu, que já está no monstro, ou tudo, sem monstro.</summary>
		private void PruneTrial()
		{
			foreach (var (slot, id) in _trial.ToList())
			{
				if (Monster is not { } monster || _player.Runes.FirstOrDefault(r => r.Id == id) is not { } rune || rune.EquippedOn == monster.Id)
					_trial.Remove(slot);
			}
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
			_middle.AddChild(Toolbar());

			if (_selecting)
			{
				_selection = new VBoxContainer { Name = "Selection" };
				_selection.AddThemeConstantOverride("separation", Space.Small);
				_middle.AddChild(_selection);
				RefreshSelection();
			}

			// Só as pedras que aparecem na rolagem existem como nós (o inventário passa de 800): as outras são
			// montadas quando chegam e soltas quando saem; o estado (escolhida, marcada) vem do que a tela guarda.
			var grid = new TileGrid(8) { Name = "Grid" };
			grid.Released += (index, _) => _tiles.Remove(runes[index].Id);
			grid.Virtualize(runes.Count, RuneTile.TileSize, index => NewTile(runes[index]));

			if (runes.Count == 0)
				_middle.AddChild(Layout.Text(T("runes.none_found"), GameTheme.Faded, 400).Named("Empty"));
			_listScroll = Layout.Scroll(grid);
			_middle.AddChild(_listScroll);
		}

		/// <summary>O nível da runa quando a tela abriu: a ficha do toque longo põe em verde o que ela ganhou desde então.</summary>
		private int? OpenedLevel(Rune rune) => _levelWhenOpened.GetValueOrDefault(rune.Id, rune.Level);

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
		/// O toque numa runa da grade: vendendo várias, marca ou desmarca a solta; senão, escolhe. Só as
		/// pedras tocadas, a faixa da seleção, a coluna da esquerda e a ficha mudam; a grade fica.
		/// </summary>
		private void Tap(Rune rune)
		{
			if (_selecting && rune.EquippedOn == null && !rune.Locked)
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
			RefreshLeft();
			RefreshDetail();
		}

		/// <summary>Marca ou desmarca várias de uma vez, nas pedras que já estão na grade.</summary>
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
        /// Os botões do inventário numa grade de 2: Filtros (com quantos ligados) e Ordenar; Onde e Vender
        /// várias. Duas colunas para "Ordem: estrelas" caber numa linha; quantas runas a busca achou fica na
        /// fileira das abas.
        /// </summary>
        private Control Toolbar()
        {
            var grid = new GridContainer() { Name = "Toolbar", SizeFlagsHorizontal = SizeFlags.ExpandFill, Columns = 2 };
            ConfigureGrid(grid, 8);
            var active = ActiveFilters();
            var filters = GameButton.Of(active == 0 ? T("filter.button") : T("filter.button_active", active), OpenFilters, active == 0 ? ButtonKind.Secondary : ButtonKind.Primary, "search", 40).Named("Filters");
            grid.AddChild(filters);

            var current = _filter.Sort == RuneSort.Substat ? SubstatOrder + (int)_filter.SortStat : (int)_filter.Sort;
            var sort = new ChoiceButton(T("filter.sort"), SortOptions(), current, 40) { Name = "Sort" };
            sort.Changed += value =>
            {
                _filter = value >= SubstatOrder
                    ? _filter with { Sort = RuneSort.Substat, SortStat = (RuneStat)(value - SubstatOrder) }
                    : _filter with { Sort = (RuneSort)value };
                Callable.From(Refresh).CallDeferred();
            };
            grid.AddChild(sort);

            var place = new ChoiceButton(T("filter.where"), Enum.GetValues<RunePlace>().Select(p => (new Choice(T($"filter.place.{p}")), (int)p)).ToList(), (int)_place, 40) { Name = "Place" };
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
            }, _selecting ? ButtonKind.Primary : ButtonKind.Secondary, "select", 40).Named("Select"));
            return grid;
        }

        private int ActiveFilters() =>
			(_filter.Sets.Count > 0 ? 1 : 0) + (_filter.Slots.Count > 0 ? 1 : 0) + (_filter.Mains.Count > 0 ? 1 : 0) + (_filter.Substats.Count > 0 ? 1 : 0)
			+ (_filter.MinGrade > 1 ? 1 : 0) + (_filter.MinRarity > RuneRarity.Normal ? 1 : 0) + (_filter.MinLevel > 0 ? 1 : 0) + (_filter.Condition != null ? 1 : 0);

		/// <summary>As ordens da grade e, depois, uma por subatributo (o valor dele fica escrito em cada pedra).</summary>
		private static IReadOnlyList<(Choice Choice, int Value)> SortOptions() =>
			Enum.GetValues<RuneSort>().Where(s => s != RuneSort.Substat).Select(s => (new Choice(Texts.Name(s)), (int)s))
				.Concat(Enum.GetValues<RuneStat>().Select(s => (new Choice(T("filter.order_substat", Texts.Label(s).ToLower(Culture)), Symbol: Texts.PlanetOf(s)), SubstatOrder + (int)s)))
				.ToList();

		/// <summary>A janela dos filtros (<see cref="FilterDialog"/>): um campo por linha. Muda na hora.</summary>
		private void OpenFilters() => FilterDialog.Open(this, T("filter.title"), dialog =>
		{
			// Conjunto, espaço e principal aceitam vários (velocidade e crítico): basta a runa ser de um deles.
			dialog.MultiField("Set", T("filter.set"), RuneSets.ActiveSets.Select(s => (new Choice(Texts.Name(s), Symbol: RuneSets.PlanetOf(s)), (int)s)), _filter.Sets.Select(s => (int)s),
				values => _filter = _filter with { Sets = values.Select(v => (RuneSet)v).ToList() });
			dialog.MultiField("Slot", T("filter.slot"), Enumerable.Range(1, RuneRules.Slots).Select(s => (new Choice(T("filter.slot_n", s)), s)), _filter.Slots,
				values => _filter = _filter with { Slots = values });
			dialog.MultiField("Main", T("filter.main"), Enum.GetValues<RuneStat>().Select(s => (new Choice(Texts.Label(s), Symbol: Texts.PlanetOf(s)), (int)s)), _filter.Mains.Select(s => (int)s),
				values => _filter = _filter with { Mains = values.Select(v => (RuneStat)v).ToList() });
			// Vários subatributos: a runa precisa ter todos. Procurar já ordena pelo primeiro, a maior primeiro (só
			// ao começar a procurar ou se o que ordenava saiu); a Ordem ainda muda depois.
			dialog.MultiField("Substat", T("filter.substat"), Enum.GetValues<RuneStat>().Select(s => (new Choice(Texts.Label(s), Symbol: Texts.PlanetOf(s)), (int)s)), _filter.Substats.Select(s => (int)s),
				values =>
				{
					var stats = values.Select(v => (RuneStat)v).ToList();
					var sorting = _filter.Sort == RuneSort.Substat && stats.Contains(_filter.SortStat);
					_filter = stats.Count == 0 || sorting || (_filter.Substats.Count > 0 && _filter.Sort != RuneSort.Substat)
						? _filter with { Substats = stats }
						: _filter with { Substats = stats, Sort = RuneSort.Substat, SortStat = stats[0] };
				});
			dialog.Field("Stars", T("filter.stars"), Enumerable.Range(2, RuneRules.MaxGrade - 1).Select(g => (new Choice(T("filter.at_least", Texts.Stars(g))), g)), _filter.MinGrade > 1 ? _filter.MinGrade : FilterDialog.All,
				value => _filter = _filter with { MinGrade = Math.Max(1, value) });
			dialog.Field("Rarity", T("filter.rarity"), Enum.GetValues<RuneRarity>().Skip(1).Select(r => (new Choice(T("filter.at_least", Texts.Name(r)), Art.Icon("gem"), Palette.Of(r)), (int)r)), _filter.MinRarity > RuneRarity.Normal ? (int)_filter.MinRarity : FilterDialog.All,
				value => _filter = _filter with { MinRarity = value < 0 ? RuneRarity.Normal : (RuneRarity)value });
			dialog.Field("Upgrade", T("filter.upgrade"), new[] { 3, 6, 9, 12, 15 }.Select(l => (new Choice(T("filter.at_least", $"+{l}")), l)), _filter.MinLevel > 0 ? _filter.MinLevel : FilterDialog.All,
				value => _filter = _filter with { MinLevel = Math.Max(0, value) });
			dialog.Field("Condition", T("filter.condition"), Enum.GetValues<RuneCondition>().Select(c => (new Choice(T($"filter.rune_condition_kind.{c}")), (int)c)), _filter.Condition is { } condition ? (int)condition : FilterDialog.All,
				value => _filter = _filter with { Condition = value < 0 ? null : (RuneCondition)value });
		}, Refresh, () => _filter = new RuneFilter { Sort = _filter.Sort, SortStat = _filter.SortStat });

		/// <summary>A faixa da seleção: a explicação numa linha só dela (quebra em vez de alargar a tela) e os botões embaixo.</summary>
		private void RefreshSelection()
		{
			if (_selection == null || !IsInstanceValid(_selection))
				return;

			Layout.Clear(_selection);
			var marked = _player.Runes.Where(r => _marked.Contains(r.Id)).ToList();
			_selection.AddChild(Layout.Text(T("runes.select_hint", marked.Count)).Named("Hint"));
			var row = Layout.Grid(3, 8).Named("Buttons");
			_selection.AddChild(row);
			row.AddChild(GameButton.Of(T("runes.mark_all"), () => Mark(_visible.Where(r => r.EquippedOn == null && !r.Locked), true),
				ButtonKind.Secondary, null, 40).Named("MarkAll"));
			var unmark = GameButton.Of(T("runes.unmark"), () => Mark(marked, false), ButtonKind.Secondary, null, 40).Named("Unmark");
			unmark.Block(marked.Count == 0 ? T("runes.why_marked") : null, T("runes.unmark"));
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
					_selecting = false;
					SellManyRequested?.Invoke(ids);
				}, ButtonKind.Danger), ButtonKind.Danger, "dismantle", 40).Named("SellMarked");
			if (marked.Count > 0)
				sell.WithCost("essence", $"+{value}");
			sell.Block(marked.Count == 0 ? T("runes.why_marked") : null, T("runes.sell_button_short"));
			row.AddChild(sell);
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
			list.AddThemeConstantOverride("separation", Space.Small);
			for (var i = 0; i < groups.Count; i++)
			{
				var group = groups[i];
				var row = Layout.Row(Space.Regular).Named($"Tool{i + 1}");
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
			RefreshForge();
			var rune = _player.Runes.FirstOrDefault(r => r.Id == _selectedRune);

			// A ficha que está refazendo a melhora desta runa termina o que começou: um refresh no meio dela
			// (qualquer que seja a causa) não a troca por uma nova, que cortaria a animação.
			var running = rune != null && _detail.GetChildCount() > 0 && _detail.GetChild(0) is RuneCard { Animating: true } card && card.Shows(rune) ? card : null;
			if (running == null)
			{
				Layout.Clear(_detail);
			}
			else
			{
				foreach (var child in _detail.GetChildren().Where(c => c != running).ToList())
				{
					_detail.RemoveChild(child);
					child.QueueFree();
				}
			}

			Layout.Clear(_actions);
			_actions.Visible = rune != null;
			if (rune == null)
			{
				return;
			}

			var opened = _levelWhenOpened.GetValueOrDefault(rune.Id, rune.Level);

			// A runa voltou atrás (a Gema de Reavaliação a devolve ao estado em que caiu): o que ela era antes
			// deixa de contar, e as melhoras seguintes partem daqui, com verde e animação.
			if (!_levelWhenOpened.ContainsKey(rune.Id) || rune.Level < opened)
			{
				opened = rune.Level;
				_levelWhenOpened[rune.Id] = opened;
			}

			// Refeita no mesmo quadro do pedido de melhora (a tela pode refazer a ficha mais de uma vez nele).
			int? animateFrom = _upgrading is { } upgrading && upgrading.Rune == rune.Id && Engine.GetProcessFrames() - upgrading.Frame <= 1 ? upgrading.Level : null;
			if (running == null)
				_detail.AddChild(new RuneCard(rune, 300, opened, animateFrom: animateFrom));
			if (rune.EquippedOn is { } owner && _player.Monster(owner) is { } holder)
				_detail.AddChild(OwnerRow(holder));

			// Cada ação tem a sua célula na grade e ela nunca muda, todas da mesma altura: o que não vale agora
			// fica no lugar, apagado, e o toque diz o motivo.
			var cells = new Control[ActionCells];
			var full = RuneInventory.IsFull(_player);
			var here = rune.EquippedOn != null && (rune.EquippedOn == _monsterId || Monster == null);
			if (here)
			{
				cells[CellEquip] = GameButton.Of(T("runes.remove"), () => UnequipRequested?.Invoke(rune.Id), ButtonKind.Secondary, "cancel", ActionHeight)
					.Block(full ? T("runes.inventory_full", RuneInventory.Capacity) : null, T("runes.remove")).Named("Unequip");
			}
			else
			{
				var monster = Monster;
				cells[CellEquip] = GameButton.Of(T("runes.equip_on"), () =>
				{
					_trial.Remove(rune.Slot);
					EquipRequested?.Invoke(rune.Id, monster!.Id);
				}, ButtonKind.Primary, "confirm", ActionHeight).Block(monster == null ? T("runes.why_no_monster") : null, T("runes.equip_on")).Named("Equip");
			}

			var trying = _trial.TryGetValue(rune.Slot, out var tried) && tried == rune.Id;
			cells[CellTry] = GameButton.Of(trying ? T("runes.untry") : T("runes.try"), () =>
			{
				if (trying)
					_trial.Remove(rune.Slot);
				else
					_trial[rune.Slot] = rune.Id;
				RefreshLeft();
				RefreshDetail();
			}, ButtonKind.Secondary, trying ? "cancel" : "stats", ActionHeight)
				.Block(Monster == null ? T("runes.why_no_monster") : here ? T("runes.why_try_equipped") : null, T("runes.try")).Named("Try");

			var maxed = rune.Level >= RuneRules.MaxLevel;
			var next = maxed ? 0 : RuneRules.UpgradeCost(rune);
			cells[CellUpgrade] = GameButton.Of(T("runes.upgrade_one", 1), () => Upgrade(rune, rune.Level + 1), ButtonKind.Primary, "level_max", ActionHeight)
				.WithCost("essence", maxed ? "" : Texts.Number(next))
				.Block(maxed ? T("runes.why_max", RuneRules.MaxLevel) : _player.Essence < next ? T("runes.why_essence") : null, T("runes.upgrade_one", 1)).Named("Upgrade");

			// O próximo marco (+3, +6, +9, +12 ou +15) vale na hora, sem pergunta.
			var milestone = maxed ? RuneRules.MaxLevel : RuneRules.NextMilestone(rune.Level);
			var milestoneCost = milestone > rune.Level + 1 ? RuneRules.UpgradeCost(rune, milestone) : 0;
			cells[CellMilestone] = GameButton.Of(T("runes.upgrade_button", milestone), () => Upgrade(rune, milestone), ButtonKind.Secondary, "level_max", ActionHeight)
				.WithCost("essence", milestoneCost > 0 ? Texts.Number(milestoneCost) : "")
				.Block(maxed ? T("runes.why_max", RuneRules.MaxLevel) : milestone <= rune.Level + 1 ? T("runes.why_same") : _player.Essence < milestoneCost ? T("runes.why_essence") : null, T("runes.upgrade_button", milestone)).Named("UpgradeToMilestone");

			// O +12 direto, pulando os marcos que faltam (de +8 para baixo): o gasto é grande, então pergunta.
			// De +9 em diante o próximo marco já é o +12, e o botão de cima vai direto, sem pergunta.
			var twelveOpen = !maxed && milestone < DirectLevel;
			var twelveCost = twelveOpen ? RuneRules.UpgradeCost(rune, DirectLevel) : 0;
			cells[CellTwelve] = GameButton.Of(T("runes.upgrade_button", DirectLevel),
					() => RuneDialog.ConfirmDirect(this, rune, _player.Essence, () => UpgradeDirect(rune)), ButtonKind.Secondary, "level_max", ActionHeight)
				.WithCost("essence", twelveOpen ? Texts.Number(twelveCost) : "")
				.Block(!twelveOpen ? T("runes.why_twelve", DirectLevel) : _player.Essence < twelveCost ? T("runes.why_essence") : null, T("runes.upgrade_button", DirectLevel)).Named("UpgradeToTwelve");

			cells[CellLock] = GameButton.Of(rune.Locked ? T("lock.unlock") : T("lock.lock"), () => LockRequested?.Invoke(rune.Id), ButtonKind.Secondary, rune.Locked ? "unlock" : "lock", ActionHeight).Named("Lock");
			cells[CellReappraise] = Reappraise(rune);
			cells[CellSpare] = new Control { Name = "Spare", CustomMinimumSize = new Vector2(0, ActionHeight), MouseFilter = MouseFilterEnum.Ignore };

			var value = RuneRules.SellValue(rune);
			cells[CellSell] = GameButton.Of(T("runes.sell_button_short"), () => Dialog.Confirm(this, T("runes.sell_title"), T("runes.sell_confirm", value), T("runes.sell_button_short"),
					() => SellRequested?.Invoke(rune.Id), ButtonKind.Danger), ButtonKind.Danger, "dismantle", ActionHeight)
				.WithCost("essence", $"+{value}")
				.Block(rune.Locked ? T("runes.locked_note") : rune.EquippedOn != null ? T("runes.why_sell_equipped") : null, T("runes.sell_button_short")).Named("Sell");

			cells[CellForge] = GameButton.Of(T("runes.forge"), OpenForge, ButtonKind.Secondary, "grindstone", ActionHeight)
				.Block(Forgeable(rune) ? null : T("runes.why_no_forge"), T("runes.forge")).Named("Forge");

			foreach (var cell in cells)
			{
				if (cell is GameButton button)
					button.FixedHeight();
				_actions.AddChild(cell);
			}
		}

		// As células da grade de ações, na ordem de sempre (duas colunas).
		// Equipar | Provar, e abaixo as fileiras do dono: Evoluir +1 | vazio, Até +X | Até +12, Reavaliar | Afiar, Bloquear | Vender.
		private const int CellEquip = 0;
		private const int CellTry = 1;
		private const int CellUpgrade = 2;
		private const int CellSpare = 3;
		private const int CellMilestone = 4;
		private const int CellTwelve = 5;
		private const int CellReappraise = 6;
		private const int CellForge = 7;
		private const int CellLock = 8;
		private const int CellSell = 9;
		private const int ActionCells = 10;

		/// <summary>Pede a melhora. A ficha da runa, refeita logo em seguida, mostra os níveis subindo.</summary>
		private void Upgrade(Rune rune, int target)
		{
			Upgraded(rune.Id, rune.Level);
			UpgradeRequested?.Invoke(rune.Id, target);
		}

		/// <summary>
		/// A runa vai ser (ou acabou de ser) melhorada a partir de <paramref name="fromLevel"/>, por aqui ou por uma
		/// janela por cima (a Batalha automática): a ficha dela, refeita no mesmo quadro, mostra os níveis subindo.
		/// </summary>
		public void Upgraded(int runeId, int fromLevel) => _upgrading = (runeId, fromLevel, Engine.GetProcessFrames());

		/// <summary>
		/// Melhora até o +12 e, se a runa mudou, abre o antes e o depois. O pedido é síncrono: quando ele volta, a
		/// runa já está no nível novo, e a cópia tirada antes guarda como ela era.
		/// </summary>
		private void UpgradeDirect(Rune rune)
		{
			var before = rune.Clone();
			Upgrade(rune, DirectLevel);
			if (_player.Runes.FirstOrDefault(r => r.Id == rune.Id) is { } after && after.Level > before.Level)
				RuneDialog.ShowUpgraded(this, before, after);
		}

		/// <summary>Quem usa a runa escolhida: o rosto dele (toque longo abre o resumo) e "Equipada em ...".</summary>
		private Control OwnerRow(OwnedSummon holder)
		{
			var summon = _database.Summon(holder.SummonId);
			var row = Layout.Row(Space.Regular).Named("Owner");
			row.AddChild(Portrait(summon, holder, 36));
			var name = Layout.Text(T(holder.Stored ? "rune.owner_vault" : "rune.owner", summon.NameFor(holder.Awakened)), GameTheme.Faded, 240).Named("Name");
			name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			row.AddChild(name);
			return row;
		}

		/// <summary>O rosto do monstro num círculo com a moldura da raridade; toque longo abre o resumo dele.</summary>
		private static Control Portrait(SummonDefinition summon, OwnedSummon monster, float size)
		{
			var portrait = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Stop };
			portrait.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 2, (int)(size / 2), 3));
			portrait.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Circle, boil: false, aura: monster.Awakened ? summon.Element : null));
			Press.On(portrait, null, () => MonsterSummary.Open(portrait, summon, monster));
			return portrait;
		}

		/// <summary>
		/// Reavaliar: sempre no lugar. Numa runa que não mudou desde que caiu, ou sem gema, fica apagado e diz o
		/// motivo; com, pergunta antes, listando tudo o que vai ser desfeito.
		/// </summary>
		private GameButton Reappraise(Rune rune)
		{
			var changes = RuneReappraisal.Preview(rune);
			var gems = _player.ReappraisalGems;
			var button = GameButton.Of(T("runes.reappraise", gems), () => Dialog.Confirm(this, T("runes.reappraise_title"), ReappraisalText(changes, gems),
				T("runes.reappraise_button"), () => ReappraiseRequested?.Invoke(rune.Id), ButtonKind.Danger), ButtonKind.Secondary, "gem", ActionHeight).Named("Reappraise");
			return button.Block(!changes.Any ? T("runes.why_no_reappraise") : gems == 0 ? T("runes.reappraise_none") : null, T("runes.reappraise_title"));
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

		/// <summary>Alguma pedra do jogador serve em algum subatributo da runa.</summary>
		private bool Forgeable(Rune rune) => Enumerable.Range(0, rune.Substats.Count)
			.Any(index => _player.Tools.Any(t => RuneForge.CanGrind(rune, index, t) || RuneForge.CanEnchant(rune, index, t)));

		/// <summary>Afiar e Encantar: a ficha da runa escolhida numa janela, com as pedras na linha de cada subatributo.</summary>
		private void OpenForge()
		{
			var dialog = Dialog.Open(this, T("runes.forge"), 480, null, "ForgeDialog");
			dialog.Closed += () =>
			{
				if (_forge == dialog)
					_forge = null;
			};
			_forge = dialog;
			RefreshForge();
		}

		/// <summary>
		/// Refaz a janela de Afiar e Encantar com a runa como está agora (a pedra que acabou de usar já
		/// aparece no valor); sem runa escolhida, ela fecha.
		/// </summary>
		private void RefreshForge()
		{
			if (_forge == null)
				return;
			if (Selected is not { } rune)
			{
				_forge.Close();
				return;
			}

			Layout.Clear(_forge.Body);
			_forge.Body.AddChild(Layout.Text(T("runes.forge_hint"), GameTheme.Faded, 440).Named("Hint"));
			_forge.Body.AddChild(new RuneCard(rune, 440, _levelWhenOpened.GetValueOrDefault(rune.Id, rune.Level), index => SubstatTools(rune, index)));
		}

		/// <summary>Os botões das pedras que servem num subatributo, no fim da linha dele na janela de Afiar e Encantar.</summary>
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

        private void ConfigureGrid(GridContainer container, int separation)
        {
            container.AddThemeConstantOverride("h_separation", separation);
            container.AddThemeConstantOverride("v_separation", separation);
        }
    }
}
