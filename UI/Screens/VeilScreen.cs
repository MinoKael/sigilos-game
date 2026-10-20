using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Rta;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Arena do Véu, o duelo em tempo real contra outro jogador. Três momentos numa tela só: o saguão (o Grau, o
	/// Áster, vitórias e derrotas, os Ecos do Véu e os botões Entrar na fila, Treino e Loja do Véu), a fila (quanto
	/// tempo faz, com o Treino contra um fantasma depois de um tempo) e a montagem do time (escolhas alternadas,
	/// banimento e Líder, com o prazo de cada um). Só mostra e avisa: quem fala com o servidor e guarda a conta é o
	/// GameRoot, que entrega cada mensagem em <see cref="Receive"/>.
	/// </summary>
	public partial class VeilScreen : Control
	{
		private enum Mode
		{
			Lobby,
			Queue,
			Draft,
		}

		/// <summary>Segundos de fila até a tela oferecer o Treino contra um fantasma.</summary>
		private const int GhostAfterSeconds = 30;

		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly VBoxContainer _content = new() { Name = "Content", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		private readonly Godot.Timer _tick = new() { Name = "Tick", WaitTime = 0.25, Autostart = true };

		private Mode _mode = Mode.Lobby;
		private bool _live;
		private bool _trying;
		private double _queuedAt;
		private double _deadline;
		private Label? _queueLabel;
		private Control? _ghostButton;
		private Label? _clock;
		private bool _clockMine;

		private RtaSeat _seat;
		private string _rival = "";
		private int _rivalAster;
		private bool _ranked;
		private RtaMessage? _draft;

		public VeilScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
		}

		/// <summary>Entrar na fila; com <c>true</c>, o Treino contra um fantasma.</summary>
		public event Action<bool>? QueueRequested;

		public event Action? CancelRequested;
		public event Action<string>? PickRequested;
		public event Action<string>? BanRequested;
		public event Action<string>? LeadRequested;

		/// <summary>Desistir da montagem do time (vale como derrota).</summary>
		public event Action? LeaveRequested;

		public event Action? ShopRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("veil.title"), _currencies, Back, this, T("veil.help_rules"), T("veil.help_draft"), T("veil.help_battle"), T("veil.help_points")).Header);
			page.AddChild(_content);
			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);
			_tick.Timeout += Tick;
			AddChild(_tick);
			Refresh();
		}

		private void Back()
		{
			// Na montagem, voltar é desistir; na fila, é sair dela; no saguão, é voltar à Arena.
			if (_mode == Mode.Draft)
				Dialog.Confirm(this, T("veil.leave_title"), T("veil.leave_text"), T("veil.leave"), () => LeaveRequested?.Invoke(), ButtonKind.Danger);
			else if (_mode == Mode.Queue)
				CancelRequested?.Invoke();
			else
				BackRequested?.Invoke();
		}

		public void ShowMessage(string text) => _message.Text = text;

		/// <summary>A conexão com o servidor abriu (<c>true</c>) ou fechou. Fechada em plena montagem, a tela volta ao saguão.</summary>
		public void SetLive(bool live, bool trying)
		{
			_live = live;
			_trying = trying;
			if (!live && _mode != Mode.Lobby)
				_mode = Mode.Lobby;
			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			Layout.Clear(_content);
			_queueLabel = null;
			_ghostButton = null;
			_clock = null;
			switch (_mode)
			{
				case Mode.Lobby:
					BuildLobby();
					break;
				case Mode.Queue:
					BuildQueue();
					break;
				default:
					BuildDraft();
					break;
			}
		}

		/// <summary>Uma mensagem do servidor para a tela: fila, adversário achado, estado da montagem ou recusa.</summary>
		public void Receive(RtaMessage message)
		{
			switch (message.Type)
			{
				case "queued":
					_mode = Mode.Queue;
					_queuedAt = Time.GetTicksMsec() / 1000.0;
					_message.Text = "";
					break;
				case "cancelled":
					if (_mode == Mode.Queue)
						_mode = Mode.Lobby;
					break;
				case "matched":
					_mode = Mode.Draft;
					_seat = message.Seat ?? RtaSeat.First;
					_rival = message.Opponent ?? "";
					_rivalAster = message.OpponentAster;
					_ranked = message.Ranked;
					_draft = null;
					_message.Text = "";
					break;
				case "draft":
					_draft = message;
					_deadline = Time.GetTicksMsec() / 1000.0 + message.Seconds;
					_clockMine = message.Waiting;
					break;
				case "end":
					_mode = Mode.Lobby;
					_draft = null;
					Refresh();
					ShowEnd(message);
					return;
				case "error":
					_message.Text = ErrorText(message.Error);
					if (_mode == Mode.Queue)
						_mode = Mode.Lobby;
					break;
			}

			Refresh();
		}

		/// <summary>O fim de uma partida que acabou na montagem (alguém desistiu): o mesmo painel do resultado da luta.</summary>
		private void ShowEnd(RtaMessage end)
		{
			var arena = _database.Arena;
			VeilResultPanel? panel = null;
			panel = new VeilResultPanel(end, _seat, arena.GradeOf(end.NewAster - end.Delta).Name, arena.GradeOf(end.NewAster).Name, () => panel!.QueueFree());
			AddChild(panel);
		}

		private static string ErrorText(string? error) => error switch
		{
			"invalid_pool" => T("veil.err_pool"),
			"no_save" or "save_unreadable" => T("veil.err_save"),
			"busy" => T("veil.err_busy"),
			"rate_limited" => T("veil.err_rate"),
			"name_required" => T("veil.err_name"),
			"already_connected" => T("veil.err_connected"),
			_ => T("veil.err_generic"),
		};

		private void Tick()
		{
			var now = Time.GetTicksMsec() / 1000.0;
			if (_mode == Mode.Queue && _queueLabel != null)
			{
				var seconds = (int)(now - _queuedAt);
				_queueLabel.Text = T("veil.searching", seconds);
				if (_ghostButton != null)
					_ghostButton.Visible = seconds >= GhostAfterSeconds;
			}
			else if (_mode == Mode.Draft && _clock != null && _draft is { Phase: not RtaPhase.Done })
			{
				var left = Math.Max(0, (int)Math.Ceiling(_deadline - now));
				_clock.Text = T(_clockMine ? "veil.your_turn" : "veil.rival_turn", left);
				_clock.AddThemeColorOverride("font_color", !_clockMine ? Palette.TextFaded : left <= 5 ? Palette.Negative : Palette.Gold);
			}
		}

		// Saguão ------------------------------------------------------------------------------------

		private void BuildLobby()
		{
			var veil = _player.Veil;
			var arena = _database.Arena;
			var grade = arena.GradeOf(veil.Aster);
			var index = arena.IndexOf(grade);
			var next = index + 1 < arena.Grades.Count ? arena.Grades[index + 1] : null;

			var center = new VBoxContainer { Name = "Lobby", SizeFlagsHorizontal = SizeFlags.ShrinkCenter, SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			center.CustomMinimumSize = new Vector2(560, 0);
			center.AddThemeConstantOverride("separation", Space.Medium);
			_content.AddChild(center);

			var status = new PanelContainer { Name = "StatusPanel" };
			var inside = new VBoxContainer { Name = "Status" };
			inside.AddThemeConstantOverride("separation", Space.Regular);
			status.AddChild(inside);
			center.AddChild(status);

			var rank = Layout.Row(Space.Wide, true).Named("Rank");
			var slot = new PanelContainer { Name = "RankSymbol", CustomMinimumSize = new Vector2(64, 64) };
			slot.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, Palette.Gold, 3, 44, 6));
			slot.AddChild(Layout.Medal(Art.Icon($"rank_{grade.Id}") ?? Art.Icon("target"), Palette.Gold, 50));
			rank.AddChild(slot);
			var title = new VBoxContainer { Name = "RankText", Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var name = new Label { Name = "RankName", Text = grade.Name, ThemeTypeVariation = GameTheme.Title };
			name.AddThemeFontSizeOverride("font_size", 26);
			title.AddChild(name);
			title.AddChild(new Label { Name = "Aster", Text = T("veil.aster", next != null ? $"{Texts.Number(veil.Aster)}/{Texts.Number(next.Points)}" : Texts.Number(veil.Aster)), ThemeTypeVariation = GameTheme.Heading });
			rank.AddChild(title);
			inside.AddChild(rank);
			inside.AddChild(Layout.Text(next != null ? T("veil.next_grade", Texts.Number(Math.Max(0, next.Points - veil.Aster)), next.Name) : T("arena.top_grade"), GameTheme.Faded).Named("NextGrade"));
			inside.AddChild(Layout.Labeled("fight", T("veil.record", veil.Wins, veil.Losses), T("veil.record_tip"), Palette.Gold).Named("Record"));
			inside.AddChild(Layout.Labeled("polar_star", Texts.Number(veil.Ecos), T("veil.ecos"), Palette.Spirit).Named("Ecos"));

			var eligible = Veil.Eligible(_player, _database);
			var can = _live && eligible >= RtaRules.MinPool;
			var queue = GameButton.Of(T("veil.queue"), () => QueueRequested?.Invoke(false), ButtonKind.Primary, "fight", 64).Named("Queue");
			queue.Disabled = !can;
			center.AddChild(queue);
			var ghost = GameButton.Of(T("veil.ghost"), () => QueueRequested?.Invoke(true), ButtonKind.Secondary, "target", 52).Named("Ghost");
			ghost.Disabled = eligible < RtaRules.MinPool;
			center.AddChild(ghost);
			center.AddChild(GameButton.Of(T("veil.shop"), () => ShopRequested?.Invoke(), ButtonKind.Secondary, "polar_star", 52).Named("Shop"));

			var note = !_live ? T(_trying ? "veil.connecting" : "veil.offline")
				: eligible < RtaRules.MinPool ? T("veil.need_pool", RtaRules.MinPool, eligible)
				: T("veil.ready");
			center.AddChild(Layout.Text(note, GameTheme.Faded, 540).Named("Note"));
		}

		// Fila --------------------------------------------------------------------------------------

		private void BuildQueue()
		{
			var center = new VBoxContainer { Name = "Queue", SizeFlagsHorizontal = SizeFlags.ShrinkCenter, SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			center.CustomMinimumSize = new Vector2(520, 0);
			center.AddThemeConstantOverride("separation", Space.Loose);
			_content.AddChild(center);

			_queueLabel = new Label { Name = "Searching", Text = T("veil.searching", 0), ThemeTypeVariation = GameTheme.Title, HorizontalAlignment = HorizontalAlignment.Center };
			center.AddChild(_queueLabel);
			center.AddChild(Layout.Text(T("veil.queue_text", Texts.Number(_player.Veil.Aster)), GameTheme.Faded, 500).Named("Text"));
			center.AddChild(GameButton.Of(T("veil.cancel"), () => CancelRequested?.Invoke(), ButtonKind.Secondary, "retreat", 56).Named("Cancel"));
			var ghost = GameButton.Of(T("veil.ghost"), () => QueueRequested?.Invoke(true), ButtonKind.Primary, "target", 56).Named("Ghost");
			ghost.Visible = false;
			_ghostButton = ghost;
			center.AddChild(ghost);
		}

		// Montagem do time ----------------------------------------------------------------------------

		private RtaSeat Rival => RtaDraft.Other(_seat);

		private IReadOnlyList<string> PicksOf(RtaSeat seat) => (seat == RtaSeat.First ? _draft?.PicksFirst : _draft?.PicksSecond) ?? new List<string>();

		private string? BannedOf(RtaSeat seat) => seat == RtaSeat.First ? _draft?.BannedFirst : _draft?.BannedSecond;

		private void BuildDraft()
		{
			var draft = _draft;
			var phase = draft?.Phase ?? RtaPhase.Picking;

			var top = Layout.Row(Space.Wide, true).Named("Top");
			top.AddChild(new Label { Name = "Phase", Text = PhaseText(draft), ThemeTypeVariation = GameTheme.Heading, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			_clock = new Label { Name = "Clock", HorizontalAlignment = HorizontalAlignment.Right };
			_clock.AddThemeFontSizeOverride("font_size", FontSize.Strong);
			top.AddChild(_clock);
			_content.AddChild(top);

			var columns = new HBoxContainer { Name = "Columns", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			columns.AddThemeConstantOverride("separation", Space.Wide);
			_content.AddChild(columns);

			columns.AddChild(Side(T("veil.you"), _player.Veil.Aster, _seat, true));

			var pool = new GridContainer { Name = "Pool", Columns = 7 };
			pool.AddThemeConstantOverride("h_separation", Space.Small);
			pool.AddThemeConstantOverride("v_separation", Space.Small);
			var scroll = Layout.Scroll(pool);
			scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			var taken = PicksOf(RtaSeat.First).Concat(PicksOf(RtaSeat.Second)).ToHashSet();
			foreach (var monster in Veil.Best(_player, _database))
			{
				var summon = _database.Summon(monster.SummonId);
				var card = new CreatureCard(summon, monster, 84) { Name = $"Pool_{summon.Id}" };
				var usable = phase == RtaPhase.Picking && draft is { Waiting: true } && !taken.Contains(summon.Id);
				card.Modulate = taken.Contains(summon.Id) ? new Color(1, 1, 1, 0.35f) : Colors.White;
				var id = summon.Id;
				card.Pressed += _ =>
				{
					if (usable)
						PickRequested?.Invoke(id);
				};
				pool.AddChild(card);
			}

			columns.AddChild(scroll);
			columns.AddChild(Side(_rival, _rivalAster, Rival, false));
		}

		private string PhaseText(RtaMessage? draft)
		{
			if (draft == null)
				return T("veil.matched", _rival);
			return draft.Phase switch
			{
				RtaPhase.Picking when draft.Waiting => T("veil.pick_you", draft.PicksLeft),
				RtaPhase.Picking => T("veil.pick_rival", _rival),
				RtaPhase.Banning when draft.Waiting => T("veil.ban_you"),
				RtaPhase.Banning => T("veil.ban_rival", _rival),
				RtaPhase.Leading when draft.Led => T("veil.lead_wait"),
				RtaPhase.Leading => T("veil.lead_you"),
				_ => T("veil.starting"),
			};
		}

		/// <summary>Uma coluna de cinco vagas: as escolhas de um lado, com a banida riscada, a Líder marcada e o toque de quem decide.</summary>
		private Control Side(string who, int aster, RtaSeat seat, bool mine)
		{
			var column = new VBoxContainer { Name = mine ? "Mine" : "Rival", CustomMinimumSize = new Vector2(190, 0) };
			column.AddThemeConstantOverride("separation", Space.Small);
			column.AddChild(new Label { Name = "Who", Text = who, ThemeTypeVariation = GameTheme.Heading, ClipText = true });
			column.AddChild(Layout.Text(T("veil.aster", Texts.Number(aster)), GameTheme.Faded).Named("Aster"));

			var draft = _draft;
			var picks = PicksOf(seat);
			var banned = BannedOf(seat);
			var phase = draft?.Phase ?? RtaPhase.Picking;
			for (var i = 0; i < RtaRules.DraftSize; i++)
			{
				var slot = new PanelContainer { Name = $"Slot{i + 1}", CustomMinimumSize = new Vector2(190, 52), MouseFilter = MouseFilterEnum.Stop };
				var id = i < picks.Count ? picks[i] : null;
				var out_ = id != null && id == banned;
				slot.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Inset, out_ ? Palette.Negative : id != null ? Palette.GoldDark : Palette.Text.Darkened(0.5f), 2, 14, 4));
				var row = Layout.Row(Space.Regular, true);
				if (id != null)
				{
					var summon = _database.Summon(id);
					row.AddChild(Layout.Medal(Art.Creature(summon.Image), Palette.Of(summon.Element), 36));
					var label = new Label { Name = "Name", Text = summon.Name + (out_ ? " · " + T("veil.banned") : ""), ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
					label.AddThemeFontSizeOverride("font_size", FontSize.Detail);
					if (out_)
						label.AddThemeColorOverride("font_color", Palette.Negative);
					row.AddChild(label);
					var chosen = id;
					var canBan = !mine && phase == RtaPhase.Banning && draft is { Waiting: true } && banned == null;
					var canLead = mine && phase == RtaPhase.Leading && draft is { Waiting: true, Led: false } && !out_;
					if (canBan || canLead)
						Press.On(slot, () =>
						{
							if (canBan)
								BanRequested?.Invoke(chosen);
							else
								LeadRequested?.Invoke(chosen);
						}, () =>
						{
							if (mine)
								MonsterSummary.Open(slot, summon, null);
						});
					else if (mine)
						Press.On(slot, null, () => MonsterSummary.Open(slot, summon, null));
				}
				else
				{
					row.AddChild(new Label { Name = "Empty", Text = "—", HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill });
				}

				slot.AddChild(row);
				column.AddChild(slot);
			}

			return column;
		}
	}
}
