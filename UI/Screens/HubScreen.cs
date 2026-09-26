using System;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O Santuário: a tela de abertura de toda sessão (GDD, seção 11). Nível da conta, coleta da
	/// ociosidade, Canalização Rápida, a equipe da Campanha e as portas para todas as outras telas.
	///
	/// Só mostra e avisa: cada botão vira um evento, e quem muda o <see cref="PlayerState"/> e salva é
	/// o GameRoot, que depois chama <see cref="Refresh"/>.
	/// </summary>
	public partial class HubScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly Label _account = new();
		private readonly ProgressBar _accountBar = new() { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
		private readonly Label _idleTime = new();
		private readonly Label _idleReward = new();
		private readonly Button _collect = new() { Text = T("santuario.coletar") };
		private readonly Button _quickChannel = new() { Text = T("santuario.canalizacao_rapida") };
		private readonly Label _hint = new() { HorizontalAlignment = HorizontalAlignment.Center };
		private readonly HBoxContainer _team = new();
		private readonly Button _campaign = Layout.IconButton("", Art.Icon("campaign"), 44);

		public HubScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
		}

		public event Action? CampaignRequested;
		public event Action? DungeonsRequested;
		public event Action? SummonRequested;
		public event Action? StorageRequested;
		public event Action? TeamsRequested;
		public event Action? RunesRequested;
		public event Action? CompendiumRequested;
		public event Action? GrimoireRequested;
		public event Action? ShopRequested;
		public event Action? CollectRequested;
		public event Action? QuickChannelRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var header = new HBoxContainer();
			header.AddChild(new Label { Text = T("santuario.titulo"), ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
			header.AddChild(_currencies);
			page.AddChild(header);
			page.AddChild(new Label { Text = T("santuario.subtitulo"), ThemeTypeVariation = GameTheme.Faded });

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

			var toNext = Account.ExperienceToNext(_player.AccountLevel);
			var maxed = _player.AccountLevel >= Account.MaxLevel;
			_account.Text = T(maxed ? "santuario.conta_maxima" : "santuario.conta", _player.AccountLevel, Mana.Max(_player));
			_accountBar.MaxValue = toNext;
			_accountBar.Value = maxed ? toNext : _player.AccountExperience;
			_accountBar.TooltipText = maxed
				? T("santuario.conta_maxima_dica", Account.MaxLevel)
				: T("santuario.conta_dica", _player.AccountExperience, toNext, Account.LevelUpGold, Account.MaxLevel, Mana.BaseMax + Mana.MaxFromLevels);

			var next = Math.Min(_player.HighestStage + 1, _database.Stages.Count);
			_campaign.Text = T("santuario.campanha", next, _database.Stage(next).Name);

			_hint.Text = _player.TotalPulls == 0
				? T("santuario.dica_primeira_invocacao")
				: _player.HighestStage == 0
					? T("santuario.dica_primeira_fase")
					: "";

			Layout.Clear(_team);
			foreach (var monster in Teams.Of(_player, Teams.Campaign).Select(_player.Monster).OfType<OwnedSummon>())
				_team.AddChild(new CreatureCard(_database.Summon(monster.SummonId), monster, width: 100));
		}

		private void RefreshIdle(DateTime now)
		{
			var preview = Idle.Preview(_player, now);
			var hours = TimeSpan.FromHours(Idle.PendingHours(_player, now));
			_idleTime.Text = T("santuario.canalizando", (int)hours.TotalHours, hours.Minutes.ToString("00"), Idle.CapHours);
			_idleReward.Text = T("santuario.recompensa", preview.Essence, preview.Gold, preview.Mana);
			_collect.Disabled = preview.IsEmpty;
			_quickChannel.Disabled = !Idle.CanQuickChannel(_player, now);
			_quickChannel.TooltipText = T("santuario.canalizacao_rapida_dica", Idle.QuickChannelHours);
		}

		private Control LeftColumn()
		{
			var column = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
			column.AddThemeConstantOverride("separation", 12);

			var account = new PanelContainer { ThemeTypeVariation = GameTheme.InsetPanel };
			var accountColumn = new VBoxContainer();
			_account.AddThemeFontOverride("font", GameTheme.Serif);
			_account.AddThemeFontSizeOverride("font_size", 18);
			accountColumn.AddChild(_account);
			_accountBar.AddThemeStyleboxOverride("fill", GameTheme.Box(Palette.Gold, Palette.Gold, 0, 3, 0));
			accountColumn.AddChild(_accountBar);
			account.AddChild(accountColumn);
			column.AddChild(account);

			var (idlePanel, idle) = Layout.Section(T("santuario.circulos"));
			idle.AddChild(Layout.Text(T("santuario.circulos_texto", Mana.PerHour), GameTheme.Faded, 420));
			idle.AddChild(_idleTime);
			_idleReward.AddThemeFontOverride("font", GameTheme.Serif);
			_idleReward.AddThemeFontSizeOverride("font_size", 18);
			idle.AddChild(_idleReward);
			var buttons = new HBoxContainer();
			buttons.AddChild(_collect);
			buttons.AddChild(_quickChannel);
			idle.AddChild(buttons);
			column.AddChild(idlePanel);

			column.AddChild(BigButton("santuario.monstros", "storage", () => StorageRequested?.Invoke()));
			column.AddChild(BigButton("santuario.equipes", "team", () => TeamsRequested?.Invoke()));
			column.AddChild(BigButton("santuario.runas", "rune", () => RunesRequested?.Invoke()));
			return column;
		}

		private Control RightColumn()
		{
			var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 12);

			var (teamPanel, team) = Layout.Section(T("santuario.equipe_campanha"));
			_team.AddThemeConstantOverride("separation", 8);
			team.AddChild(_team);
			column.AddChild(teamPanel);

			_campaign.CustomMinimumSize = new Vector2(0, 64);
			_campaign.Pressed += () => CampaignRequested?.Invoke();
			column.AddChild(_campaign);

			column.AddChild(BigButton("santuario.masmorras", "dungeon", () => DungeonsRequested?.Invoke()));

			var shopping = new HBoxContainer();
			shopping.AddThemeConstantOverride("separation", 12);
			shopping.AddChild(BigButton("santuario.invocacao", "summon", () => SummonRequested?.Invoke()));
			shopping.AddChild(BigButton("santuario.loja", "shop", () => ShopRequested?.Invoke()));
			column.AddChild(shopping);

			var books = new HBoxContainer();
			books.AddThemeConstantOverride("separation", 12);
			books.AddChild(BigButton("santuario.compendio", "compendium", () => CompendiumRequested?.Invoke()));
			books.AddChild(BigButton("santuario.grimorio", "grimoire", () => GrimoireRequested?.Invoke()));
			column.AddChild(books);
			return column;
		}

		/// <summary>Botão grande com ícone; o texto é a chave, a dica é a chave + "_dica".</summary>
		private static Button BigButton(string key, string icon, Action onPressed)
		{
			var button = Layout.IconButton(T(key), Art.Icon(icon), 36);
			button.TooltipText = T($"{key}_dica");
			button.CustomMinimumSize = new Vector2(0, 56);
			button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			button.Pressed += onPressed;
			return button;
		}
	}
}
