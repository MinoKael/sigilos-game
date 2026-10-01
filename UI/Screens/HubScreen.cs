using System;
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
	/// O Santuário: a tela de abertura de toda sessão (GDD, seção 11), pensada para o celular.
	///
	/// - No alto, a conta (retrato, nível e a barra de experiência; tocar explica) e os recursos.
	/// - No meio, à esquerda, a Canalização: a constelação (o sigilo do centro com o anel do tempo
	///   acumulado), o que se juntou enquanto o jogador estava fora, o tempo e o botão Coletar. À direita,
	///   os dois caminhos principais em botões grandes: Batalha e Invocar.
	/// - Embaixo, a barra com tudo o mais, cada botão com o nome escrito: Monstros, Runas, Equipes, Loja,
	///   Grimório, Compêndio e Ajustes.
	///
	/// Quem ainda não invocou vê Invocar pulsar; quem não venceu a primeira fase, Batalha.
	/// Só mostra e avisa: quem muda o <see cref="PlayerState"/> e salva é o GameRoot.
	/// </summary>
	public partial class HubScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly Button _account = new() { Name = "Account", FocusMode = FocusModeEnum.None, Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand };
		private readonly Constellation _constellation = new() { Name = "Constellation", CustomMinimumSize = new Vector2(330, 0) };
		private readonly SigilButton _core = new(Art.Icon("collect"), 128) { Name = "Core" };
		private readonly Label _time = new() { Name = "Time", ThemeTypeVariation = GameTheme.Number };
		private readonly HFlowContainer _pending = Layout.Flow(8).Named("Pending");
		private readonly GameButton _collect;
		private TileButton? _battle;
		private TileButton? _summon;

		public HubScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
			_collect = GameButton.Of(T("hub.collect"), () => CollectRequested?.Invoke(), ButtonKind.Primary, "collect", 64).Named("Collect");
		}

		public event Action<Destination>? Requested;
		public event Action? ConfigRequested;
		public event Action? CollectRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);

			var top = Layout.Row(12).Named("Top");
			top.AddChild(_account);
			top.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });
			_currencies.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			top.AddChild(_currencies);
			page.AddChild(top);
			_account.Pressed += ExplainAccount;

			var middle = Layout.Row(20).Named("Middle");
			middle.SizeFlagsVertical = SizeFlags.ExpandFill;
			middle.AddChild(Channel());
			middle.AddChild(Paths());
			page.AddChild(middle);
			page.AddChild(NavBar());

			// A ociosidade anda com a tela aberta: o anel e as recompensas acompanham a cada segundo.
			var timer = new Timer { Name = "IdleTimer", WaitTime = 1, Autostart = true };
			timer.Timeout += () => RefreshIdle(DateTime.Now);
			AddChild(timer);

			Refresh(DateTime.Now);
		}

		public void Refresh(DateTime now)
		{
			_currencies.Refresh(_player);
			RefreshAccount();
			RefreshIdle(now);
			if (_summon != null)
				_summon.Highlight = _player.TotalPulls == 0;
			if (_battle != null)
				_battle.Highlight = _player.TotalPulls > 0 && _player.HighestStage == 0;
		}

		/// <summary>O retrato da conta, o nível e a barra de experiência.</summary>
		private void RefreshAccount()
		{
			Layout.Clear(_account);
			var row = Layout.Row(12).Named("Row");
			row.MouseFilter = MouseFilterEnum.Ignore;
			var frame = new PanelContainer { Name = "Portrait", CustomMinimumSize = new Vector2(68, 68), MouseFilter = MouseFilterEnum.Ignore };
			frame.AddThemeStyleboxOverride("panel", GameTheme.Box(Palette.Panel, Palette.Gold, 2, 34, 4));
			frame.AddChild(Doodle.Masked(Art.Icon("avatar"), Palette.Gold, MaskShape.Circle, boil: false));
			row.AddChild(frame);

			var column = new VBoxContainer { Name = "Info", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			column.AddThemeConstantOverride("separation", 4);
			var maxed = _player.AccountLevel >= Account.MaxLevel;
			var toNext = Account.ExperienceToNext(_player.AccountLevel);
			column.AddChild(new Label { Name = "Level", Text = T("hub.account_level", _player.AccountLevel), ThemeTypeVariation = GameTheme.Heading, MouseFilter = MouseFilterEnum.Ignore });
			var bar = Layout.Energy(Palette.Arcane, 10).Named("Experience");
			bar.CustomMinimumSize = new Vector2(200, 10);
			bar.MaxValue = 1;
			bar.Step = 0;
			bar.Value = maxed ? 1 : _player.AccountExperience / (double)Math.Max(1, toNext);
			bar.MouseFilter = MouseFilterEnum.Ignore;
			column.AddChild(bar);
			column.AddChild(new Label { Name = "Progress", Text = maxed ? T("hub.account_max_short") : T("hub.account_progress", _player.AccountExperience, toNext), ThemeTypeVariation = GameTheme.Faded, MouseFilter = MouseFilterEnum.Ignore });
			row.AddChild(column);
			_account.AddChild(row);
			_account.CustomMinimumSize = row.GetCombinedMinimumSize();
		}

		private void ExplainAccount()
		{
			var maxed = _player.AccountLevel >= Account.MaxLevel;
			var text = maxed
				? T("hub.account_max_tip", _player.AccountLevel)
				: T("hub.account_tip", _player.AccountExperience, Account.ExperienceToNext(_player.AccountLevel), Account.LevelUpGold, Account.MaxLevel, Mana.BaseMax + Mana.MaxFromLevels);
			Dialog.Info(_account, T("hub.account_level", _player.AccountLevel), text);
		}

		/// <summary>
		/// A Canalização: a constelação (o sigilo do centro, o anel do tempo acumulado e as estrelas), o que
		/// já se juntou e o botão Coletar. Tocar no sigilo do centro também coleta, quando há o que coletar.
		/// </summary>
		private Control Channel()
		{
			var panel = new PanelContainer { Name = "Channel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var row = Layout.Row(20).Named("Row");
			panel.AddChild(row);

			_core.Pressed += () =>
			{
				if (!Idle.Preview(_player, DateTime.Now).IsEmpty)
					CollectRequested?.Invoke();
			};
			_constellation.SetCenter(_core);
			row.AddChild(_constellation);

			var column = new VBoxContainer { Name = "Text", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
			column.AddThemeConstantOverride("separation", 12);
			column.AddChild(new Label { Name = "Title", Text = T("hub.channel"), ThemeTypeVariation = GameTheme.Title });
			column.AddChild(Layout.Text(T("hub.channel_text", Idle.CapHours), GameTheme.Faded).Named("Explain"));
			_time.AddThemeFontSizeOverride("font_size", 22);
			column.AddChild(_time);
			column.AddChild(_pending);
			var actions = Layout.Row(0).Named("Actions");
			actions.AddChild(_collect.Wide(240));
			column.AddChild(actions);
			row.AddChild(column);
			return panel;
		}

		/// <summary>Os dois caminhos principais, em botões grandes: Batalha (Campanha e Masmorras) e Invocar.</summary>
		private Control Paths()
		{
			var column = new VBoxContainer { Name = "Paths", CustomMinimumSize = new Vector2(380, 0) };
			column.AddThemeConstantOverride("separation", 16);
			column.Alignment = BoxContainer.AlignmentMode.Center;

			var next = Math.Min(_player.HighestStage + 1, _database.Stages.Count);
			_battle = new TileButton(T("hub.battle"), T("hub.battle_detail", next, _database.Stages.Count), Art.Icon("fight"), new Vector2(380, 170), ButtonKind.Secondary, horizontal: true) { Name = "Battle" };
			_battle.Pressed += () => Requested?.Invoke(Destination.Map);
			column.AddChild(_battle);

			_summon = new TileButton(T("destination.Summon"), T("hub.summon_detail", _player.Scrolls), Art.Icon("summon"), new Vector2(380, 130), ButtonKind.Secondary, horizontal: true) { Name = "Summon" };
			_summon.Pressed += () => Requested?.Invoke(Destination.Summon);
			column.AddChild(_summon);
			return column;
		}

		/// <summary>A barra de baixo: um botão escrito para cada lugar do jogo.</summary>
		private Control NavBar()
		{
			var panel = new PanelContainer { Name = "Nav" };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.GoldDark, 8));
			var row = Layout.Row(8, true).Named("Row");
			panel.AddChild(row);
			foreach (var destination in new[] { Destination.Monsters, Destination.Runes, Destination.Teams, Destination.Shop, Destination.Grimoire, Destination.Compendium })
			{
				var button = TileButton.Nav(Destinations.Name(destination), Destinations.Icon(destination)).Named(destination.ToString());
				button.Pressed += () => Requested?.Invoke(destination);
				row.AddChild(button);
			}

			var config = TileButton.Nav(T("destination.Config"), "config").Named("Config");
			config.Pressed += () => ConfigRequested?.Invoke();
			row.AddChild(config);
			return panel;
		}

		private void RefreshIdle(DateTime now)
		{
			var preview = Idle.Preview(_player, now);
			var pendingHours = Idle.PendingHours(_player, now);
			var hours = TimeSpan.FromHours(pendingHours);
			_constellation.Progress = (float)(pendingHours / Idle.CapHours);
			_core.Highlight = !preview.IsEmpty;
			_time.Text = T("hub.channeling", (int)hours.TotalHours, hours.Minutes.ToString("00"), Idle.CapHours);

			Layout.Clear(_pending);
			_pending.AddChild(Layout.Labeled("essence", Texts.Short(preview.Essence), T("currency.essence")));
			_pending.AddChild(Layout.Labeled("gold", Texts.Short(preview.Gold), T("currency.gold")));
			_pending.AddChild(Layout.Labeled("mana", Texts.Short(preview.Mana), T("currency.mana")));
			_collect.Disabled = preview.IsEmpty;
		}
	}
}
