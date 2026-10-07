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
	/// Monstros, no jeito do Summoners War: à esquerda a grade de cartões, com as abas Coleção e Baú e o
	/// botão de selecionar vários; à direita a ficha do escolhido.
	///
	/// A ficha tem duas colunas. Na estreita, à esquerda, a régua de abas escritas em pé (Atributos,
	/// Habilidades, Despertar, Runas) e, embaixo dela, o que se faz com o monstro em qualquer aba:
	/// Favoritar, Bloquear, Baú e Soltar. Na larga, o retrato, as estrelas, o elemento, o nível e a aba:
	/// - Atributos: subir nível (ou evoluir), cada botão dizendo quanto custa, e a ficha (base + runas).
	/// - Habilidades: cada uma com a recarga, o nível e o que os próximos níveis dão; a Liderança; e
	///   Fundir cópias, que abre a janela própria da fusão (<see cref="FusionDialog"/>).
	/// - Despertar: o desenho de agora e o desperto, o que ganha e o botão Despertar com o custo.
	/// - Runas: as 6 no círculo, os conjuntos ativos e o botão que abre a tela de Runas.
	///
	/// Embaixo das abas, Filtros e Ordem (<see cref="MonsterFilter"/>): elemento, papel, estrelas, estrelas
	/// naturais, Despertar, situação e as habilidades (o que fazem, em que escalam, que efeito põem: só as
	/// opções que algum monstro tem, <see cref="SkillTraits"/>); a ordem por estrelas, nível, elemento, nome, chegada ou por um
	/// atributo (com as runas), que então aparece escrito em cada cartão. Os favoritos vêm antes em
	/// qualquer ordem. O GameRoot guarda a busca enquanto o jogo está aberto (<see cref="FilterChanged"/>).
	///
	/// Selecionar vários marca cartões (nas duas abas, qualquer monstro, bloqueado ou não) para guardar no
	/// Baú, tirar dele ou soltar de uma vez; soltar deixa de fora os bloqueados. Fundir é só pela janela
	/// da fusão, para uma escolha não se confundir com a outra. Mudar o filtro desmarca os que somem da
	/// grade, para não soltar nada que não se vê. Toque longo em qualquer cartão abre o resumo. Cada botão vira um evento; o GameRoot aplica a regra e
	/// chama <see cref="Refresh"/>.
	/// </summary>
	public partial class StorageScreen : Control
	{
		private const float DetailWidth = 610;

		/// <summary>Cabe o botão mais comprido (Desfavoritar): a ficha não muda de largura ao favoritar.</summary>
		private const float SideWidth = 180;

		/// <summary>Os cartões da grade: cinco por linha ao lado da ficha.</summary>
		private const float CardWidth = 98;

		/// <summary>A largura do texto das habilidades na coluna larga da ficha.</summary>
		private const float TextWidth = 300;

		/// <summary>Na lista da Ordem, as por atributo valem isto mais o atributo; as outras, a própria <see cref="MonsterSort"/>.</summary>
		private const int StatOrder = 100;

		private enum Page
		{
			Stats,
			Skills,
			Awaken,
			Runes,
		}

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private int? _selected;
		private bool _showStorage;
		private bool _selecting;
		private Page _page;
		private MonsterFilter _filter;
		private readonly HashSet<int> _marked = new();

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _tools = Layout.Row(10).Named("Tools");
		private readonly GridContainer _search = Layout.Grid(2, 8).Named("Search");
		private readonly VBoxContainer _selection = new() { Name = "Selection" };
		private readonly TileGrid _roster = new(10) { Name = "Roster" };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly TextTabs _pages = new(vertical: true, 64) { Name = "Pages" };
		private readonly VBoxContainer _sideActions = new() { Name = "Actions" };
		private ScrollContainer _rosterScroll = null!;

		/// <summary>O cartão que o jogador acabou de tocar: a rolagem o mostra inteiro.</summary>
		private int? _touched;

		/// <param name="filter">A busca da última vez (nula: sem filtro, por estrelas).</param>
		public StorageScreen(GameDatabase database, PlayerState player, int? selected, MonsterFilter? filter = null)
		{
			_database = database;
			_player = player;
			_filter = filter ?? new MonsterFilter();
			_selected = player.Monster(selected ?? -1)?.Id ?? player.Collection.FirstOrDefault()?.Id;
			_showStorage = player.Monster(_selected ?? -1)?.Stored ?? false;
		}

		/// <summary>Id e se é até o nível máximo (falso = só o próximo nível).</summary>
		public event Action<int, bool>? InfuseRequested;

		public event Action<int>? AwakenRequested;
		public event Action<int>? EvolveRequested;
		public event Action<int>? RunesRequested;
		public event Action<int>? StoreRequested;
		public event Action<int>? RetrieveRequested;

		/// <summary>Alvo e materiais, na ordem em que sobem habilidades.</summary>
		public event Action<int, IReadOnlyList<int>>? FuseRequested;

		public event Action<IReadOnlyList<int>>? ReleaseRequested;

		/// <summary>Os marcados da coleção vão para o Baú.</summary>
		public event Action<IReadOnlyList<int>>? StoreManyRequested;

		/// <summary>Os marcados do Baú voltam para a coleção (os que couberem).</summary>
		public event Action<IReadOnlyList<int>>? RetrieveManyRequested;

		/// <summary>Bloquear ou desbloquear o monstro (bloqueado não se solta nem vira material de fusão).</summary>
		public event Action<int>? LockRequested;

		/// <summary>Favoritar ou desfavoritar o monstro (favorito aparece antes nas listas).</summary>
		public event Action<int>? FavoriteRequested;

		public event Action? BackRequested;

		/// <summary>O jogador mudou o filtro ou a ordem.</summary>
		public event Action<MonsterFilter>? FilterChanged;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Monsters"), _currencies, () => BackRequested?.Invoke()).Header);

			var body = Layout.Row(16).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var rosterPanel = new PanelContainer { Name = "Collection", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer { Name = "Column" };
			rosterColumn.AddThemeConstantOverride("separation", 10);
			rosterPanel.AddChild(rosterColumn);
			rosterColumn.AddChild(_tools);
			rosterColumn.AddChild(_search);
			rosterColumn.AddChild(_selection);
			_rosterScroll = Layout.Scroll(_roster);
			rosterColumn.AddChild(_rosterScroll);
			body.AddChild(rosterPanel);

			var detailPanel = new PanelContainer { Name = "Sheet", CustomMinimumSize = new Vector2(DetailWidth, 0) };
			var detailRow = Layout.Row(14).Named("Row");

			var side = new VBoxContainer { Name = "Side", CustomMinimumSize = new Vector2(SideWidth, 0) };
			side.AddThemeConstantOverride("separation", 22);
			_pages.Add(T("monsters.page.stats"), "", "stats").Name = nameof(Page.Stats);
			_pages.Add(T("monsters.page.skills"), "", "skill").Name = nameof(Page.Skills);
			_pages.Add(T("monsters.page.awaken"), "", "awaken").Name = nameof(Page.Awaken);
			_pages.Add(T("monsters.page.runes"), "", "rune").Name = nameof(Page.Runes);
			_pages.Changed += index =>
			{
				_page = (Page)index;
				RefreshDetail();
			};
			side.AddChild(_pages);
			_sideActions.AddThemeConstantOverride("separation", 10);
			side.AddChild(_sideActions);

			_detail.AddThemeConstantOverride("separation", 12);
			detailRow.AddChild(Layout.Scroll(_detail));
			detailRow.AddChild(side);
			detailPanel.AddChild(detailRow);
			body.AddChild(detailPanel);

			Refresh();
		}

		public void Refresh()
		{
			if (_selected is { } id && _player.Monster(id) == null)
				_selected = _player.Collection.FirstOrDefault()?.Id;
			_marked.RemoveWhere(marked => _player.Monster(marked) == null);

			_currencies.Refresh(_player);
			RefreshTools();
			RefreshSelection();
			RefreshRoster();
			RefreshDetail();
		}

		// Abas e seleção ----------------------------------------------------------------------------

		private void RefreshTools()
		{
			Layout.Clear(_tools);
			var tabs = new TextTabs { Name = "Places" };
			tabs.Add(T("monsters.collection"), $"{_player.Collection.Count()}/{_player.CollectionCapacity}").Name = "Collection";
			tabs.Add(T("monsters.vault"), _player.Storage.Count().ToString()).Name = "Vault";
			tabs.Select(_showStorage ? 1 : 0);
			tabs.Changed += index =>
			{
				_showStorage = index == 1;
				Callable.From(Refresh).CallDeferred();
			};
			_tools.AddChild(tabs);
			_tools.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });

			var select = GameButton.Of(_selecting ? T("monsters.select_done") : T("monsters.select"), () =>
			{
				_selecting = !_selecting;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			}, _selecting ? ButtonKind.Primary : ButtonKind.Secondary, "select", 52).Named("Select");
			_tools.AddChild(select);

			Layout.Clear(_search);
			var active = _filter.Active;
			_search.AddChild(GameButton.Of(active == 0 ? T("filter.button") : T("filter.button_active", active), OpenFilters, active == 0 ? ButtonKind.Secondary : ButtonKind.Primary, "search", 44).Named("Filters"));
			var current = _filter.Sort == MonsterSort.Stat ? StatOrder + (int)_filter.SortStat : (int)_filter.Sort;
			var sort = new ChoiceButton(T("filter.sort"), SortOptions(), current, 44) { Name = "Sort" };
			sort.Changed += value => Filter(value >= StatOrder
				? _filter with { Sort = MonsterSort.Stat, SortStat = (Stat)(value - StatOrder) }
				: _filter with { Sort = (MonsterSort)value });
			_search.AddChild(sort);
		}

		/// <summary>As ordens da grade e, depois, uma por atributo.</summary>
		private static IReadOnlyList<(Choice Choice, int Value)> SortOptions() =>
			Enum.GetValues<MonsterSort>().Where(s => s != MonsterSort.Stat).Select(s => (new Choice(Texts.Name(s)), (int)s))
				.Concat(Enum.GetValues<Stat>().Select(s => (new Choice(Texts.Name(s).ToLower(Culture), Rune: Texts.GlyphOf(s)), StatOrder + (int)s)))
				.ToList();

		/// <summary>A janela dos filtros (<see cref="FilterDialog"/>): um campo por linha. Muda na hora.</summary>
		private void OpenFilters() => FilterDialog.Open(this, T("filter.title_monsters"), dialog =>
		{
			dialog.Field("Element", T("filter.element"), Enum.GetValues<Element>().Select(e => (new Choice(Texts.Name(e), Art.Element(e), Palette.Of(e)), (int)e)), _filter.Element is { } element ? (int)element : FilterDialog.All,
				value => _filter = _filter with { Element = value < 0 ? null : (Element)value });
			dialog.Field("Role", T("filter.role"), Enum.GetValues<Role>().Select(r => (new Choice(Texts.Name(r)), (int)r)), _filter.Role is { } role ? (int)role : FilterDialog.All,
				value => _filter = _filter with { Role = value < 0 ? null : (Role)value });
			dialog.Field("Stars", T("filter.stars"), Enumerable.Range(1, Growth.MaxStars).Select(s => (new Choice(Texts.Stars(s)), s)), _filter.Stars ?? FilterDialog.All,
				value => _filter = _filter with { Stars = value < 0 ? null : value });
			dialog.Field("Rarity", T("filter.natural"), _database.Summons.Select(s => s.Rarity).Distinct().OrderBy(r => r).Select(r => (new Choice(Texts.Stars(r)), r)), _filter.Rarity ?? FilterDialog.All,
				value => _filter = _filter with { Rarity = value < 0 ? null : value });
			dialog.Field("Awakening", T("filter.awakening"), new[] { (new Choice(T("filter.awakened")), 1), (new Choice(T("filter.not_awakened")), 0) }, _filter.Awakened is { } awakened ? (awakened ? 1 : 0) : FilterDialog.All,
				value => _filter = _filter with { Awakened = value < 0 ? null : value == 1 });
			dialog.Field("Condition", T("filter.condition"), Enum.GetValues<MonsterCondition>().Select(c => (new Choice(T($"filter.condition_kind.{c}")), (int)c)), _filter.Condition is { } condition ? (int)condition : FilterDialog.All,
				value => _filter = _filter with { Condition = value < 0 ? null : (MonsterCondition)value });

			// O que as habilidades fazem: só as opções que algum monstro do jogo tem.
			dialog.Field("Behavior", T("filter.behavior"), SkillTraits.BehaviorsIn(_database.Summons).Select(b => (new Choice(T($"filter.behavior_kind.{b}")), (int)b)), _filter.Behavior is { } behavior ? (int)behavior : FilterDialog.All,
				value => _filter = _filter with { Behavior = value < 0 ? null : (SkillBehavior)value });
			dialog.Field("Scaling", T("filter.scaling"), SkillTraits.ScalingsIn(_database.Summons).Select(s => (new Choice(T($"filter.scaling_kind.{s}")), (int)s)), _filter.Scaling is { } scaling ? (int)scaling : FilterDialog.All,
				value => _filter = _filter with { Scaling = value < 0 ? null : (SkillScaling)value });
			dialog.Field("Applies", T("filter.applies"), SkillTraits.StatusesIn(_database.Summons).Select(s => (new Choice(Texts.Name(s)), (int)s)), _filter.Applies is { } status ? (int)status : FilterDialog.All,
				value => _filter = _filter with { Applies = value < 0 ? null : (StatusKind)value });
		}, () => Filter(_filter), () => _filter = new MonsterFilter { Sort = _filter.Sort, SortStat = _filter.SortStat });

		/// <summary>A busca nova: desmarca quem saiu da grade, avisa o GameRoot e refaz a tela.</summary>
		private void Filter(MonsterFilter filter)
		{
			_filter = filter;
			_marked.RemoveWhere(id => _player.Monster(id) is not { } m || !_database.HasSummon(m.SummonId) || !filter.Matches(m, _database.Summon(m.SummonId), _player));
			FilterChanged?.Invoke(filter);
			Callable.From(Refresh).CallDeferred();
		}

		/// <summary>
		/// A faixa da seleção: o que fazer e quantos marcados, numa linha; embaixo, Desmarcar, Guardar no Baú
		/// (na aba do Baú, Tirar do Baú, até onde a coleção tiver vaga) e Soltar, só dos desbloqueados.
		/// </summary>
		private void RefreshSelection()
		{
			Layout.Clear(_selection);
			_selection.Visible = _selecting;
			if (!_selecting)
				return;

			var marked = _marked.Select(_player.Monster).OfType<OwnedSummon>().ToList();
			_selection.AddChild(new Label { Name = "Hint", Text = T("monsters.select_hint", marked.Count), AutowrapMode = TextServer.AutowrapMode.WordSmart });
			var buttons = Layout.Grid(3, 8).Named("Buttons");
			_selection.AddChild(buttons);

			var unmark = GameButton.Of(T("monsters.unmark"), () =>
			{
				_marked.Clear();
				Refresh();
			}, ButtonKind.Secondary, null, 48).Named("Unmark");
			unmark.Disabled = marked.Count == 0;
			buttons.AddChild(unmark);
			buttons.AddChild(_showStorage ? RetrieveMarked(marked) : StoreMarked(marked));

			buttons.AddChild(ReleaseMarked(marked));
		}

		/// <summary>Soltar os marcados desbloqueados: os bloqueados ficam (o botão conta só os que vão).</summary>
		private GameButton ReleaseMarked(IReadOnlyList<OwnedSummon> marked)
		{
			// O Núcleo de Infusão não se solta: só se funde.
			var released = marked.Where(m => !m.Locked && !m.IsInfusionCore).ToList();
			var fragments = released.Sum(m => Fusion.FragmentsFor(_database.Summon(m.SummonId).Rarity));
			var text = T("monsters.release_many_confirm", released.Count, fragments);
			if (released.Count < marked.Count)
				text += " " + T("monsters.release_many_locked", marked.Count - released.Count);
			var button = GameButton.Of(T("monsters.release_marked", released.Count), () => Dialog.Confirm(this,
				T("monsters.release_title"),
				text + MonsterNotes.Warning(_database, _player, released),
				T("monsters.release_button"),
				() => ReleaseRequested?.Invoke(released.Select(m => m.Id).ToList()), ButtonKind.Danger), ButtonKind.Danger, "release", 48).Named("ReleaseMarked");
			if (released.Count > 0)
				button.WithCost("fragments", $"+{fragments}");
			button.Disabled = released.Count == 0;
			return button;
		}

		/// <summary>Guardar os marcados da coleção no Baú. Se algum está em equipe, pergunta antes (ele sai dela).</summary>
		private GameButton StoreMarked(IReadOnlyList<OwnedSummon> marked)
		{
			var ids = marked.Where(m => !m.Stored).Select(m => m.Id).ToList();
			void Store()
			{
				_marked.ExceptWith(ids);
				StoreManyRequested?.Invoke(ids);
			}

			var inTeams = marked.Count(m => !m.Stored && MonsterNotes.Teams(_database, _player, m.Id).Count > 0);
			var button = GameButton.Of(T("monsters.store_marked", ids.Count), () =>
			{
				if (inTeams == 0)
					Store();
				else
					Dialog.Confirm(this, T("monsters.store_many_title"), T("monsters.store_many_confirm", ids.Count, inTeams), T("monsters.store_many_button"), Store);
			}, ButtonKind.Secondary, "chest", 48).Named("StoreMarked");
			button.Disabled = ids.Count == 0;
			return button;
		}

		/// <summary>Tirar os marcados do Baú: só os que cabem na coleção (o botão diz quantos).</summary>
		private GameButton RetrieveMarked(IReadOnlyList<OwnedSummon> marked)
		{
			var ids = marked.Where(m => m.Stored).Select(m => m.Id).Take(Roster.FreeSlots(_player)).ToList();
			var button = GameButton.Of(T("monsters.retrieve_marked", ids.Count), () =>
			{
				_marked.ExceptWith(ids);
				RetrieveManyRequested?.Invoke(ids);
			}, ButtonKind.Secondary, "storage", 48).Named("RetrieveMarked");
			button.Disabled = ids.Count == 0;
			return button;
		}

		private void RefreshRoster()
		{
			Layout.Clear(_roster);
			var place = (_showStorage ? _player.Storage : _player.Collection).ToList();
			var monsters = _filter.Apply(place, _database, _player).ToList();

			foreach (var monster in monsters)
			{
				var summon = _database.Summon(monster.SummonId);
				var inTeam = MonsterNotes.Teams(_database, _player, monster.Id).Count > 0;
				// Na ordem por atributo, o valor dele (com as runas) fica escrito no cartão.
				var value = _filter.Sort == MonsterSort.Stat ? Texts.Value(_filter.SortStat, MonsterFilter.Value(monster, summon, _player, _filter.SortStat)) : null;
				var card = new CreatureCard(summon, monster, CardWidth, inTeam ? "team" : null, value) { Name = $"Monster{monster.Id}" };
				card.SetSelected(monster.Id == _selected);
				card.SetMarked(_marked.Contains(monster.Id));
				card.Pressed += c =>
				{
					var clicked = c.Monster!.Id;
					_touched = clicked;
					if (_selecting)
					{
						if (!_marked.Remove(clicked))
							_marked.Add(clicked);
					}
					else
					{
						_selected = clicked;
					}

					Refresh();
				};
				_roster.AddChild(card);
				if (monster.Id == _touched)
					Layout.Reveal(_rosterScroll, card);
			}

			_touched = null;

			if (monsters.Count == 0)
			{
				var empty = place.Count > 0 ? "monsters.filter_empty" : _showStorage ? "monsters.vault_empty" : "monsters.collection_empty";
				_roster.AddChild(Layout.Text(T(empty), GameTheme.Faded, 400).Named("Empty"));
			}
		}

		// Ficha ------------------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			Layout.Clear(_sideActions);
			var hasMonster = _selected is { } id && _player.Monster(id) != null;
			_pages.Visible = hasMonster;
			_sideActions.Visible = hasMonster;
			if (!hasMonster)
			{
				_detail.AddChild(Layout.Text(T("monsters.none_selected"), GameTheme.Faded).Named("Empty"));
				return;
			}

			var monster = _player.Monster(_selected!.Value)!;
			var summon = _database.Summon(monster.SummonId);
			SideActions(summon, monster);
			if (monster.IsInfusionCore)
			{
				// O Núcleo não tem atributos, runas, habilidades nem Despertar: só o que ele é e como usar.
				_pages.Visible = false;
				_detail.AddChild(CoreDetail(summon, monster));
				return;
			}

			_detail.AddChild(Identity(summon, monster));
			switch (_page)
			{
				case Page.Stats:
					StatsPage(summon, monster);
					break;
				case Page.Skills:
					SkillsPage(summon, monster);
					break;
				case Page.Awaken:
					AwakenPage(summon, monster);
					break;
				default:
					RunesPage(summon, monster);
					break;
			}
		}

		/// <summary>Embaixo das abas, o que vale em qualquer aba: Favoritar, Bloquear, Baú e Soltar.</summary>
		private void SideActions(SummonDefinition summon, OwnedSummon monster)
		{
			var id = monster.Id;
			_sideActions.AddChild(Side(GameButton.Of(monster.Favorite ? T("monsters.unfavorite") : T("monsters.favorite"), () => FavoriteRequested?.Invoke(id), ButtonKind.Secondary, "favorite")).Named("Favorite"));
			_sideActions.AddChild(Side(GameButton.Of(monster.Locked ? T("lock.unlock") : T("lock.lock"), () => LockRequested?.Invoke(id), ButtonKind.Secondary, monster.Locked ? "unlock" : "lock")).Named("Lock"));

			if (!monster.Stored)
			{
				_sideActions.AddChild(Side(GameButton.Of(T("monsters.store_short"), () => StoreRequested?.Invoke(id), ButtonKind.Secondary, "chest")).Named("Store"));
			}
			else
			{
				var retrieve = Side(GameButton.Of(T("monsters.retrieve_short"), () => RetrieveRequested?.Invoke(id), ButtonKind.Secondary, "storage")).Named("Retrieve");
				retrieve.Disabled = Roster.IsFull(_player);
				_sideActions.AddChild(retrieve);
			}

			if (monster.IsInfusionCore)
				return;

			var fragments = Fusion.FragmentsFor(summon.Rarity);
			var release = GameButton.Of(T("monsters.release"), () => Dialog.Confirm(this,
				T("monsters.release_title"),
				T("monsters.release_confirm", summon.NameFor(monster.Awakened), monster.Level, fragments) + MonsterNotes.Warning(_database, _player, new[] { monster }),
				T("monsters.release_button"),
				() => ReleaseRequested?.Invoke(new[] { id }), ButtonKind.Danger), ButtonKind.Danger, "release").WithCost("fragments", $"+{fragments}");
			release.Disabled = monster.Locked;
			_sideActions.AddChild(Side(release).Named("Release"));
		}

		/// <summary>O Núcleo de Infusão: retrato, nome, onde está e para que serve (fundir em qualquer monstro).</summary>
		private VBoxContainer CoreDetail(SummonDefinition summon, OwnedSummon monster)
		{
			var column = new VBoxContainer { Name = "Core" };
			column.AddThemeConstantOverride("separation", 10);
			var row = Layout.Row(14).Named("Row");
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(112, 112) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Gold, 3, 10, 8));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Gold, MaskShape.Rounded, 6));
			row.AddChild(frame);
			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddChild(new Label { Name = "Name", Text = summon.Name, ThemeTypeVariation = GameTheme.Heading });
			var where = new List<string>();
			if (monster.Stored)
				where.Add(T("monsters.in_vault"));
			if (monster.Locked)
				where.Add(T("monsters.locked"));
			where.Add(T("monsters.core_count", _player.Monsters.Count(m => m.IsInfusionCore)));
			info.AddChild(Layout.Text(string.Join(" · ", where), GameTheme.Faded).Named("Where"));
			row.AddChild(info);
			column.AddChild(row);
			column.AddChild(Layout.Text(T("monsters.core_info"), null, TextWidth).Named("Info"));
			return column;
		}

		/// <summary>Os botões da coluna estreita ocupam a largura dela inteira.</summary>
		private static GameButton Side(GameButton button)
		{
			button.SizeFlagsHorizontal = SizeFlags.Fill;
			return button;
		}

		/// <summary>Retrato, nome, estrelas, elemento e papel, onde está, nível e experiência: igual em toda aba.</summary>
		private VBoxContainer Identity(SummonDefinition summon, OwnedSummon monster)
		{
			var column = new VBoxContainer { Name = "Identity" };
			column.AddThemeConstantOverride("separation", 6);
			var row = Layout.Row(14).Named("Row");
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(112, 112) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), 3, 10, 8));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Rounded, 6, aura: monster.Awakened ? summon.Element : null));
			row.AddChild(frame);

			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddThemeConstantOverride("separation", 4);
			var title = Layout.Row(8).Named("Title");
			var name = new Label { Name = "Name", Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			if (monster.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			title.AddChild(name);
			if (monster.Favorite)
				title.AddChild(Doodle.Icon(Art.Icon("favorite"), 22, Palette.Negative.Lightened(0.15f)).Named("Favorite"));
			info.AddChild(title);

			var stars = new Label { Name = "Stars", Text = Texts.Stars(monster.Stars) };
			stars.AddThemeColorOverride("font_color", Palette.Stars(monster.Awakened));
			stars.AddThemeFontSizeOverride("font_size", 20);
			info.AddChild(stars);

			var line = Layout.Row(8).Named("Line");
			line.AddChild(Doodle.Icon(Art.Element(summon.Element), 24, Palette.Of(summon.Element)).Named("Element"));
			var element = new Label { Name = "ElementName", Text = Texts.Name(summon.Element), VerticalAlignment = VerticalAlignment.Center };
			element.AddThemeColorOverride("font_color", Palette.Of(summon.Element));
			line.AddChild(element);
			line.AddChild(new Label { Name = "Role", Text = $"· {Texts.Name(summon.Role)}", VerticalAlignment = VerticalAlignment.Center });
			info.AddChild(line);
			row.AddChild(info);
			column.AddChild(row);

			var where = new List<string> { T("monsters.natural_stars", Texts.Stars(summon.Rarity)) };
			if (monster.Stored)
				where.Add(T("monsters.in_vault"));
			if (monster.Locked)
				where.Add(T("monsters.locked"));
			var teams = MonsterNotes.Teams(_database, _player, monster.Id);
			if (teams.Count > 0)
				where.Add(T("monsters.teams", string.Join(", ", teams)));
			column.AddChild(Layout.Text(string.Join(" · ", where), GameTheme.Faded).Named("Where"));

			var max = Leveling.MaxLevel(monster);
			var level = Layout.Row(10).Named("Level");
			level.AddChild(new Label { Name = "Text", Text = T("monsters.level", monster.Level, max), ThemeTypeVariation = GameTheme.Number });
			var bar = Layout.Energy(Palette.Gold, 12).Named("Experience");
			bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			bar.MaxValue = Math.Max(1, Leveling.ExperienceToNext(monster));
			bar.Value = Leveling.IsMaxLevel(monster) ? bar.MaxValue : monster.Experience;
			level.AddChild(bar);
			level.AddChild(new Label
			{
				Name = "ExperienceText",
				Text = Leveling.IsMaxLevel(monster) ? T("monsters.level_max") : T("monsters.experience", monster.Experience, Leveling.ExperienceToNext(monster)),
				ThemeTypeVariation = GameTheme.Faded,
			});
			column.AddChild(level);
			return column;
		}

		/// <summary>Subir até o máximo gasta muita Essência de uma vez: pergunta antes, com o gasto e o nível a que chega.</summary>
		private void ConfirmLevelMax(SummonDefinition summon, OwnedSummon monster, int full)
		{
			var spend = Math.Min(full, _player.Essence);
			var target = Leveling.LevelAfter(monster, spend);
			Dialog.Confirm(this, T("monsters.max_confirm_title", target),
				T("monsters.max_confirm", Texts.Number(spend), summon.NameFor(monster.Awakened), target, Texts.Number(_player.Essence)),
				T("monsters.max_level_button", target), () => InfuseRequested?.Invoke(monster.Id, true));
		}

		private void StatsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var id = monster.Id;
			var actions = Layout.Row(10).Named("Actions");

			if (!Leveling.IsMaxLevel(monster))
			{
				var (next, full) = Leveling.InfuseCosts(monster);
				var one = GameButton.Of(T("monsters.level_up"), () => InfuseRequested?.Invoke(id, false), ButtonKind.Primary, "essence").WithCost("essence", Texts.Number(next)).Named("LevelUp");
				one.Disabled = _player.Essence < next;
				one.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				actions.AddChild(one);
				var all = GameButton.Of(T("monsters.max_level_button", Leveling.MaxLevel(monster)), () => ConfirmLevelMax(summon, monster, full), ButtonKind.Secondary, "level_max").WithCost("essence", Texts.Number(full)).Named("LevelMax");
				all.Disabled = _player.Essence <= 0;
				all.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				actions.AddChild(all);
			}
			else if (Evolution.IsReady(monster))
			{
				var fragmentCost = Evolution.Cost(monster.Stars);
				var evolve = GameButton.Of(T("monsters.evolve", Texts.Stars(monster.Stars + 1)), () => EvolveRequested?.Invoke(id), ButtonKind.Primary, "evolve")
					.WithCost("essence", $"{fragmentCost} {T("currency.fragments")}").Named("Evolve");
				evolve.Disabled = !Evolution.CanEvolve(_player, monster);
				evolve.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				actions.AddChild(evolve);
			}

			if (actions.GetChildCount() > 0)
				_detail.AddChild(actions);
			if (monster.Locked)
				_detail.AddChild(Layout.Text(T("monsters.locked_note"), GameTheme.Faded).Named("Locked"));

			if (!Leveling.IsMaxLevel(monster))
				_detail.AddChild(Layout.Text(T("monsters.level_hint", Leveling.ExperiencePerEssence), GameTheme.Faded).Named("LevelHint"));
			else if (monster.Stars >= Growth.MaxStars)
				_detail.AddChild(Layout.Text(T("monsters.fully_grown"), GameTheme.Faded).Named("Grown"));
			if (Roster.IsFull(_player) && monster.Stored)
				_detail.AddChild(Layout.Text(T("monsters.collection_full"), GameTheme.Faded).Named("Full"));

			_detail.AddChild(new HSeparator { Name = "StatsLine" });
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, _player.RunesOn(monster.Id));
			var table = new StatTable { Name = "Stats" };
			table.Show(sheet);
			_detail.AddChild(table);
			_detail.AddChild(Layout.Text(T("monsters.stats_hint"), GameTheme.Faded).Named("StatsHint"));
		}

		private void RunesPage(SummonDefinition summon, OwnedSummon monster)
		{
			var runes = _player.RunesOn(monster.Id);
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, runes);

			var actions = Layout.Row(0, true).Named("Actions");
			actions.AddChild(GameButton.Of(T("monsters.runes_button"), () => RunesRequested?.Invoke(monster.Id), ButtonKind.Primary, "rune").Named("OpenRunes"));
			_detail.AddChild(actions);

			var ring = new SigilRing(280) { Name = "Ring", Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
			var tiles = new List<Control>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var tile = new RuneTile(runes.FirstOrDefault(r => r.Slot == slot), slot, 1.1f) { Name = $"Slot{slot}" };
				tile.Pressed += _ => { if (tile.Rune != null) RuneDialog.Show(tile, tile.Rune); };
				tiles.Add(tile);
			}

			ring.Set(null, tiles);
			_detail.AddChild(ring);

			// Conjunto de 2 peças pode estar ativo mais de uma vez: a posição deixa o nome único.
			for (var i = 0; i < sheet.Runes.ActiveSets.Count; i++)
			{
				var set = sheet.Runes.ActiveSets[i];
				var row = Layout.Row(8).Named($"{set.Set}{i + 1}");
				row.AddChild(new RuneGlyph(RuneSets.For(set.Set).Glyph, 26, Palette.Gold) { Name = "Glyph" });
				row.AddChild(RichText.Label($"{Texts.Term(set.Set)}: {Texts.Describe(set)}", TextWidth + 30).Named("Effect"));
				_detail.AddChild(row);
			}

			if (sheet.Runes.ActiveSets.Count == 0)
				_detail.AddChild(Layout.Text(runes.Count == 0 ? T("monsters.no_runes") : T("monsters.no_sets"), GameTheme.Faded).Named("NoSets"));
		}

		private void SkillsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var skills = summon.AllSkills;
			for (var i = 0; i < skills.Count; i++)
			{
				var locked = !monster.Awakened && i >= summon.Skills.Count;
				_detail.AddChild(SkillRow.Build(skills[i], monster.SkillLevel(i), monster.Awakened, locked, TextWidth, levels: true).Named($"Skill{i + 1}"));
			}

			if (summon.Leader is { } leader)
			{
				var row = Layout.Row(10).Named("Leader");
				row.AddChild(Doodle.Icon(Art.Icon("leader"), 34, Palette.Gold).Named("Icon"));
				row.AddChild(RichText.Label(T("monsters.leadership", Texts.Percent(leader.Value), Texts.Name(leader.Stat)), TextWidth + 10).Named("Text"));
				_detail.AddChild(row);
			}

			_detail.AddChild(new HSeparator { Name = "ActionsLine" });
			var copies = FusionDialog.Candidates(_database, _player, monster).Count(c => !c.Locked);
			var left = Fusion.SkillUpsLeft(_database, monster);
			_detail.AddChild(Layout.Text(left == 0 ? T("monsters.fuse_full") : T("monsters.fuse_hint", left, copies), GameTheme.Faded).Named("FuseHint"));
			var actions = Layout.Grid(2, 10).Named("Actions");
			var fuse = GameButton.Of(T("monsters.fuse_open"), () => FusionDialog.Open(this, _database, _player, monster, ids => FuseRequested?.Invoke(monster.Id, ids)), ButtonKind.Primary, "fuse").Named("Fuse");
			fuse.Disabled = copies == 0 || left == 0;
			actions.AddChild(fuse);
			_detail.AddChild(actions);
		}

		private void AwakenPage(SummonDefinition summon, OwnedSummon monster)
		{
			var forms = Layout.Row(14, true).Named("Forms");
			forms.AddChild(Form(summon, false, !monster.Awakened).Named("Normal"));
			forms.AddChild(Doodle.Icon(Art.Icon("awaken"), 40, monster.Awakened ? Palette.Awakened : Palette.GoldDark).Named("Arrow"));
			forms.AddChild(Form(summon, true, monster.Awakened).Named("Awakened"));
			_detail.AddChild(forms);

			var name = new Label { Name = "AwakenedName", Text = summon.Awakening.Name, ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			name.AddThemeColorOverride("font_color", Palette.Awakened);
			_detail.AddChild(name);
			_detail.AddChild(Layout.Text(monster.Awakened ? T("monsters.awakened_already") : T("monsters.awaken_gains"), GameTheme.Faded).Named("Explain"));

			var gains = Layout.Flow(8).Named("Gains");
			foreach (var (stat, gain) in Texts.AwakeningStats(summon))
				gains.AddChild(Layout.Labeled(Texts.GlyphOf(stat), gain, Texts.Name(stat)).Named(stat.ToString()));
			_detail.AddChild(gains);

			if (summon.Awakening.Skill is { } skill)
				_detail.AddChild(SkillRow.Build(skill, monster.SkillLevel(summon.Skills.Count), true, !monster.Awakened, TextWidth).Named("NewSkill"));
			foreach (var improved in summon.Skills.Where(s => s.ChangesOnAwakening))
			{
				var index = summon.Skills.ToList().IndexOf(improved);
				_detail.AddChild(SkillRow.Build(improved, monster.SkillLevel(index), true, false, TextWidth).Named($"Improved{index + 1}"));
			}

			if (monster.Awakened)
				return;

			var row = Layout.Row(0, true).Named("Actions");
			var cost = Awakening.Cost(summon.Rarity);
			var awaken = GameButton.Of(T("monsters.awaken_button"), () => Dialog.Confirm(this,
				T("monsters.awaken_title"),
				T("monsters.awaken_confirm", summon.Name, summon.Awakening.Name, cost),
				T("monsters.awaken_button"),
				() => AwakenRequested?.Invoke(monster.Id)), ButtonKind.Primary, "awaken", 64).WithCost("essence", Texts.Number(cost)).Named("Awaken");
			awaken.Disabled = !Awakening.CanAwaken(_player, monster, summon);
			row.AddChild(awaken.Wide(260));
			_detail.AddChild(row);
			if (awaken.Disabled)
				_detail.AddChild(Layout.Text(T("monsters.awaken_short", Texts.Number(cost - _player.Essence)), GameTheme.Faded).Named("Short"));
		}

		/// <summary>O monstro normal ou desperto (o mesmo desenho, com a aura e o anel do elemento), aceso se for a forma de agora.</summary>
		private static Control Form(SummonDefinition summon, bool awakened, bool current)
		{
			var frame = new PanelContainer { CustomMinimumSize = new Vector2(140, 140) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, current ? Palette.Arcane : Palette.GoldDark, current ? 3 : 1, 12, 10));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.Image), current ? Palette.Of(summon.Element) : Palette.Of(summon.Element).Darkened(0.45f), MaskShape.Rounded, 8, aura: awakened ? summon.Element : null));
			return frame;
		}
	}
}
