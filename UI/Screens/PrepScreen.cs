using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.UI.Audio;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A preparação da luta, entre o Lutar da Campanha, de uma Masmorra ou da Exploração e a batalha:
	///
	/// - No alto, à esquerda, a equipe em formação (três na frente, duas atrás; a primeira é a Líder), a
	///   Liderança dela e, com um monstro da equipe escolhido, Tornar Líder e Tirar.
	/// - No alto, à direita, os inimigos da última onda (o chefe sozinho na frente; tocar ou segurar abre o
	///   resumo) e os botões Lutar, com a Mana, e Batalha automática, quando a luta já tem.
	/// - Embaixo, a coleção numa lista que rola de lado, com Filtros e Ordem (<see cref="MonsterSearch"/>):
	///   tocar num monstro põe ou tira da equipe; com um da equipe escolhido, entra no lugar dele (ou os
	///   dois trocam de lugar, se já estava nela). Segurar qualquer monstro abre o resumo.
	///
	/// Não há equipes prontas: cada conteúdo (a Campanha, cada Masmorra, a Exploração) guarda a última
	/// equipe montada aqui (<see cref="Teams"/>), e a preparação abre com ela. Trocar não remonta a lista:
	/// só o ✓ e o destaque dos cartões, e a formação, que tem cinco.
	/// </summary>
	public partial class PrepScreen : Control
	{
		/// <summary>Os cartões da formação: duas fileiras cabem ao lado dos inimigos, com a coleção embaixo.</summary>
		private const float SlotWidth = 100;

		/// <summary>Os cartões da coleção, na lista que rola de lado.</summary>
		private const float CardWidth = 104;

		/// <summary>Quantos vão na fileira da frente da formação; o resto, atrás.</summary>
		private const int FrontRow = 3;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly string _title;
		private readonly string _content;
		private readonly Encounter _encounter;
		private readonly int _mana;
		private readonly bool _auto;
		private MonsterFilter _filter;

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _formation = new() { Name = "Formation", Alignment = BoxContainer.AlignmentMode.Center };
		private readonly VBoxContainer _side = new() { Name = "Side" };
		private readonly GridContainer _search = Layout.Grid(2, 8).Named("Search");
		private readonly HBoxContainer _cards = Layout.Row(8).Named("Cards");
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private ScrollContainer _scroll = null!;
		private GameButton _fight = null!;
		private GameButton? _autoBattle;

		/// <summary>Os cartões da coleção, pelo monstro: pôr ou tirar da equipe só troca o ✓ deles.</summary>
		private readonly Dictionary<int, CreatureCard> _listed = new();

		/// <summary>O monstro da equipe escolhido: o próximo toque na coleção entra no lugar dele.</summary>
		private int? _chosen;

		/// <param name="title">O nome da luta ("Fase 12 · …").</param>
		/// <param name="content">A chave da equipe (<see cref="Teams"/>).</param>
		/// <param name="mana">O custo da vitória; 0 não mostra custo.</param>
		/// <param name="auto">A luta já tem Batalha automática.</param>
		/// <param name="filter">A busca da coleção da última vez.</param>
		public PrepScreen(GameDatabase database, PlayerState player, string title, string content, Encounter encounter, int mana, bool auto, MonsterFilter filter)
		{
			_database = database;
			_player = player;
			_title = title;
			_content = content;
			_encounter = encounter;
			_mana = mana;
			_auto = auto;
			_filter = filter;
		}

		public event Action? FightRequested;
		public event Action? AutoBattleRequested;

		/// <summary>Põe o monstro na equipe ou tira.</summary>
		public event Action<int>? ToggleRequested;

		/// <summary>Quem sai e quem entra na vaga dele (<see cref="Teams.Replace"/>).</summary>
		public event Action<int, int>? ReplaceRequested;

		public event Action<int>? LeaderRequested;

		/// <summary>O jogador mudou o filtro ou a ordem da coleção.</summary>
		public event Action<MonsterFilter>? FilterChanged;

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(_title, _currencies, () => BackRequested?.Invoke()).Header);

			var top = Layout.Row(16).Named("Top");
			var team = new PanelContainer { Name = "Team", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var teamRow = Layout.Row(20).Named("Row");
			_formation.AddThemeConstantOverride("separation", 8);
			teamRow.AddChild(_formation);
			_side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_side.AddThemeConstantOverride("separation", 10);
			teamRow.AddChild(_side);
			team.AddChild(teamRow);
			top.AddChild(team);
			top.AddChild(Enemies());
			page.AddChild(top);

			var collection = new PanelContainer { Name = "Collection", SizeFlagsVertical = SizeFlags.ExpandFill };
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", 8);
			var tools = Layout.Row(12).Named("Tools");
			tools.AddChild(new Label { Name = "Title", Text = T("prep.collection"), ThemeTypeVariation = GameTheme.Heading, VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			_search.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			_search.CustomMinimumSize = new Vector2(420, 0);
			tools.AddChild(_search);
			column.AddChild(tools);
			// A lista rola de lado: a roda do mouse e o arrasto também (sem barra de pé, a roda anda na horizontal).
			_scroll = new ScrollContainer
			{
				Name = "Scroll",
				HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
				VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
				SizeFlagsVertical = SizeFlags.ExpandFill,
				ScrollHorizontalCustomStep = CardWidth * 2,
			};
			_scroll.AddChild(_cards);
			DragScroll.Enable(_scroll);
			column.AddChild(_scroll);
			collection.AddChild(column);
			page.AddChild(collection);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);

			RefreshList();
			Refresh();
		}

		/// <summary>Depois de cada troca: a formação, a coluna ao lado e o ✓ da coleção, sem remontar a lista.</summary>
		public void Refresh()
		{
			_currencies.Refresh(_player);
			var team = Teams.Of(_player, _content);
			if (_chosen is { } chosen && !team.Contains(chosen))
				_chosen = null;

			RefreshFormation();
			RefreshSide();
			foreach (var (id, card) in _listed)
			{
				card.SetMarked(team.Contains(id));
				card.SetSelected(id == _chosen);
			}

			var ready = Members().Count > 0;
			_fight.Disabled = !ready;
			if (_autoBattle != null)
				_autoBattle.Disabled = !ready;
		}

		/// <summary>Os monstros da equipe que lutam, na ordem (a Líder primeiro).</summary>
		private List<OwnedSummon> Members() => Teams.Of(_player, _content)
			.Select(_player.Monster)
			.OfType<OwnedSummon>()
			.Where(m => !m.Stored && _database.HasSummon(m.SummonId))
			.ToList();

		// Equipe ------------------------------------------------------------------------------------

		private void RefreshFormation()
		{
			Layout.Clear(_formation);
			var team = Members();
			var front = Layout.Row(10, centered: true).Named("Front");
			var back = Layout.Row(10, centered: true).Named("Back");
			for (var i = 0; i < PlayerState.TeamSize; i++)
				(i < FrontRow ? front : back).AddChild(Slot(i, i < team.Count ? team[i] : null));
			_formation.AddChild(front);
			_formation.AddChild(back);
		}

		/// <summary>Uma vaga: o cartão do monstro (a Líder com a faixa) ou a pedra vazia.</summary>
		private Control Slot(int index, OwnedSummon? monster)
		{
			if (monster == null)
			{
				var empty = new PanelContainer { Name = $"Slot{index + 1}", CustomMinimumSize = new Vector2(SlotWidth, SlotWidth * 1.25f), ThemeTypeVariation = GameTheme.InsetPanel, MouseFilter = MouseFilterEnum.Ignore };
				var text = new Label { Name = "Text", Text = T("teams.empty"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
				text.AddThemeColorOverride("font_color", new Color(Palette.GoldDark, 0.9f));
				empty.AddChild(text);
				return empty;
			}

			var leader = index == 0;
			var card = new CreatureCard(_database.Summon(monster.SummonId), monster, SlotWidth, leader ? "leader" : null, leader ? T("teams.leader") : null) { Name = $"Slot{index + 1}" };
			card.SetSelected(monster.Id == _chosen);
			card.Pressed += _ => Choose(monster.Id);
			return card;
		}

		/// <summary>Escolhe (ou larga) um monstro da equipe: o próximo da coleção entra no lugar dele.</summary>
		private void Choose(int id)
		{
			_message.Text = "";
			_chosen = _chosen == id ? null : id;
			Sfx.Play(_chosen != null ? "ui.toggle_on" : "ui.toggle_off");
			Refresh();
		}

		/// <summary>
		/// Ao lado da formação: o conteúdo e quantos há na equipe, a Liderança e, com um monstro escolhido, o
		/// que fazer com ele; sem escolha, como se monta a equipe.
		/// </summary>
		private void RefreshSide()
		{
			Layout.Clear(_side);
			var team = Members();
			var header = Layout.Row(10).Named("Header");
			header.AddChild(new Label { Name = "Title", Text = T("prep.team", Texts.ContentName(_database, _content)), ThemeTypeVariation = GameTheme.Heading });
			header.AddChild(new Label { Name = "Count", Text = $"{team.Count}/{PlayerState.TeamSize}", ThemeTypeVariation = GameTheme.Faded, VerticalAlignment = VerticalAlignment.Center });
			_side.AddChild(header);

			var leader = team.Count > 0 ? _database.Summon(team[0].SummonId) : null;
			if (leader?.Leader is { } skill)
				_side.AddChild(RichText.Label(T("teams.leadership_text", leader.NameFor(team[0].Awakened), Texts.Percent(skill.Value), Texts.Name(skill.Stat))).Named("Leadership"));
			else
				_side.AddChild(Layout.Text(team.Count == 0 ? T("teams.no_team") : T("teams.no_leadership"), GameTheme.Faded).Named("Leadership"));

			if (team.FirstOrDefault(m => m.Id == _chosen) is { } chosen)
			{
				_side.AddChild(Layout.Text(T("prep.chosen", _database.Summon(chosen.SummonId).NameFor(chosen.Awakened))).Named("Chosen"));
				var buttons = Layout.Grid(2, 8).Named("Actions");
				if (team[0].Id != chosen.Id)
					buttons.AddChild(GameButton.Of(T("teams.make_leader"), () =>
					{
						_chosen = null;
						LeaderRequested?.Invoke(chosen.Id);
					}, ButtonKind.Primary, "leader", 44).Named("MakeLeader"));
				buttons.AddChild(GameButton.Of(T("teams.remove"), () => ToggleRequested?.Invoke(chosen.Id), ButtonKind.Secondary, "cancel", 44).Named("Remove"));
				_side.AddChild(buttons);
			}
			else
			{
				_side.AddChild(Layout.Text(T("prep.hint"), GameTheme.Faded).Named("Hint"));
			}

			_side.AddChild(Layout.Text(T("prep.saved"), GameTheme.Faded).Named("Saved"));
		}

		// Inimigos ----------------------------------------------------------------------------------

		/// <summary>A última onda, em formação (o chefe na frente; sem chefe, o primeiro), e os botões da luta.</summary>
		private Control Enemies()
		{
			var panel = new PanelContainer { Name = "Enemies", CustomMinimumSize = new Vector2(440, 0) };
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", 8);

			var last = _encounter.Waves[^1];
			var lead = Enumerable.Range(0, last.Count).Where(i => IsBoss(last[i])).ToList();
			var title = new Label { Name = "Title", Text = lead.Count > 0 ? T("prep.boss_room") : T("prep.last_wave"), ThemeTypeVariation = GameTheme.Heading };
			column.AddChild(title);
			column.AddChild(new Label { Name = "Wave", Text = T("prep.wave", _encounter.Waves.Count, T("common.stars_level", Texts.Stars(_encounter.Stars), _encounter.Level)), ThemeTypeVariation = GameTheme.Faded });
			if (lead.Count == 0 && last.Count > 0)
				lead.Add(0);

			var formation = new VBoxContainer { Name = "Formation", SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			formation.AddThemeConstantOverride("separation", 10);
			formation.AddChild(FoeRow("Front", lead.Select(i => last[i]), 84));
			var rest = Enumerable.Range(0, last.Count).Where(i => !lead.Contains(i)).Select(i => last[i]).ToList();
			if (rest.Count > 0)
				formation.AddChild(FoeRow("Back", rest, 60));
			column.AddChild(formation);

			var buttons = Layout.Grid(_auto ? 2 : 1, 10).Named("Buttons");
			_fight = GameButton.Of(T("common.fight"), () => FightRequested?.Invoke(), ButtonKind.Primary, "fight", 60).Named("Fight");
			if (_mana > 0)
				_fight.WithCost("mana", _mana.ToString());
			buttons.AddChild(_fight);
			if (_auto)
			{
				_autoBattle = GameButton.Of(T("common.auto_battle"), () => AutoBattleRequested?.Invoke(), ButtonKind.Secondary, "repeat", 60).Named("AutoBattle");
				buttons.AddChild(_autoBattle);
			}

			column.AddChild(buttons);
			panel.AddChild(column);
			return panel;
		}

		private bool IsBoss(StageEnemy slot) => slot.Guardian || slot.Enemy != null && !_database.Enemy(slot.Enemy).Minion;

		private Control FoeRow(string name, IEnumerable<StageEnemy> slots, float size)
		{
			var row = Layout.Row(10, centered: true).Named(name);
			var number = 0;
			foreach (var slot in slots)
				row.AddChild(Foe(slot, size).Named($"Foe{++number}"));
			return row;
		}

		/// <summary>Um inimigo: o retrato na cor do elemento (o chefe com a moldura de ouro); tocar ou segurar abre o resumo.</summary>
		private Control Foe(StageEnemy slot, float size)
		{
			var (_, image, element) = _database.Foe(slot);
			var boss = IsBoss(slot);
			var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, boss ? Palette.Gold : Palette.Of(element).Darkened(0.3f), boss ? 3 : 2, (int)(size / 2) + 4, 3));
			frame.AddChild(Layout.Medal(Art.Creature(image), Palette.Of(element), size));
			void Open() => MonsterSummary.Open(frame, BattleFactory.Foe(_database, slot, _encounter));
			Press.On(frame, Open, Open);
			return frame;
		}

		// Coleção -----------------------------------------------------------------------------------

		/// <summary>A lista da coleção, na busca de agora: só ela remonta tudo (ao mudar o filtro ou a ordem).</summary>
		private void RefreshList()
		{
			Layout.Clear(_search);
			MonsterSearch.Fill(_search, _database, _filter, filter =>
			{
				_filter = filter;
				FilterChanged?.Invoke(filter);
				Callable.From(() =>
				{
					RefreshList();
					Refresh();
				}).CallDeferred();
			});

			Layout.Clear(_cards);
			_listed.Clear();
			foreach (var monster in _filter.Apply(_player.Collection.Where(m => !m.Stored && !m.IsInfusionCore), _database, _player))
			{
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, CardWidth) { Name = $"Monster{monster.Id}" };
				card.Pressed += c => Pick(c.Monster!.Id);
				_cards.AddChild(card);
				_listed[monster.Id] = card;
			}

			if (_listed.Count == 0)
				_cards.AddChild(new Label { Name = "Empty", Text = T("prep.none"), ThemeTypeVariation = GameTheme.Faded });
			_scroll.ScrollHorizontal = 0;
		}

		/// <summary>
		/// Um toque na coleção: com um monstro da equipe escolhido, entra no lugar dele; sem escolha, põe ou
		/// tira. Equipe cheia e nada escolhido: o aviso diz como trocar.
		/// </summary>
		private void Pick(int id)
		{
			_message.Text = "";
			if (_chosen is { } chosen)
			{
				_chosen = null;
				if (chosen == id)
					Refresh();
				else
					ReplaceRequested?.Invoke(chosen, id);
				return;
			}

			var team = Teams.Of(_player, _content);
			if (!team.Contains(id) && team.Count >= PlayerState.TeamSize)
			{
				_message.Text = T("prep.full");
				Sfx.Play("ui.action_unavailable");
				return;
			}

			ToggleRequested?.Invoke(id);
		}
	}
}
