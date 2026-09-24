using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O Santuário: a tela de abertura de toda sessão (GDD, seção 11). Coleta da ociosidade,
	/// Canalização Rápida, nível compartilhado e as portas para Campanha, Invocação e Time.
	///
	/// Só mostra e avisa: cada botão vira um evento, e quem muda o <see cref="PlayerState"/> e salva é
	/// o GameRoot, que depois chama <see cref="Refresh"/>.
	/// </summary>
	public partial class HubScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly Label _idleTime = new();
		private readonly Label _idleReward = new();
		private readonly Button _collect = new() { Text = "Coletar" };
		private readonly Button _quickChannel = new() { Text = "Canalização Rápida" };
		private readonly Label _level = new();
		private readonly Button _raise = new();
		private readonly Label _hint = new();
		private readonly HBoxContainer _team = new();
		private readonly Button _campaign = new();

		public HubScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
		}

		public event Action? CampaignRequested;
		public event Action? SummonRequested;
		public event Action? PrepareRequested;
		public event Action? CollectRequested;
		public event Action? QuickChannelRequested;
		public event Action? RaiseLevelRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());

			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = "Sigilos", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			header.AddChild(_currencies);
			page.AddChild(header);
			page.AddChild(new Label { Text = "Santuário do Erudito · Planície dos Menires", ThemeTypeVariation = GameTheme.OnStone });

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 20);
			page.AddChild(body);

			body.AddChild(LeftColumn());
			body.AddChild(RightColumn());

			_hint.ThemeTypeVariation = GameTheme.OnStone;
			_hint.HorizontalAlignment = HorizontalAlignment.Center;
			page.AddChild(_hint);

			_collect.Pressed += () => CollectRequested?.Invoke();
			_quickChannel.Pressed += () => QuickChannelRequested?.Invoke();
			_raise.Pressed += () => RaiseLevelRequested?.Invoke();

			// A ociosidade anda com a tela aberta: o texto acompanha a cada segundo.
			var timer = new Timer { WaitTime = 1, Autostart = true };
			timer.Timeout += () => RefreshIdle(DateTime.Now);
			AddChild(timer);

			Refresh(DateTime.Now);
		}

		public void Refresh(DateTime now)
		{
			_currencies.Refresh(_player);
			RefreshIdle(now);

			var cost = SharedLevel.CostToRaise(_player.Level);
			_level.Text = $"Nível {_player.Level} de {SharedLevel.RegionOneCap}";
			_raise.Text = _player.Level >= SharedLevel.RegionOneCap ? "Teto da região" : $"Elevar ({cost} Essência)";
			_raise.Disabled = !SharedLevel.CanRaise(_player.Level, _player.Essence);

			var next = Math.Min(_player.HighestStage + 1, _database.Stages.Count);
			_campaign.Text = $"Campanha · Fase {next}: {_database.Stage(next).Name}";

			_hint.Text = _player.TotalPulls == 0
				? "Sua primeira invocação é uma 5★ garantida. Abra a Invocação Ritual."
				: _player.HighestStage == 0 ? "Monte o time e vença a primeira fase da Campanha." : "";

			RefreshTeam();
		}

		private void RefreshIdle(DateTime now)
		{
			var hours = Idle.PendingHours(_player, now);
			var preview = Idle.Preview(_player, now);
			_idleTime.Text = $"Canalizando há {FormatHours(hours)} (máx. {Idle.CapHours:0} h)";
			_idleReward.Text = $"+{preview.Essence} Essência   +{Texts.Scrolls(preview.Scrolls)}";
			_collect.Disabled = preview.IsEmpty;
			_quickChannel.Disabled = !Idle.CanQuickChannel(_player, now);
			_quickChannel.TooltipText = $"Uma vez por dia: {Idle.QuickChannelHours:0} horas de ociosidade na hora.";
		}

		private Control LeftColumn()
		{
			var column = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
			column.AddThemeConstantOverride("separation", 16);

			var (idlePanel, idle) = Layout.Section("Círculos de canalização");
			idle.AddChild(new Label
			{
				Text = "Com o jogo fechado, seus círculos continuam canalizando.",
				ThemeTypeVariation = GameTheme.Faded,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
			idle.AddChild(_idleTime);
			_idleReward.AddThemeFontOverride("font", GameTheme.Serif);
			_idleReward.AddThemeFontSizeOverride("font_size", 20);
			idle.AddChild(_idleReward);
			var buttons = new HBoxContainer();
			buttons.AddChild(_collect);
			buttons.AddChild(_quickChannel);
			idle.AddChild(buttons);
			column.AddChild(idlePanel);

			var (levelPanel, level) = Layout.Section("Nível compartilhado");
			level.AddChild(new Label
			{
				Text = "Todas as invocações lutam neste nível: testar qualquer time custa zero.",
				ThemeTypeVariation = GameTheme.Faded,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
			level.AddChild(_level);
			level.AddChild(_raise);
			column.AddChild(levelPanel);
			return column;
		}

		private Control RightColumn()
		{
			var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 14);

			var (teamPanel, team) = Layout.Section("Time");
			_team.AddThemeConstantOverride("separation", 10);
			team.AddChild(_team);
			column.AddChild(teamPanel);

			_campaign.CustomMinimumSize = new Vector2(0, 64);
			_campaign.Icon = Art.Icon("campaign");
			_campaign.ExpandIcon = true;
			_campaign.AddThemeConstantOverride("icon_max_width", 40);
			_campaign.Pressed += () => CampaignRequested?.Invoke();
			column.AddChild(_campaign);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 14);
			row.AddChild(BigButton("Invocação Ritual", () => SummonRequested?.Invoke()));
			row.AddChild(BigButton("Time e Grimório", () => PrepareRequested?.Invoke()));
			column.AddChild(row);
			return column;
		}

		private void RefreshTeam()
		{
			Layout.Clear(_team);

			foreach (var id in _player.Team.Where(_database.HasSummon))
				_team.AddChild(new CreatureCard(_database.Summon(id), _player.Echoes(id), width: 118));
		}

		private static Button BigButton(string text, Action onPressed)
		{
			var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 64), SizeFlagsHorizontal = SizeFlags.ExpandFill };
			button.Pressed += onPressed;
			return button;
		}

		private static string FormatHours(double hours)
		{
			var span = TimeSpan.FromHours(hours);
			return $"{(int)span.TotalHours}h{span.Minutes:00}";
		}
	}
}
