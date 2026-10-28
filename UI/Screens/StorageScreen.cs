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
	/// Monstros: à esquerda a grade densa de retratos quadrados, com o lugar e a contagem no alto ("Coleção
	/// 227/500") ao lado de Selecionar vários, e embaixo a barra Ordem, Filtros, Álbum (<see cref="CollectionDialog"/>)
	/// e Baú, que troca a grade entre a coleção e o Baú; à direita a ficha do escolhido.
	///
	/// A ficha tem duas colunas. Na estreita, à direita, só a régua de abas escritas em pé (Atributos,
	/// Habilidades, Despertar, Sigilos). Na larga, a aba:
	/// - Atributos: o retrato, as estrelas, o elemento, onde está e o nível; subir nível (ou evoluir), cada
	///   botão dizendo quanto custa; a ficha (a base e, em verde, o que os sigilos somam); e o que se faz com
	///   o monstro: Favoritar, Bloquear, Baú e Soltar.
	/// - Habilidades: os ícones numa fileira, com a Liderança; o tocado fica marcado e o texto dele (recarga,
	///   nível, descrição e aprimoramentos) aparece logo embaixo; e Fundir cópias, que abre a janela própria
	///   da fusão (<see cref="FusionDialog"/>).
	/// - Despertar: o desenho de agora e o desperto, o que ganha e o botão Despertar com o custo.
	/// - Sigilos: Gerenciar (a tela de Sigilos com este monstro) e Auto-equipar (<see cref="AutoEquipDialog"/>),
	///   os 6 no círculo e os conjuntos ativos.
	/// Fora da aba Atributos, o alto da ficha encolhe para uma linha (retrato pequeno, nome, estrelas e nível).
	///
	/// Ordem e Filtros (<see cref="MonsterFilter"/>): elemento, papel, estrelas, estrelas
	/// naturais, Despertar, situação e as habilidades (cada campo aceita vários valores: basta um valer; campos
	/// diferentes valem juntos) (o que fazem, em que escalam, que efeito põem: só as
	/// opções que algum monstro tem, <see cref="SkillTraits"/>); a ordem por estrelas, nível, elemento, nome, chegada ou por um
	/// atributo (com as runas), que então aparece escrito em cada cartão. Os favoritos vêm antes em
	/// qualquer ordem. O GameRoot guarda a busca enquanto o jogo está aberto (<see cref="FilterChanged"/>).
	///
	/// Os monstros que chegaram desde a última visita (<see cref="Roster.IsNew"/>) vêm na frente de tudo,
	/// com a faixa "Novo!", a contagem do alto diz quantos e, abrindo a tela sem escolha, o mais novo já vem
	/// escolhido (no lugar dele). Ao sair pelo Voltar, deixam de ser novos.
	///
	/// No Baú, cópias iguais em tudo o que importa (<see cref="MonsterStack"/>) viram um cartão só, com
	/// "×7"; tocar nele abre as cópias uma a uma (<see cref="CopiesDialog"/>): marcar algumas e só com elas
	/// tirar do Baú, fundir ou soltar. Sobrando uma cópia, o cartão volta a ser normal.
	///
	/// Selecionar vários marca cartões (nos dois lugares, qualquer monstro, bloqueado ou não) para guardar no
	/// Baú, tirar dele ou soltar de uma vez; soltar deixa de fora os bloqueados. No Baú, tocar num grupo
	/// marca (ou desmarca) as cópias todas; o cartão diz quantas estão marcadas ("3/7"). Fundir é só pela
	/// janela da fusão, para uma escolha não se confundir com a outra. Mudar o filtro desmarca os que somem da
	/// grade, para não soltar nada que não se vê. Toque longo em qualquer cartão abre o resumo. Tocar num
	/// cartão muda só ele, a faixa da seleção e a ficha: a grade não é refeita, e a rolagem fica onde está.
	/// Cada botão vira um evento; o GameRoot aplica a regra e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class StorageScreen : Control
	{
		private const float DetailWidth = 570;

		/// <summary>Cabe a aba mais comprida (Habilidades) com o símbolo.</summary>
		private const float SideWidth = 150;

		/// <summary>Os retratos da grade: sete por linha ao lado da ficha em 1280 de largura.</summary>
		private const float CardWidth = 80;

		private const float CardAspect = CreatureCard.Square;
		private const float CardGap = Space.Small;

		/// <summary>A largura do texto na coluna larga da ficha (a coluna menos a barra de rolagem reservada).</summary>
		private const float TextWidth = 340;

		/// <summary>Os ícones da aba Habilidades: cinco (quatro e a Liderança) cabem na coluna larga.</summary>
		private const float SkillSize = 64;

		/// <summary>O retrato do alto da ficha: grande na aba Atributos, pequeno nas outras.</summary>
		private const float PortraitSize = 96;
		private const float SmallPortraitSize = 60;

		/// <summary>Os botões grandes da aba (subir nível, evoluir) e os do que se faz com o monstro.</summary>
		private const int MainHeight = 56;
		private const int ActionHeight = 44;

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
		private readonly HBoxContainer _tools = Layout.Row(Space.Regular).Named("Tools");
		private readonly GridContainer _bar = Layout.Grid(4, Space.Medium).Named("Bar");
		private readonly VBoxContainer _selection = new() { Name = "Selection" };
		private readonly TileGrid _roster = new(CardGap) { Name = "Roster" };
		private readonly VBoxContainer _detail = new() { Name = "Detail" };
		private readonly TextTabs _pages = new(vertical: true, 64) { Name = "Pages" };

		/// <summary>A habilidade aberta na aba Habilidades (a posição; depois da última, a Liderança).</summary>
		private int _skill;

		/// <summary>O cartão de cada monstro da grade (as cópias de um grupo dão no mesmo): o toque muda só ele.</summary>
		private readonly Dictionary<int, CreatureCard> _cards = new();
		private IReadOnlyList<MonsterStack> _stacks = Array.Empty<MonsterStack>();

		/// <param name="filter">A busca da última vez (nula: sem filtro, por estrelas).</param>
		public StorageScreen(GameDatabase database, PlayerState player, int? selected, MonsterFilter? filter = null)
		{
			_database = database;
			_player = player;
			_filter = filter ?? new MonsterFilter();
			_selected = player.Monster(selected ?? -1)?.Id
				?? player.Monsters.Where(m => Roster.IsNew(player, m)).MaxBy(m => m.Id)?.Id
				?? player.Collection.FirstOrDefault()?.Id;
			_showStorage = player.Monster(_selected ?? -1)?.Stored ?? false;
		}

		/// <summary>Id e se é até o nível máximo (falso = só o próximo nível).</summary>
		public event Action<int, bool>? InfuseRequested;

		public event Action<int>? AwakenRequested;
		public event Action<int>? EvolveRequested;
		public event Action<int>? RunesRequested;

		/// <summary>O Auto-equipar confirmado: os sigilos que entram e o monstro.</summary>
		public event Action<IReadOnlyList<int>, int>? EquipManyRequested;

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
			page.AddChild(Layout.Header(T("destination.Monsters"), _currencies, () => BackRequested?.Invoke(), this,
				T("monsters.level_hint", Leveling.ExperiencePerEssence), T("monsters.stats_hint"), T("monsters.skills_hint"), T("monsters.awaken_skills_hint")).Header);

			var body = Layout.Row(Space.Loose).Named("Body");
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			page.AddChild(body);

			var rosterPanel = new PanelContainer { Name = "Collection", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var rosterColumn = new VBoxContainer { Name = "Column" };
			rosterColumn.AddThemeConstantOverride("separation", Space.Regular);
			rosterPanel.AddChild(rosterColumn);
			rosterColumn.AddChild(_tools);
			rosterColumn.AddChild(_selection);
			_roster.Released += (index, _) =>
			{
				foreach (var copy in _stacks[index].Copies)
					_cards.Remove(copy.Id);
			};
			rosterColumn.AddChild(Layout.Scroll(_roster));
			rosterColumn.AddChild(_bar);
			body.AddChild(rosterPanel);

			var detailPanel = new PanelContainer { Name = "Sheet", CustomMinimumSize = new Vector2(DetailWidth, 0) };
			var detailRow = Layout.Row(Space.Wide).Named("Row");

			var side = new VBoxContainer { Name = "Side", CustomMinimumSize = new Vector2(SideWidth, 0) };
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

			_detail.AddThemeConstantOverride("separation", Space.Medium);
			detailRow.AddChild(Layout.Scroll(_detail));
			detailRow.AddChild(side);
			detailPanel.AddChild(detailRow);
			body.AddChild(detailPanel);

			Refresh();
		}

		public void Refresh()
		{
			if (_selected is { } id && _player.Monster(id) == null)
				_selected = (_showStorage ? _player.Storage : _player.Collection).FirstOrDefault()?.Id ?? _player.Collection.FirstOrDefault()?.Id;
			_marked.RemoveWhere(marked => _player.Monster(marked) == null);

			_currencies.Refresh(_player);
			RefreshTools();
			RefreshSelection();
			RefreshRoster();
			RefreshDetail();
		}

		// Lugar, barra e seleção --------------------------------------------------------------------

		/// <summary>
		/// No alto, o lugar aberto e a contagem ("Coleção 227/500", "Baú 12") e Selecionar vários; embaixo da grade,
		/// Ordem, Filtros, Álbum e Baú, que troca de lugar (aceso enquanto o Baú está aberto).
		/// </summary>
		private void RefreshTools()
		{
			Layout.Clear(_tools);
			var place = _showStorage ? _player.Storage : _player.Collection;
			var count = _showStorage ? place.Count().ToString() : $"{place.Count()}/{_player.CollectionCapacity}";
			var title = new Label
			{
				Name = "Place",
				Text = T(_showStorage ? "monsters.place_vault" : "monsters.place_collection", count),
				ThemeTypeVariation = GameTheme.Heading,
				// Uma linha só: quebrar empurraria a grade para baixo. Os novos já vêm na frente com o selo "Novo!".
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				CustomMinimumSize = new Vector2(10, 0),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center,
			};
			_tools.AddChild(title);

			var select = GameButton.Of(_selecting ? T("monsters.select_done") : T("monsters.select"), () =>
			{
				_selecting = !_selecting;
				_marked.Clear();
				Callable.From(Refresh).CallDeferred();
			}, _selecting ? ButtonKind.Primary : ButtonKind.Secondary, "select", 48).Named("Select");
			_tools.AddChild(select.Wide(220).FixedHeight());

			Layout.Clear(_bar);
			MonsterSearch.Fill(_bar, _database, _filter, Filter, 48);
			// A Ordem vem primeiro: é a que mais se usa.
			_bar.MoveChild(_bar.GetNode("Sort"), 0);
			_bar.AddChild(GameButton.Of(T("monsters.album"), () => CollectionDialog.Open(this, _database, _player), ButtonKind.Secondary, "grimoire", 48).Named("Album"));
			_bar.AddChild(GameButton.Of(T("monsters.vault_button", _player.Storage.Count()), () =>
			{
				_showStorage = !_showStorage;
				Callable.From(Refresh).CallDeferred();
			}, _showStorage ? ButtonKind.Primary : ButtonKind.Secondary, "chest", 48).Named("Vault"));
			foreach (var button in _bar.GetChildren().OfType<GameButton>())
			{
				button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				button.FixedHeight();
			}
		}

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
			unmark.Block(marked.Count == 0 ? T("monsters.why_marked") : null, T("monsters.unmark"));
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
				() =>
				{
					EndSelection();
					ReleaseRequested?.Invoke(released.Select(m => m.Id).ToList());
				}, ButtonKind.Danger), ButtonKind.Danger, "release", 48).Named("ReleaseMarked");
			button.Block(released.Count == 0 ? T("monsters.why_marked") : null, T("monsters.release"));
			return button;
		}

		/// <summary>Guardar os marcados da coleção no Baú. Se algum está em equipe, pergunta antes (ele sai dela).</summary>
		private GameButton StoreMarked(IReadOnlyList<OwnedSummon> marked)
		{
			var ids = marked.Where(m => !m.Stored).Select(m => m.Id).ToList();
			void Store()
			{
				EndSelection();
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
			button.Block(ids.Count == 0 ? T("monsters.why_marked") : null, T("monsters.store_short"));
			return button;
		}

		/// <summary>Tirar os marcados do Baú: só os que cabem na coleção (o botão diz quantos).</summary>
		private GameButton RetrieveMarked(IReadOnlyList<OwnedSummon> marked)
		{
			var ids = marked.Where(m => m.Stored).Select(m => m.Id).Take(Roster.FreeSlots(_player)).ToList();
			var button = GameButton.Of(T("monsters.retrieve_marked", ids.Count), () =>
			{
				EndSelection();
				RetrieveManyRequested?.Invoke(ids);
			}, ButtonKind.Secondary, "storage", 48).Named("RetrieveMarked");
			button.Block(ids.Count == 0 ? T("monsters.why_marked") : null, T("monsters.retrieve_short"));
			return button;
		}

		/// <summary>Acabou a ação em lote: sai do modo de seleção e nada fica marcado.</summary>
		private void EndSelection()
		{
			_selecting = false;
			_marked.Clear();
		}

		private void RefreshRoster()
		{
			_roster.Reset();
			Layout.Clear(_roster);
			_cards.Clear();
			var place = (_showStorage ? _player.Storage : _player.Collection).ToList();
			// Os novos na frente; entre eles e entre os outros, a ordem do filtro.
			var monsters = _filter.Apply(place, _database, _player).OrderByDescending(m => Roster.IsNew(_player, m)).ToList();
			var stacks = _showStorage
				? MonsterStack.Group(monsters, _player)
				: monsters.Select(m => new MonsterStack(new[] { m })).ToList();

			// Só os cartões que aparecem na rolagem existem como nós: a coleção e o Baú passam de centenas.
			_stacks = stacks;
			if (stacks.Count > 0)
				_roster.Virtualize(stacks.Count, new Vector2(CardWidth, CardWidth * CardAspect), index => Card(_stacks[index]));

			if (monsters.Count == 0)
			{
				var empty = place.Count > 0 ? "monsters.filter_empty" : _showStorage ? "monsters.vault_empty" : "monsters.collection_empty";
				_roster.AddChild(Layout.Text(T(empty), GameTheme.Faded, 400).Named("Empty"));
			}
		}

		/// <summary>O cartão de um monstro, ou de um grupo de cópias do Baú (com "×7", ou "3/7" marcadas).</summary>
		private CreatureCard Card(MonsterStack stack)
		{
			var monster = stack.First;
			var summon = _database.Summon(monster.SummonId);
			var inTeam = MonsterNotes.Teams(_database, _player, monster.Id).Count > 0;
			// Na ordem por atributo, o valor dele (com as runas) fica escrito no cartão; o novo diz que é novo.
			var tag = Roster.IsNew(_player, monster) ? T("monsters.new")
				: _filter.Sort == MonsterSort.Stat ? Texts.Value(_filter.SortStat, MonsterFilter.Value(monster, summon, _player, _filter.SortStat)) : null;
			var card = new CreatureCard(summon, monster, CardWidth, inTeam ? "team" : null, tag, aspect: CardAspect) { Name = stack.Count > 1 ? $"Group{monster.Id}" : $"Monster{monster.Id}" };
			var marked = stack.Copies.Count(c => _marked.Contains(c.Id));
			card.SetSelected(stack.Copies.Any(c => c.Id == _selected));
			card.SetMarked(marked == stack.Count);
			card.SetCount(stack.Count, marked);
			card.Pressed += pressed => Tap(stack, pressed);
			foreach (var copy in stack.Copies)
				_cards[copy.Id] = card;
			return card;
		}

		/// <summary>
		/// Na seleção, marca ou desmarca (o grupo inteiro); fora dela, escolhe e, num grupo, abre as cópias.
		/// Só os cartões tocados, a faixa da seleção e a ficha mudam; a grade fica, e a rolagem com ela.
		/// </summary>
		private void Tap(MonsterStack stack, CreatureCard card)
		{
			var ids = stack.Copies.Select(c => c.Id).ToList();
			if (_selecting)
			{
				if (ids.All(_marked.Contains))
					_marked.ExceptWith(ids);
				else
					_marked.UnionWith(ids);
				var marked = ids.Count(_marked.Contains);
				card.SetMarked(marked == stack.Count);
				card.SetCount(stack.Count, marked);
				RefreshSelection();
				return;
			}

			if (!ids.Contains(_selected ?? -1))
			{
				if (_selected is { } previous && _cards.TryGetValue(previous, out var before))
					before.SetSelected(false);
				_selected = ids[0];
				_skill = 0;
				card.SetSelected(true);
				RefreshDetail();
			}

			if (stack.Count > 1)
				CopiesDialog.Open(this, _database, _player, stack,
					retrieved => RetrieveManyRequested?.Invoke(retrieved),
					(target, materials) => Fuse(target, materials),
					released => ReleaseRequested?.Invoke(released));
		}

		/// <summary>
		/// Funde as cópias e, com a ficha refeita, sobe em verde o "+n" sobre cada habilidade que subiu de nível
		/// (a mesma animação dos marcos da runa).
		/// </summary>
		private void Fuse(int target, IReadOnlyList<int> materials)
		{
			var monster = _player.Monster(target);
			var before = monster == null ? new List<int>() : Enumerable.Range(0, _database.Summon(monster.SummonId).AllSkills.Count).Select(monster.SkillLevel).ToList();
			FuseRequested?.Invoke(target, materials);
			if (monster == null)
				return;

			// Só quando a ficha na tela é a do monstro que subiu (a fusão aberta pelas cópias pode ser de outro).
			if (_selected != target)
				return;

			var gains = before.Select((level, i) => monster.SkillLevel(i) - level).ToList();
			// A ficha acabou de ser refeita: espera o próximo quadro para os ícones já estarem no lugar.
			Callable.From(() => Callable.From(() =>
			{
				if (!IsInsideTree())
					return;
				for (var i = 0; i < gains.Count; i++)
				{
					if (gains[i] > 0 && _detail.FindChild($"Skill{i + 1}", true, false) is Control tile)
						GainPop.Show(this, tile, $"+{gains[i]}");
				}
			}).CallDeferred()).CallDeferred();
		}

		// Ficha ------------------------------------------------------------------------------------

		private void RefreshDetail()
		{
			Layout.Clear(_detail);
			var hasMonster = _selected is { } id && _player.Monster(id) != null;
			_pages.Visible = hasMonster;
			if (!hasMonster)
			{
				_detail.AddChild(Layout.Text(T("monsters.none_selected"), GameTheme.Faded).Named("Empty"));
				return;
			}

			var monster = _player.Monster(_selected!.Value)!;
			var summon = _database.Summon(monster.SummonId);

			// Homúnculo de 2★ não desperta (versão 0.1): a aba do Despertar nem aparece, e a aberta passa para Atributos.
			var canAwaken = !monster.IsInfusionCore && Awakening.CanBeAwakened(summon);
			_pages.SetTabVisible((int)Page.Awaken, canAwaken);
			if (!canAwaken && _page == Page.Awaken)
			{
				_page = Page.Stats;
				_pages.Select((int)Page.Stats);
			}

			if (monster.IsInfusionCore)
			{
				// O Núcleo não tem atributos, runas, habilidades nem Despertar: só o que ele é e como usar.
				_pages.Visible = false;
				_detail.AddChild(CoreDetail(summon, monster));
				_detail.AddChild(Actions(summon, monster));
				return;
			}

			_detail.AddChild(Identity(summon, monster, _page != Page.Stats));
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

		/// <summary>
		/// O que se faz com o monstro, no fim da aba Atributos (e da ficha do Núcleo), em duas colunas fixas:
		/// Favoritar e Bloquear; Baú (guardar ou tirar) e Soltar. O que não vale fica apagado e diz o porquê.
		/// </summary>
		private GridContainer Actions(SummonDefinition summon, OwnedSummon monster)
		{
			var id = monster.Id;
			var grid = Layout.Grid(2, Space.Regular).Named("Actions");
			grid.AddChild(GameButton.Of(monster.Favorite ? T("monsters.unfavorite") : T("monsters.favorite"), () => FavoriteRequested?.Invoke(id), ButtonKind.Secondary, "favorite", ActionHeight).Named("Favorite"));
			grid.AddChild(GameButton.Of(monster.Locked ? T("lock.unlock") : T("lock.lock"), () => LockRequested?.Invoke(id), ButtonKind.Secondary, monster.Locked ? "unlock" : "lock", ActionHeight).Named("Lock"));

			if (!monster.Stored)
			{
				grid.AddChild(GameButton.Of(T("monsters.store_short"), () => StoreRequested?.Invoke(id), ButtonKind.Secondary, "chest", ActionHeight).Named("Store"));
			}
			else
			{
				var retrieve = GameButton.Of(T("monsters.retrieve_short"), () => RetrieveRequested?.Invoke(id), ButtonKind.Secondary, "storage", ActionHeight).Named("Retrieve");
				retrieve.Block(Roster.IsFull(_player) ? T("monsters.collection_full") : null, T("monsters.retrieve_short"));
				grid.AddChild(retrieve);
			}

			var fragments = Fusion.FragmentsFor(summon.Rarity);
			var release = GameButton.Of(T("monsters.release"), () => Dialog.Confirm(this,
				T("monsters.release_title"),
				T("monsters.release_confirm", summon.NameFor(monster.Awakened), monster.Level, fragments) + MonsterNotes.Warning(_database, _player, new[] { monster }),
				T("monsters.release_button"),
				() => ReleaseRequested?.Invoke(new[] { id }), ButtonKind.Danger), ButtonKind.Danger, "release", ActionHeight);
			release.Block(monster.IsInfusionCore ? T("monsters.why_core_release") : monster.Locked ? T("monsters.locked_note") : null, T("monsters.release"));
			grid.AddChild(release.Named("Release"));

			foreach (var button in grid.GetChildren().OfType<GameButton>())
			{
				button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				button.FixedHeight();
			}

			return grid;
		}

		/// <summary>O Núcleo de Infusão: retrato, nome, onde está e para que serve (fundir em qualquer monstro).</summary>
		private VBoxContainer CoreDetail(SummonDefinition summon, OwnedSummon monster)
		{
			var column = new VBoxContainer { Name = "Core" };
			column.AddThemeConstantOverride("separation", Space.Regular);
			var row = Layout.Row(Space.Wide).Named("Row");
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(112, 112) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Gold, 3, Radius.Button, 8));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Gold, MaskShape.Rounded, 6));
			row.AddChild(frame);
			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			// O nome quebra em vez de alargar a ficha (sem isso a grade da esquerda perde uma coluna).
			info.AddChild(Layout.Text(summon.Name, GameTheme.Heading, 10).Named("Name"));
			var where = new List<string>();
			if (monster.Stored)
				where.Add(T("monsters.in_vault"));
			if (monster.Locked)
				where.Add(T("monsters.locked"));
			where.Add(T("monsters.core_count", _player.Monsters.Count(m => m.IsInfusionCore)));
			info.AddChild(Layout.Text(string.Join(" · ", where), GameTheme.Faded).Named("Where"));
			row.AddChild(info);
			row.AddChild(SigilButton.Of("help", () => Dialog.Info(this, summon.Name, T("monsters.core_info"), 480), 40).Named("CoreHelp"));
			column.AddChild(row);
			return column;
		}

		/// <summary>
		/// Retrato, nome, estrelas, elemento e papel, onde está, nível e experiência, na aba Atributos. Nas outras
		/// (<paramref name="compact"/>), uma linha só: retrato pequeno, nome, estrelas e nível.
		/// </summary>
		private VBoxContainer Identity(SummonDefinition summon, OwnedSummon monster, bool compact)
		{
			var column = new VBoxContainer { Name = "Identity" };
			column.AddThemeConstantOverride("separation", Space.Small);
			var row = Layout.Row(Space.Wide).Named("Row");
			var side = compact ? SmallPortraitSize : PortraitSize;
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(side, side), SizeFlagsVertical = SizeFlags.ShrinkCenter };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Frame(summon.Rarity), compact ? 2 : 3, 10, compact ? 4 : 8));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.Image), Palette.Of(summon.Element), MaskShape.Rounded, 6, aura: monster.Awakened ? summon.Element : null));
			row.AddChild(frame);

			var info = new VBoxContainer { Name = "Info", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			info.AddThemeConstantOverride("separation", compact ? Space.Hair : Space.Tight);
			var title = Layout.Row(Space.Medium).Named("Title");
			var name = new Label { Name = "Name", Text = summon.NameFor(monster.Awakened), ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(10, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
			if (monster.Awakened)
				name.AddThemeColorOverride("font_color", Palette.Awakened);
			title.AddChild(name);
			if (monster.Favorite)
				title.AddChild(Doodle.Icon(Art.Icon("favorite"), 22, Palette.Negative.Lightened(0.15f)).Named("Favorite"));
			info.AddChild(title);

			var stars = new Label { Name = "Stars", Text = Texts.Stars(monster.Stars) };
			stars.AddThemeColorOverride("font_color", Palette.Stars(summon.Rarity, monster.Awakened));
			stars.AddThemeFontSizeOverride("font_size", FontSize.Strong);
			if (compact)
			{
				// Estrelas e nível na mesma linha, embaixo do nome.
				var facts = Layout.Row(Space.Wide).Named("Facts");
				facts.AddChild(stars);
				facts.AddChild(new Label { Name = "Level", Text = T("monsters.level", monster.Level, Leveling.MaxLevel(monster)), ThemeTypeVariation = GameTheme.Number, VerticalAlignment = VerticalAlignment.Center });
				info.AddChild(facts);
				row.AddChild(info);
				column.AddChild(row);
				return column;
			}

			info.AddChild(stars);

			var line = Layout.Row(Space.Medium).Named("Line");
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
			var level = Layout.Row(Space.Regular).Named("Level");
			var label = new Label { Name = "Text", Text = T("monsters.level", monster.Level, max), ThemeTypeVariation = GameTheme.Number };
            if (max == 40) label.AddThemeColorOverride("font_color", Palette.Gold);
			level.AddChild(label);
            var bar = Layout.Energy(Palette.Gold, 12).Named("Experience");
			bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			bar.MaxValue = Math.Max(1, Leveling.ExperienceToNext(monster));
			bar.Value = Leveling.IsMaxLevel(monster) ? bar.MaxValue : monster.Experience;
			level.AddChild(bar);
			var textLabel = new Label
			{
				Name = "ExperienceText",
				Text = Leveling.IsMaxLevel(monster) ? T("monsters.level_max") : T("monsters.experience", monster.Experience, Leveling.ExperienceToNext(monster)),
				ThemeTypeVariation = GameTheme.Faded,
			};
            if (max == 40) textLabel.AddThemeColorOverride("font_color", Palette.Gold);
            level.AddChild(textLabel);
			// O que o próximo passo custa: um nível (Essência) ou, no nível máximo, a estrela seguinte (Fragmentos).
			var atMax = Leveling.IsMaxLevel(monster);
			if (!atMax || Evolution.IsReady(monster))
				level.AddChild(Layout.Chip(atMax ? "fragments" : "essence", Texts.Number(atMax ? Evolution.Cost(monster.Stars) : Leveling.InfuseCosts(monster).Item1)).Named("Cost"));

			column.AddChild(level);
			return column;
		}

		private void StatsPage(SummonDefinition summon, OwnedSummon monster)
		{
			var id = monster.Id;
			// Em grade: os dois botões ficam do mesmo tamanho e o texto quebra igual nos dois ("Até o nível 30" e "35").
			// Duas células fixas, da mesma altura: subir 1 nível e evoluir. O que não vale agora fica
			// apagado no lugar e o toque diz o motivo.
			var actions = Layout.Grid(2, Space.Regular).Named("Actions");
			var maxed = Leveling.IsMaxLevel(monster);
			var next = maxed ? 0 : Leveling.InfuseCosts(monster).Item1;
			var one = GameButton.Of(T("monsters.level_up"), () => InfuseRequested?.Invoke(id, false), ButtonKind.Primary, "essence", MainHeight).Named("LevelUp");
			one.Block(maxed ? T("monsters.why_max_level") : _player.Essence < next ? T("common.why_essence") : null, T("monsters.level_up"));
			actions.AddChild(one.FixedHeight());
			var evolve = GameButton.Of(T("monsters.evolve", Texts.Stars(Math.Min(monster.Stars + 1, Growth.MaxStars))), () => EvolveRequested?.Invoke(id), ButtonKind.Primary, "evolve", MainHeight).Named("Evolve");
			evolve.Block(monster.Stars >= Growth.MaxStars ? T("monsters.fully_grown") : !maxed ? T("monsters.why_evolve_level") : !Evolution.CanEvolve(_player, monster) ? T("monsters.why_evolve") : null,
				T("monsters.evolve", Texts.Stars(Math.Min(monster.Stars + 1, Growth.MaxStars))));
			actions.AddChild(evolve.FixedHeight());
			_detail.AddChild(actions);

			_detail.AddChild(new HSeparator { Name = "StatsLine" });
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, _player.RunesOn(monster.Id));
			var table = new StatTable { Name = "Stats" };
			table.Show(sheet);
			_detail.AddChild(table);
			_detail.AddChild(Actions(summon, monster));
		}

		private void RunesPage(SummonDefinition summon, OwnedSummon monster)
		{
			var runes = _player.RunesOn(monster.Id);
			var sheet = SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, runes);

			// Gerenciar abre a tela de Sigilos com este monstro; Auto-equipar abre a janela que só equipa no Equipar.
			var actions = Layout.Grid(2, Space.Regular).Named("Actions");
			actions.AddChild(GameButton.Of(T("monsters.runes_button"), () => RunesRequested?.Invoke(monster.Id), ButtonKind.Primary, "rune", ActionHeight).Named("OpenRunes"));
			actions.AddChild(GameButton.Of(T("runes.auto"), () => AutoEquipDialog.Open(this, _database, _player, monster, ids => EquipManyRequested?.Invoke(ids, monster.Id)), ButtonKind.Secondary, "auto", ActionHeight).Named("AutoEquip"));
			foreach (var button in actions.GetChildren().OfType<GameButton>())
			{
				button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				button.FixedHeight();
			}

			_detail.AddChild(actions);

			var ring = new SigilRing(260) { Name = "Ring", Spread = 0.72f, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
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
				var row = Layout.Row(Space.Medium).Named($"{set.Set}{i + 1}");
				row.AddChild(new PlanetSymbol(RuneSets.PlanetOf(set.Set), 26, Palette.Gold) { Name = "Planet" });
				row.AddChild(RichText.Label($"{Texts.Term(set.Set)}: {Texts.Describe(set)}", TextWidth - 34).Named("Effect"));
				_detail.AddChild(row);
			}

			if (sheet.Runes.ActiveSets.Count == 0)
				_detail.AddChild(Layout.Text(runes.Count == 0 ? T("monsters.no_runes") : T("monsters.no_sets"), GameTheme.Faded).Named("NoSets"));
		}

		private void SkillsPage(SummonDefinition summon, OwnedSummon monster)
		{
			// Até quatro habilidades e a Liderança numa fileira só: ativas em pedra quadrada, passivas em losango,
			// a Liderança em pentágono com a coroa. A tocada ganha o traço dourado embaixo e o texto dela aparece
			// abaixo de Fundir (que fica sempre no mesmo lugar); o toque troca só o traço e o texto.
			var skills = summon.AllSkills;
			var leader = summon.Leader;
			var count = skills.Count + (leader != null ? 1 : 0);
			_skill = Math.Clamp(_skill, 0, Math.Max(0, count - 1));
			var tiles = Layout.Grid(SkillTile.PerRow, Space.Regular).Named("SkillTiles");
			var markers = new List<Control>();
			var info = new VBoxContainer { Name = "SkillInfo" };
			info.AddThemeConstantOverride("separation", Space.Small);

			void Show(int index)
			{
				_skill = index;
				for (var i = 0; i < markers.Count; i++)
					markers[i].Modulate = new Color(1, 1, 1, i == index ? 1 : 0);
				Layout.Clear(info);
				var title = new Label { Name = "Title", ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(10, 0) };
				info.AddChild(title);
				if (index < skills.Count)
				{
					var skill = skills[index];
					title.Text = SkillDialog.Title(skill);
					SkillDialog.Fill(info, skill, monster.SkillLevel(index), monster.Awakened, !monster.Awakened && index >= summon.Skills.Count, TextWidth);
				}
				else if (leader != null)
				{
					title.Text = T("monsters.leader_title");
					info.AddChild(SkillDialog.LeaderText(leader, TextWidth));
				}
			}

			Control Slot(Control tile, string name)
			{
				var slot = new VBoxContainer { Name = name };
				slot.AddThemeConstantOverride("separation", Space.Tight);
				slot.AddChild(tile.Named("Tile"));
				var marker = new ColorRect { Name = "Marker", Color = Palette.Gold, CustomMinimumSize = new Vector2(SkillSize * 0.6f, 4), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
				markers.Add(marker);
				slot.AddChild(marker);
				return slot;
			}

			for (var i = 0; i < skills.Count; i++)
			{
				var index = i;
				var locked = !monster.Awakened && i >= summon.Skills.Count;
				tiles.AddChild(Slot(SkillTile.Build(skills[i], monster.SkillLevel(i), locked, SkillSize, onPressed: () => Show(index)), $"Skill{i + 1}"));
			}

			if (leader != null)
				tiles.AddChild(Slot(SkillTile.Leader(leader.Stat, SkillSize, () => Show(skills.Count)), "Leader"));

			_detail.AddChild(tiles);

			var copies = FusionDialog.Candidates(_database, _player, monster).Count(c => !c.Locked);
			var left = Fusion.SkillUpsLeft(_database, monster);
			//_detail.AddChild(Layout.Text(left == 0 ? T("monsters.fuse_full") : T("monsters.fuse_hint", left, copies), GameTheme.Faded).Named("FuseHint"));
			var actions = Layout.Grid(2, Space.Regular).Named("Actions");
			var fuse = GameButton.Of(T("monsters.fuse_open"), () => FusionDialog.Open(this, _database, _player, monster, ids => Fuse(monster.Id, ids)), ButtonKind.Primary, "fuse", ActionHeight).Named("Fuse");
			fuse.Block(left == 0 ? T("monsters.fuse_full") : copies == 0 ? T("monsters.why_no_copies") : null, T("monsters.fuse_open"));
			actions.AddChild(fuse.Wide(240).FixedHeight());
			_detail.AddChild(actions);

			_detail.AddChild(new HSeparator { Name = "InfoLine" });
			_detail.AddChild(info);
			if (count > 0)
				Show(_skill);
		}

		private void AwakenPage(SummonDefinition summon, OwnedSummon monster)
		{
			var forms = Layout.Row(Space.Wide, true).Named("Forms");
			forms.AddChild(Form(summon, false, !monster.Awakened).Named("Normal"));
			forms.AddChild(Doodle.Icon(Art.Icon("awaken"), 32, monster.Awakened ? Palette.Awakened : Palette.GoldDark).Named("Arrow"));
			forms.AddChild(Form(summon, true, monster.Awakened).Named("Awakened"));
			_detail.AddChild(forms);

			var name = new Label { Name = "AwakenedName", Text = summon.Awakening.Name, ThemeTypeVariation = GameTheme.Heading, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			name.AddThemeColorOverride("font_color", Palette.Awakened);
			_detail.AddChild(name);

			// O botão vem antes do que se ganha: é a decisão da página. Homúnculo de 2★ não desperta (versão 0.1):
			// nem a opção aparece.
			if (!monster.Awakened && Awakening.CanBeAwakened(summon))
				AwakenAction(summon, monster);

			_detail.AddChild(Layout.Text(monster.Awakened ? T("monsters.awakened_already") : T("monsters.awaken_gains"), GameTheme.Faded).Named("Explain"));

			var gains = Layout.Flow(Space.Small).Named("Gains");
			foreach (var (stat, gain) in Texts.AwakeningStats(summon))
				gains.AddChild(Layout.Labeled(Texts.PlanetOf(stat), gain, Texts.Name(stat), compact: true).Named(stat.ToString()));
			_detail.AddChild(gains);

			// A habilidade nova e as melhoradas, nos mesmos ícones da aba Habilidades; nas melhoradas a janela
			// mostra o antes e o depois.
			var tiles = SkillTile.Grid().Named("SkillTiles");
			if (summon.Awakening.Skill is { } skill)
			{
				var index = summon.Skills.Count;
				var level = monster.SkillLevel(index);
				var locked = !monster.Awakened;
				Control? tile = null;
				tile = SkillTile.Build(skill, level, locked, SkillTile.DefaultSize, onPressed: () => SkillDialog.Open(tile!, skill, monster.SkillLevel(index), true, locked)).Named("NewSkill");
				tiles.AddChild(tile);
			}

			var skills = summon.Skills.ToList();
			foreach (var improved in skills.Where(s => s.ChangesOnAwakening))
			{
				var index = skills.IndexOf(improved);
				var level = monster.SkillLevel(index);
				Control? tile = null;
				tile = SkillTile.Build(improved, level, false, SkillTile.DefaultSize, onPressed: () => SkillDialog.OpenAwakening(tile!, improved, monster.SkillLevel(index))).Named($"Improved{index + 1}");
				tiles.AddChild(tile);
			}

			if (tiles.GetChildCount() > 0)
			{
				_detail.AddChild(tiles);
			}
		}

		private void AwakenAction(SummonDefinition summon, OwnedSummon monster)
		{
			var row = Layout.Row(Space.None, true).Named("Actions");
			var cost = Awakening.Cost(summon.Rarity);
			var awaken = GameButton.Of(T("monsters.awaken_button"), () => Dialog.Confirm(this,
				T("monsters.awaken_title"),
				T("monsters.awaken_confirm", summon.Name, summon.Awakening.Name, cost),
				T("monsters.awaken_button"),
				() => AwakenRequested?.Invoke(monster.Id)), ButtonKind.Primary, "awaken").Named("Awaken");
			awaken.Block(Awakening.CanAwaken(_player, monster, summon) ? null : _player.Essence < cost ? T("common.why_essence") : T("monsters.why_awaken"), T("monsters.awaken_button"));
			row.AddChild(awaken.Wide(260));
			_detail.AddChild(row);
			if (awaken.Disabled)
				_detail.AddChild(Layout.Text(T("monsters.awaken_short", Texts.Number(cost - _player.Essence)), GameTheme.Faded).Named("Short"));
		}

		/// <summary>O monstro normal ou desperto (o mesmo desenho, com a aura e o anel do elemento), aceso se for a forma de agora.</summary>
		private static Control Form(SummonDefinition summon, bool awakened, bool current)
		{
			var frame = new PanelContainer { CustomMinimumSize = new Vector2(124, 124) };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, current ? Palette.Arcane : Palette.GoldDark, current ? 3 : 1, 12, 10));
			frame.AddChild(Doodle.Masked(Art.Creature(summon.Image), current ? Palette.Of(summon.Element) : Palette.Of(summon.Element).Darkened(0.45f), MaskShape.Rounded, 8, aura: awakened ? summon.Element : null));
			return frame;
		}
	}
}
