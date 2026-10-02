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
	/// - No alto, a conta (retrato, o nome dela e o nível, a barra de experiência; tocar explica) e os
	///   recursos. Sem conta (ou numa conta sem nome), no lugar do nome vai "Conta".
	/// - No meio, à esquerda, a Canalização: a constelação ocupa o painel (o sigilo do centro com o anel
	///   do tempo acumulado e os orbes em volta) e, embaixo do centro, o tempo, o que se juntou enquanto o
	///   jogador estava fora e o botão Coletar. À direita, os dois caminhos principais em botões grandes:
	///   Batalha e Invocar.
	/// - Embaixo, a barra com tudo o mais, cada botão com o nome escrito: Monstros, Runas, Equipes, Loja,
	///   Grimório, Compêndio e Ajustes; no canto esquerdo dela, a versão do jogo.
	///
	/// Quem ainda não invocou vê Invocar pulsar; quem não venceu a primeira fase, Batalha. A barra de baixo
	/// e a Canalização aparecem aos poucos, conforme a Campanha abre cada parte (<see cref="Features"/>):
	/// o botão que acabou de abrir pulsa até a próxima fase vencida.
	/// Só mostra e avisa: quem muda o <see cref="PlayerState"/> e salva é o GameRoot.
	/// </summary>
	public partial class HubScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		/// <summary>O nome da conta, no lugar de "Conta" ao lado do nível; nulo sem conta (ou sem nome).</summary>
		private readonly string? _accountName;

		/// <summary>"Sem conexão", ao lado da conta: jogando a conta sem falar com o servidor. Tocar explica.</summary>
		private readonly Button _offline = new() { Name = "Offline", Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, SizeFlagsVertical = SizeFlags.ShrinkCenter };

		private readonly CurrencyBar _currencies = new();
		private readonly Button _account = new() { Name = "Account", FocusMode = FocusModeEnum.None, Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand };
		private readonly Constellation _constellation = new() { Name = "Constellation" };
		private readonly SigilButton _core = new(Art.Icon("collect"), 128) { Name = "Core" };
		private readonly Label _time = new() { Name = "Time", ThemeTypeVariation = GameTheme.Number };
		private readonly HBoxContainer _pending = Layout.Row(8, true).Named("Pending");
		private readonly GameButton _collect;
		private TileButton? _battle;
		private TileButton? _summon;
		private bool _channelOpen;

		/// <param name="offline">Jogando a conta sem conexão com o servidor.</param>
		public HubScreen(GameDatabase database, PlayerState player, string? accountName = null, bool offline = false)
		{
			_database = database;
			_player = player;
			_accountName = accountName;
			_offline.Visible = offline;
			_collect = GameButton.Of(T("hub.collect"), () => CollectRequested?.Invoke(), ButtonKind.Primary, "collect", 50).Named("Collect");
		}

		public event Action<Destination>? Requested;
		public event Action? ConfigRequested;
		public event Action? CollectRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			// O anel do fundo gira em volta do sigilo da Canalização.
			AddChild(Layout.Background(_core));
			var page = Layout.Page(this);

			var top = Layout.Row(12).Named("Top");
			top.AddChild(_account);
			top.AddChild(_offline);
			top.AddChild(new Control { Name = "Spacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });
			_currencies.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			top.AddChild(_currencies);
			page.AddChild(top);
			_account.Pressed += ExplainAccount;
			_offline.Text = T("hub.offline");
			_offline.AddThemeColorOverride("font_color", Palette.Negative);
			_offline.AddThemeColorOverride("font_hover_color", Palette.Negative.Lightened(0.2f));
			_offline.Pressed += () => Dialog.Info(_offline, T("hub.offline"), T("hub.offline_text"));

			_channelOpen = Features.IsOpen(_player, _database, Feature.Channel);
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

		/// <summary>A conexão com o servidor caiu ou voltou: mostra ou esconde o "Sem conexão".</summary>
		public void SetOffline(bool offline) => _offline.Visible = offline;

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
			column.AddChild(new Label { Name = "Level", Text = AccountTitle(), ThemeTypeVariation = GameTheme.Heading, MouseFilter = MouseFilterEnum.Ignore });
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
			Dialog.Info(_account, AccountTitle(), text);
		}

		/// <summary>"Fulano · Nível 22" com conta que tem nome; "Conta · Nível 22" sem.</summary>
		private string AccountTitle() =>
			_accountName != null ? T("hub.account_named", _accountName, _player.AccountLevel) : T("hub.account_level", _player.AccountLevel);

		/// <summary>
		/// A Canalização: a constelação ocupa o painel inteiro (o sigilo do centro com o anel do tempo
		/// acumulado e os orbes em volta) e, logo embaixo do centro, numa placa escura, o tempo, o que já se
		/// juntou e o botão Coletar. Tocar no sigilo do centro também coleta, quando há o que coletar; tocar
		/// no tempo explica a Canalização.
		/// </summary>
		private Control Channel()
		{
			var panel = new PanelContainer { Name = "Channel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			// Couro translúcido: o anel do fundo, centrado no sigilo, aparece atrás da constelação.
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(new Color(Palette.Panel, 0.6f), Palette.GoldDark));
			panel.AddChild(_constellation);

			_core.Pressed += () =>
			{
				if (!Idle.Preview(_player, DateTime.Now).IsEmpty)
					CollectRequested?.Invoke();
			};
			_constellation.SetCenter(_core);

			var plate = new PanelContainer { Name = "Plate" };
			var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0f), AntiAliasing = true };
			box.SetCornerRadiusAll(14);
			box.ContentMarginLeft = box.ContentMarginRight = 16;
			box.ContentMarginTop = box.ContentMarginBottom = 8;
			plate.AddThemeStyleboxOverride("panel", box);
			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", 6);
			plate.AddChild(column);

			// O tempo é um botão sem moldura: parece texto, e o toque abre a explicação colada nele.
			var time = new Button { Name = "Time", Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
			time.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
			time.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
			time.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
			_time.HorizontalAlignment = HorizontalAlignment.Center;
			_time.MouseFilter = MouseFilterEnum.Ignore;
			_time.AddThemeFontSizeOverride("font_size", 22);
			_time.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			time.AddChild(_time);
			_time.MinimumSizeChanged += () => time.CustomMinimumSize = _time.GetCombinedMinimumSize();
			time.Pressed += () => Dialog.Info(time, T("hub.channel"), T("hub.channel_text", Idle.CapHours));
			column.AddChild(time);

			column.AddChild(_pending);
			var actions = Layout.Row(0, true).Named("Actions");
			column.AddChild(actions);
			if (!_channelOpen)
			{
				// Fechada: a constelação apagada e, no lugar do tempo, a fase que abre a Canalização.
				time.Visible = _pending.Visible = false;
				_core.Disabled = true;
				panel.Modulate = new Color(1, 1, 1, 0.55f);
				var closed = Layout.Text(T("hub.channel_opens", Features.StageOf(_database, Feature.Channel)), GameTheme.Faded, 300).Named("Closed");
				closed.HorizontalAlignment = HorizontalAlignment.Center;
				column.AddChild(closed);
			}

			_constellation.Attach(plate);
			return panel;
		}

		/// <summary>Os dois caminhos principais, em botões grandes: Batalha (Campanha e Masmorras) e Invocar.</summary>
		private Control Paths()
		{
			var column = new VBoxContainer { Name = "Paths", CustomMinimumSize = new Vector2(380, 0) };
			column.AddThemeConstantOverride("separation", 16);
			column.Alignment = BoxContainer.AlignmentMode.Center;

			var next = Math.Min(_player.HighestStage + 1, _database.Stages.Count);
			// Antes de abrirem, as Masmorras não aparecem nem no nome do caminho.
			var detail = Features.IsOpen(_player, _database, Feature.Dungeons) ? T("hub.battle_detail", next, _database.Stages.Count) : T("hub.battle_detail_campaign", next, _database.Stages.Count);
			_battle = new TileButton(T("hub.battle"), detail, Art.Icon("fight"), new Vector2(380, 170), ButtonKind.Secondary, horizontal: true) { Name = "Battle" };
			_battle.Pressed += () => Requested?.Invoke(Destination.Map);
			column.AddChild(_battle);

			_summon = new TileButton(T("destination.Summon"), T("hub.summon_detail", _player.Scrolls), Art.Icon("summon"), new Vector2(380, 130), ButtonKind.Secondary, horizontal: true) { Name = "Summon" };
			_summon.Pressed += () => Requested?.Invoke(Destination.Summon);
			column.AddChild(_summon);
			return column;
		}

		/// <summary>
		/// A barra de baixo: um botão escrito para cada lugar do jogo, no meio. No canto de baixo à esquerda,
		/// a versão do jogo (application/config/version), para saber de que release é o que está rodando.
		/// </summary>
		private Control NavBar()
		{
			var panel = new PanelContainer { Name = "Nav" };
			panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.GoldDark, 8));
			var row = Layout.Row(8).Named("Row");
			panel.AddChild(row);

			// A versão e o vão da direita dividem a sobra igual: os botões ficam no centro.
			var version = ProjectSettings.GetSetting("application/config/version").AsString();
			row.AddChild(new Label
			{
				Name = "Version",
				Text = OS.IsDebugBuild() ? T("hub.version_debug", version) : T("hub.version", version),
				ThemeTypeVariation = GameTheme.Faded,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ShrinkEnd,
			});
			var places = new[]
			{
				(Destination.Monsters, Feature.Monsters), (Destination.Runes, Feature.Runes), (Destination.Teams, Feature.Teams),
				(Destination.Shop, Feature.Shop), (Destination.Grimoire, Feature.Grimoire), (Destination.Compendium, Feature.Compendium),
			};
			foreach (var (destination, feature) in places)
			{
				if (!Features.IsOpen(_player, _database, feature))
					continue;
				var button = TileButton.Nav(Destinations.Name(destination), Destinations.Icon(destination)).Named(destination.ToString());
				button.Highlight = Features.IsNew(_player, _database, feature);
				button.Pressed += () => Requested?.Invoke(destination);
				row.AddChild(button);
			}

			var config = TileButton.Nav(T("destination.Config"), "config").Named("Config");
			config.Pressed += () => ConfigRequested?.Invoke();
			row.AddChild(config);
			row.AddChild(new Control { Name = "Balance", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			return panel;
		}

		private void RefreshIdle(DateTime now)
		{
			if (!_channelOpen)
				return;

			var preview = Idle.Preview(_player, now);
			var pendingHours = Idle.PendingHours(_player, now);
			var hours = TimeSpan.FromHours(pendingHours);
			_constellation.Progress = (float)(pendingHours / Idle.CapHours);
			_core.Highlight = !preview.IsEmpty;
			_time.Text = T("hub.channeling", (int)hours.TotalHours, hours.Minutes.ToString("00"), Idle.CapHours);

			Layout.Clear(_pending);
			_pending.AddChild(Layout.Labeled("essence", Texts.Short(preview.Essence), T("currency.essence"), labelMinimumSize: new Vector2(40,22)));
			_pending.AddChild(Layout.Labeled("gold", Texts.Short(preview.Gold), T("currency.gold"), labelMinimumSize: new Vector2(23, 22)));
			_pending.AddChild(Layout.Labeled("mana", Texts.Short(preview.Mana), T("currency.mana"), labelMinimumSize: new Vector2(26, 22)));
			_collect.Disabled = preview.IsEmpty;
		}
	}
}
