using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Arena Astral: no alto, a conta na Arena (o Grau e a barra até o próximo, os Convites Astrais e quando
	/// chega o próximo, o Astrarium e a sequência de vitórias com o bônus de agora). Embaixo dela quatro
	/// botões: Oponentes (a lista de dez, cada um com a defesa, os pontos que dá e o Lutar), Rivais (equipes
	/// fixas com espera), Defesa (a equipe que defende, na preparação) e a Loja Cósmica. Só mostra e avisa:
	/// quem gasta Convite, luta e salva é o GameRoot.
	/// </summary>
	public partial class ArenaScreen : Control
	{
		/// <summary>As abas da Arena; o GameRoot guarda a de agora para voltar nela depois de uma preparação.</summary>
		public enum View
		{
			Defense,
			Opponents,
			Rivals,
		}

		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly DateTime _now;

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _status = new() { Name = "Status" };
		private readonly GridContainer _tabs = Layout.Grid(1, 10).Named("Tabs");
		private readonly VBoxContainer _list = new() { Name = "List" };
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private View _view = View.Defense;

		public View Current { get => _view; init => _view = value; }

		public event Action<View>? TabChanged;
		private bool _editing;
		private int? _chosen;
		private MonsterFilter _filter = new();
		private const float DefenseCardWidth = 150;

		public ArenaScreen(GameDatabase database, PlayerState player, DateTime now)
		{
			_database = database;
			_player = player;
			_now = now;
		}

		/// <summary>Lutar com o da lista; o GameRoot abre a preparação.</summary>
		public event Action<int>? OpponentRequested;

		/// <summary>A busca da coleção na edição da defesa (a mesma da preparação).</summary>
		public MonsterFilter Filter { get => _filter; init => _filter = value; }

		public event Action<ArenaRival>? RivalRequested;
		/// <summary>A edição da defesa: pôr ou tirar (id), trocar (sai, entra) e tornar Líder; o GameRoot guarda e chama <see cref="Refresh"/>.</summary>
		public event Action<int>? ToggleRequested;

		public event Action<int, int>? ReplaceRequested;
		public event Action<int>? LeaderRequested;
		public event Action<MonsterFilter>? FilterChanged;
		public event Action? ShopRequested;

		/// <summary>Uma lista nova, de graça; a sequência acaba.</summary>
		public event Action? RefreshRequested;

		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Arena"), _currencies, () => BackRequested?.Invoke()).Header);

			// Duas colunas, 30/70: à esquerda a conta (o Grau em destaque) e os botões; à direita a lista.
			var columns = new HBoxContainer { Name = "Columns", SizeFlagsVertical = SizeFlags.ExpandFill };
			columns.AddThemeConstantOverride("separation", Space.Wide);
			page.AddChild(columns);

			var side = new VBoxContainer { Name = "Side", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 30, CustomMinimumSize = new Vector2(0, 0) };
			side.AddThemeConstantOverride("separation", Space.Medium);
			columns.AddChild(side);

			var status = new PanelContainer { Name = "StatusPanel" };
			_status.AddThemeConstantOverride("separation", Space.Regular);
			status.AddChild(_status);
			side.AddChild(status);
			side.AddChild(_tabs);

			var scroll = Layout.Scroll(_list);
			scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scroll.SizeFlagsStretchRatio = 70;
			scroll.CustomMinimumSize = new Vector2(0, 0);
			_list.AddThemeConstantOverride("separation", Space.Medium);
			columns.AddChild(scroll);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);
			Refresh();
		}

		public void ShowMessage(string text) => _message.Text = text;

		public void Refresh()
		{
			_currencies.Refresh(_player);
			RefreshStatus();
			RefreshTabs();
			RefreshList();
		}

		// A conta ------------------------------------------------------------------------------------

		private void RefreshStatus()
		{
			Layout.Clear(_status);
			var arena = _database.Arena;
			var state = _player.Arena;
			var grade = arena.GradeOf(state.Points);
			var index = arena.IndexOf(grade);
			var next = index + 1 < arena.Grades.Count ? arena.Grades[index + 1] : null;

			// O Grau de agora, em destaque: o lugar do símbolo do Grau (arte rank_<id>, ou o alvo até existir),
			// o nome grande e os pontos como XXX/XXXX até o próximo Grau.
			var rank = Layout.Row(Space.Wide, true).Named("Rank");
			var slot = new PanelContainer { Name = "RankSymbol", CustomMinimumSize = new Vector2(64, 64) };
			slot.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Gold, 3, 44, 6));
			slot.AddChild(Layout.Medal(Art.Icon($"rank_{grade.Id}") ?? Art.Icon("target"), Palette.Gold, 50));
			rank.AddChild(slot);
			var title = new VBoxContainer { Name = "RankText", Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var name = new Label { Name = "RankName", Text = grade.Name, ThemeTypeVariation = GameTheme.Title, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			name.AddThemeFontSizeOverride("font_size", 26);
			title.AddChild(name);
			var points = next != null ? $"{Texts.Number(state.Points)}/{Texts.Number(next.Points)}" : Texts.Number(state.Points);
			var pointsRow = Layout.Row(Space.Regular).Named("PointsRow");
			pointsRow.AddChild(new Label { Name = "RankPoints", Text = points, ThemeTypeVariation = GameTheme.Heading, VerticalAlignment = VerticalAlignment.Center });
			pointsRow.AddChild(SigilButton.Of("help", OpenPointsHelp, 40).Named("PointsHelp"));
			title.AddChild(pointsRow);
			rank.AddChild(title);
			_status.AddChild(rank);
			_status.AddChild(Layout.Text(next != null ? T("arena.next_grade", Texts.Number(Math.Max(0, next.Points - state.Points)), next.Name) : T("arena.top_grade"), GameTheme.Faded).Named("NextGrade"));

			_status.AddChild(Layout.Labeled("astral_invite", $"{state.Invites}/{arena.MaxInvites}", NextInviteText(), Palette.Arcane).Named("Invites"));
			_status.AddChild(Layout.Labeled("astrarium", Texts.Number(state.Astrarium), T("arena.astrarium"), Palette.Spirit).Named("Astrarium"));
			_status.AddChild(Layout.Labeled("fight", state.Streak.ToString(), StreakText(), Palette.Gold).Named("Streak"));

			var left = Arena.CycleEnds(_now) - _now;
			_status.AddChild(Layout.Text(T("arena.cycle_short", Duration(left)), GameTheme.Faded).Named("Cycle"));
		}

		/// <summary>O "?" dos pontos: como se ganha e perde, a semana e o que cada Grau paga.</summary>
		private void OpenPointsHelp()
		{
			var arena = _database.Arena;
			var dialog = Dialog.Open(this, T("arena.help_title"), 640, null, "PointsHelp");
			dialog.Body.AddChild(Layout.Text(T("arena.help_points", arena.PointsMin, arena.PointsMax), null, 590).Named("Points"));
			dialog.Body.AddChild(Layout.Text(T("arena.help_week"), null, 590).Named("Week"));
			dialog.Body.AddChild(Layout.Text(T("arena.help_rewards"), GameTheme.Faded, 590).Named("RewardsTitle"));
			var grid = Layout.Grid(2, 6).Named("Grades");
			foreach (var grade in arena.Grades.OrderByDescending(g => g.Points))
			{
				var legendary = grade.LegendaryScrolls > 0 ? T("arena.payout_legendary", grade.LegendaryScrolls) : "";
				grid.AddChild(new Label { Name = $"{grade.Id}_Name", Text = $"{grade.Name} · {Texts.Number(grade.Points)}+", ThemeTypeVariation = GameTheme.Heading });
				grid.AddChild(Layout.Text(T("arena.help_grade", Texts.Number(grade.Gold), grade.Astrarium, legendary), null, 330).Named($"{grade.Id}_Reward"));
			}

			dialog.Body.AddChild(grid);
		}

		private string NextInviteText()
		{
			if (Arena.NextInvite(_player, _database.Arena) is not { } at)
				return T("arena.invites_full");
			return T("arena.invites_next", Duration(at - _now));
		}

		private string StreakText()
		{
			var (health, attack) = Arena.StreakBonus(_player, _database.Arena, _now);
			if (health == 0 && attack == 0)
			{
				// Já venceu, mas ainda não chegou ao primeiro degrau: diz quantas faltam.
				var next = _database.Arena.Streak.FirstOrDefault(s => s.Wins > _player.Arena.Streak);
				return _player.Arena.Streak > 0 && next != null && _player.Arena.StreakUntil > _now
					? T("arena.streak_building", next.Wins - _player.Arena.Streak)
					: T("arena.streak_none");
			}

			var left = _player.Arena.StreakUntil is { } until ? Duration(until - _now) : "";
			return T("arena.streak_bonus", Texts.Number((int)Math.Round(health * 100)), Texts.Number((int)Math.Round(attack * 100)), left);
		}

		private static string Duration(TimeSpan span)
		{
			if (span < TimeSpan.Zero)
				span = TimeSpan.Zero;
			return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h{span.Minutes:00}" : $"{Math.Max(1, (int)Math.Ceiling(span.TotalMinutes))}min";
		}

		// Botões -------------------------------------------------------------------------------------

		private void RefreshTabs()
		{
			Layout.Clear(_tabs);
			_tabs.AddChild(Tab(T("arena.defense"), View.Defense, "team").Named("Defense"));
			_tabs.AddChild(Tab(T("arena.tab_opponents"), View.Opponents, "target").Named("Opponents"));
			_tabs.AddChild(Tab(T("arena.tab_rivals"), View.Rivals, "fight").Named("Rivals"));
			_tabs.AddChild(GameButton.Of(T("arena.cosmic_shop"), () => ShopRequested?.Invoke(), ButtonKind.Secondary, "astrarium", 52).Named("CosmicShop"));
		}

		private GameButton Tab(string text, View view, string icon) => GameButton.Of(text, () =>
		{
			_view = view;
			TabChanged?.Invoke(view);
			_editing = false;
			_chosen = null;
			_message.Text = "";
			Refresh();
		}, _view == view ? ButtonKind.Primary : ButtonKind.Secondary, icon, 52);

		// Lista --------------------------------------------------------------------------------------

		private void RefreshList()
		{
			Layout.Clear(_list);
			// A defesa fica no meio da coluna; as listas começam no alto.
			_list.SizeFlagsVertical = SizeFlags.ExpandFill;
			_list.Alignment = _view == View.Defense ? BoxContainer.AlignmentMode.Center : BoxContainer.AlignmentMode.Begin;
			if (_view == View.Defense)
				FillDefense();
			else if (_view == View.Opponents)
				FillOpponents();
			else
				FillRivals();
		}

		/// <summary>
		/// A defesa de agora, em cartões, no meio da coluna. Editar defesa abre aqui mesmo a coleção abaixo dela:
		/// tocar num da coleção põe ou tira da equipe; com um da equipe escolhido, o da coleção entra no lugar.
		/// </summary>
		private void FillDefense()
		{
			var team = Teams.Of(_player, Teams.ArenaDefense).Select(_player.Monster).OfType<OwnedSummon>()
				.Where(m => !m.Stored && _database.HasSummon(m.SummonId)).ToList();
			if (_chosen is { } picked && team.All(m => m.Id != picked))
				_chosen = null;

			var row = Layout.Row(Space.Regular, true).Named("DefenseTeam");
			for (var i = 0; i < team.Count; i++)
			{
				var member = team[i];
				var leader = i == 0;
				var card = new CreatureCard(_database.Summon(member.SummonId), member, _editing ? 110 : DefenseCardWidth, leader ? "leader" : null, leader ? T("teams.leader") : null) { Name = $"Defender{i + 1}" };
				if (_editing)
				{
					card.SetSelected(member.Id == _chosen);
					card.Pressed += _ =>
					{
						_chosen = _chosen == member.Id ? null : member.Id;
						_message.Text = "";
						Refresh();
					};
				}

				row.AddChild(card);
			}

			if (team.Count == 0)
				row.AddChild(Layout.Text(T("teams.no_team"), GameTheme.Faded).Named("Empty"));
			_list.AddChild(row);

			if (!_editing)
			{
				var edit = GameButton.Of(T("arena.defense_edit"), () =>
				{
					_editing = true;
					Refresh();
				}, ButtonKind.Primary, "team", 56).Named("EditDefense");
				edit.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
				_list.AddChild(edit.Wide(260));
				return;
			}

			// Os botões da edição ficam sempre nos mesmos lugares: sem escolha, Líder e Remover só apagam.
			var actions = Layout.Row(Space.Regular, true).Named("DefenseActions");
			actions.AddChild(SigilButton.Of("help", () => Dialog.Info(this, T("arena.defense_edit"), T("prep.hint") + "\n\n" + T("arena.defense_text"), 520), 40).Named("DefenseHelp"));
			var chosen = team.FirstOrDefault(m => m.Id == _chosen);
			var leaderButton = GameButton.Of(T("teams.make_leader"), () =>
			{
				var id = chosen!.Id;
				_chosen = null;
				LeaderRequested?.Invoke(id);
			}, ButtonKind.Primary, "leader", 44).Named("MakeLeader");
			leaderButton.Block(chosen == null ? T("teams.why_pick") : team[0].Id == chosen.Id ? T("teams.why_leader") : null, T("teams.make_leader"));
			actions.AddChild(leaderButton);
			var removeButton = GameButton.Of(T("teams.remove"), () => ToggleRequested?.Invoke(chosen!.Id), ButtonKind.Secondary, "cancel", 44).Named("Remove");
			removeButton.Block(chosen == null ? T("teams.why_pick") : null, T("teams.remove"));
			actions.AddChild(removeButton);
			actions.AddChild(GameButton.Of(T("arena.defense_done"), () =>
			{
				_editing = false;
				_chosen = null;
				Refresh();
			}, ButtonKind.Secondary, null, 44).Named("Done"));
			_list.AddChild(actions);

			var search = Layout.Grid(2, 8).Named("Search");
			MonsterSearch.Fill(search, _database, _filter, filter =>
			{
				_filter = filter;
				FilterChanged?.Invoke(filter);
				Callable.From(Refresh).CallDeferred();
			});
			_list.AddChild(search);

			var cards = Layout.Row(Space.Medium).Named("Collection");
			var inTeam = team.Select(m => m.Id).ToHashSet();
			foreach (var monster in _filter.Apply(_player.Collection.Where(m => !m.Stored && !m.IsInfusionCore), _database, _player))
			{
				var id = monster.Id;
				var card = new CreatureCard(_database.Summon(monster.SummonId), monster, 96) { Name = $"Monster{id}" };
				card.SetMarked(inTeam.Contains(id));
				card.Pressed += _ => Pick(id, inTeam);
				cards.AddChild(card);
			}

			var scroll = new ScrollContainer
			{
				Name = "CollectionScroll",
				HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
				VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
				CustomMinimumSize = new Vector2(0, 96 * 1.25f + 14),
			};
			scroll.AddChild(cards);
			_list.AddChild(scroll);
		}

		/// <summary>Um toque na coleção: com um da equipe escolhido, entra no lugar dele; sem escolha, põe ou tira.</summary>
		private void Pick(int id, HashSet<int> inTeam)
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

			if (!inTeam.Contains(id) && inTeam.Count >= Teams.SizeOf(Teams.ArenaDefense))
			{
				_message.Text = T("prep.full");
				return;
			}

			ToggleRequested?.Invoke(id);
		}

		private void FillOpponents()
		{
			var arena = _database.Arena;
			var opponents = Arena.Opponents(_player, _database);
			var beaten = opponents.Count(o => o.Beaten);
			var header = Layout.Row(Space.Wide).Named("ListHeader");
			var text = Layout.Text(T("arena.list_short", beaten, opponents.Count), GameTheme.Faded, 10).Named("Progress");
			text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			header.AddChild(text);
			header.AddChild(Layout.HelpButton(this, T("arena.tab_opponents"), T("arena.list_progress", beaten, opponents.Count, arena.ListBonusInvites)));
			header.AddChild(GameButton.Of(T("arena.refresh"), () => RefreshRequested?.Invoke(), ButtonKind.Secondary, "repeat", 44).Named("Renew"));
			_list.AddChild(header);

			foreach (var opponent in opponents)
				_list.AddChild(OpponentRow(opponent, arena).Named($"Opponent{opponent.Index + 1}"));
		}

		private Control OpponentRow(ArenaOpponent opponent, ArenaDefinition arena)
		{
			var panel = new PanelContainer();
			var row = Layout.Row(Space.Wide).Named("Row");
			row.AddThemeConstantOverride("separation", Space.Wide);
			panel.AddChild(row);

			var who = new VBoxContainer { Name = "Who", CustomMinimumSize = new Vector2(10, 0), Alignment = BoxContainer.AlignmentMode.Center };
			who.AddChild(new Label { Name = "Name", Text = opponent.Name, ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(10, 0) });
			who.AddChild(new Label { Name = "Grade", Text = $"{opponent.Grade.Name} · {T("arena.points", Texts.Number(opponent.Points))}", ThemeTypeVariation = GameTheme.Faded });
			who.AddChild(Layout.Text(T("arena.stake", Arena.Gain(arena, _player.Arena.Points, opponent.Points), Arena.Loss(arena, _player.Arena.Points, opponent.Points)), GameTheme.Faded).Named("Stake"));
			who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(who);

			row.AddChild(Team("Defense", opponent.Team));

			// Lutar fica sempre no mesmo lugar: vencido, só apaga (o aviso vai embaixo do nome).
			if (opponent.Beaten)
				who.AddChild(new Label { Name = "Beaten", Text = T("arena.beaten"), ThemeTypeVariation = GameTheme.Faded });
			var canFight = Arena.CanFight(_player, arena, opponent.Index);
			var fight = GameButton.Of(T("common.fight"), () =>
			{
				if (_player.Arena.Invites <= 0)
					ShowMessage(T("arena.no_invites"));
				else
					OpponentRequested?.Invoke(opponent.Index);
			}, ButtonKind.Primary, "fight", 52).WithCost("astral_invite", "1").Named("Fight");
			fight.Block(opponent.Beaten ? T("arena.why_beaten") : !canFight && _player.Arena.Invites > 0 ? T("arena.why_pending") : null, T("common.fight"));
			fight.Wide(120).SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			row.AddChild(fight);

			return panel;
		}

		private void FillRivals()
		{
			var arena = _database.Arena;
			var header = Layout.Row(Space.Wide).Named("ListHeader");
			header.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			header.AddChild(Layout.HelpButton(this, T("arena.tab_rivals"), T("arena.rivals_text")));
			_list.AddChild(header);
			foreach (var rival in arena.Rivals)
				_list.AddChild(RivalRow(rival, arena).Named($"Rival_{rival.Id}"));
		}

		private Control RivalRow(ArenaRival rival, ArenaDefinition arena)
		{
			var panel = new PanelContainer();
			var row = Layout.Row(Space.Wide).Named("Row");
			panel.AddChild(row);

			var who = new VBoxContainer { Name = "Who", CustomMinimumSize = new Vector2(10, 0), Alignment = BoxContainer.AlignmentMode.Center };
			who.AddChild(new Label { Name = "Name", Text = rival.Name, ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(10, 0) });
			who.AddChild(new Label { Name = "Level", Text = T("common.stars_level", Texts.Stars(rival.Team.Stars), rival.Team.Level), ThemeTypeVariation = GameTheme.Faded });
			who.AddChild(Layout.Text(T("arena.rival_reward", arena.GradeOf(_player.Arena.Points).Astrarium, rival.Gold), GameTheme.Faded).Named("Reward"));
			who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(who);

			row.AddChild(Team("Team", ArenaTeams.Rival(_database, rival)));

			// Em espera: Lutar continua no lugar, apagado, e o tempo que falta vai embaixo do nome.
			var waiting = Arena.RivalReadyAt(_player, rival, _now);
			if (waiting is { } at)
				who.AddChild(new Label { Name = "Wait", Text = T("arena.rival_wait", Duration(at - _now)), ThemeTypeVariation = GameTheme.Faded });
			var rivalFight = GameButton.Of(T("common.fight"), () =>
			{
				if (_player.Arena.Invites <= 0)
					ShowMessage(T("arena.no_invites"));
				else
					RivalRequested?.Invoke(rival);
			}, ButtonKind.Primary, "fight", 52).WithCost("astral_invite", "1").Named("Fight");
			rivalFight.Block(waiting is { } until ? T("arena.rival_wait", Duration(until - _now)) : null, T("common.fight"));
			rivalFight.Wide(120).SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			row.AddChild(rivalFight);

			return panel;
		}

		/// <summary>Os quatro da defesa, em medalhas: tocar ou segurar abre o resumo.</summary>
		private static Control Team(string name, BattleTeam team)
		{
			var medals = Layout.Row(Space.Regular).Named(name);
			for (var i = 0; i < team.Members.Count; i++)
			{
				var unit = BattleFactory.DefenderUnit(team, i);
				var frame = new PanelContainer { Name = $"Member{i + 1}", SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
				frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, i == 0 ? Palette.Gold : Palette.Of(unit.Element).Darkened(0.3f), i == 0 ? 3 : 2, 34, 3));
				frame.AddChild(Layout.Medal(Art.Creature(unit.Image), Palette.Of(unit.Element), 52));
				void Open() => MonsterSummary.Open(frame, unit);
				Press.On(frame, Open, Open);
				medals.AddChild(frame);
			}

			return medals;
		}
	}
}
