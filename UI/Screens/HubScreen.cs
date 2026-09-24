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
	/// Canalização Rápida, o time e as portas para Campanha, Invocação, Monstros e Compêndio.
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
		private readonly Label _hint = new() { HorizontalAlignment = HorizontalAlignment.Center };
		private readonly HBoxContainer _team = new();
		private readonly Button _campaign = Layout.IconButton("", Art.Icon("campaign"), 44);

		public HubScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
		}

		public event Action? CampaignRequested;
		public event Action? SummonRequested;
		public event Action? StorageRequested;
		public event Action? CompendiumRequested;
		public event Action? CollectRequested;
		public event Action? QuickChannelRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = "Sigilos", ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			header.AddChild(_currencies);
			page.AddChild(header);
			page.AddChild(new Label { Text = "Santuário do Conjurador · Planície dos Menires", ThemeTypeVariation = GameTheme.Faded });

			var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
			body.AddThemeConstantOverride("separation", 20);
			page.AddChild(body);
			body.AddChild(LeftColumn());
			body.AddChild(RightColumn());

			page.AddChild(_hint);

			_collect.Pressed += () => CollectRequested?.Invoke();
			_quickChannel.Pressed += () => QuickChannelRequested?.Invoke();

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

			var next = Math.Min(_player.HighestStage + 1, _database.Stages.Count);
			_campaign.Text = $"Campanha · Fase {next}: {_database.Stage(next).Name}";

			_hint.Text = _player.TotalPulls == 0
				? "Sua primeira invocação é uma 5★ garantida. Abra a Invocação Ritual."
				: _player.HighestStage == 0
					? "Equipe as runas iniciais em Monstros e vença a primeira fase da Campanha."
					: "";

			Layout.Clear(_team);
			foreach (var id in _player.Team.Where(id => _database.HasSummon(id) && _player.Owns(id)))
				_team.AddChild(new CreatureCard(_database.Summon(id), _player.Summon(id), width: 118));
		}

		private void RefreshIdle(DateTime now)
		{
			var preview = Idle.Preview(_player, now);
			var hours = TimeSpan.FromHours(Idle.PendingHours(_player, now));
			_idleTime.Text = $"Canalizando há {(int)hours.TotalHours}h{hours.Minutes:00} (máx. {Idle.CapHours:0} h)";
			_idleReward.Text = $"+{preview.Essence} Essência   +{preview.Dust} Pó   +{Texts.Scrolls(preview.Scrolls)}";
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
				Text = "Com o jogo fechado, seus círculos continuam canalizando Essência, Pó de Sigilo e Pergaminhos.",
				ThemeTypeVariation = GameTheme.Faded,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
			idle.AddChild(_idleTime);
			_idleReward.AddThemeFontOverride("font", GameTheme.Serif);
			_idleReward.AddThemeFontSizeOverride("font_size", 19);
			idle.AddChild(_idleReward);
			var buttons = new HBoxContainer();
			buttons.AddChild(_collect);
			buttons.AddChild(_quickChannel);
			idle.AddChild(buttons);
			column.AddChild(idlePanel);

			column.AddChild(BigButton("Monstros", "storage", "Coleção, níveis, Despertar e runas.", () => StorageRequested?.Invoke()));
			column.AddChild(BigButton("Compêndio", "compendium", "O que são os Glifos, elementos, efeitos, runas e Éter.", () => CompendiumRequested?.Invoke()));
			return column;
		}

		private Control RightColumn()
		{
			var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 14);

			var (teamPanel, team) = Layout.Section("Time · a primeira é a Líder");
			_team.AddThemeConstantOverride("separation", 10);
			team.AddChild(_team);
			column.AddChild(teamPanel);

			_campaign.CustomMinimumSize = new Vector2(0, 72);
			_campaign.Pressed += () => CampaignRequested?.Invoke();
			column.AddChild(_campaign);

			column.AddChild(BigButton("Invocação Ritual", "summon", "Gaste Pergaminhos para invocar criaturas.", () => SummonRequested?.Invoke()));
			return column;
		}

		private static Button BigButton(string text, string icon, string tooltip, Action onPressed)
		{
			var button = Layout.IconButton(text, Art.Icon(icon), 40);
			button.TooltipText = tooltip;
			button.CustomMinimumSize = new Vector2(0, 64);
			button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			button.Pressed += onPressed;
			return button;
		}
	}
}
