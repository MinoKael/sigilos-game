using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Rta;
using Sigilos.Core.Social;
using Sigilos.Core.Summoning;
using Sigilos.GameEntry.Account;
using Sigilos.GameEntry.Update;
using Sigilos.UI;
using Sigilos.UI.Audio;
using Sigilos.UI.Components;
using Sigilos.UI.Screens;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Nó raiz (Scenes/GameRoot.tscn) e único lugar que junta as peças: carrega os dados, os textos e o
	/// save, decide qual tela está no ar e aplica as regras do Core quando uma tela pede.
	///
	/// <b>As telas não o conhecem.</b> Cada uma recebe o que mostra e avisa por evento o que o jogador
	/// escolheu; quem muda o <see cref="PlayerState"/> e salva é esta classe.
	///
	/// A navegação: o Santuário leva à escolha de batalha (Campanha, Masmorras), à Invocação e, pela
	/// barra de baixo, a Monstros, Runas, Loja, Grimório, Compêndio e Ajustes; cada tela volta
	/// para onde veio (seta, Esc ou o Voltar do celular). A Batalha automática roda fora das telas
	/// (<see cref="AutoBattleRunner"/>), com o aviso flutuante no alto de todas.
	/// A música acompanha a tela (<see cref="MusicPlayer"/>): a Batalha na luta, o Plano Celestial no resto.
	/// Os efeitos sonoros tocam pelo <see cref="Sfx"/> (docs/SONS.md): cada ação daqui soa o seu.
	///
	/// Antes do Santuário vem a conta (<see cref="AccountSession"/>, docs/SAVE_NUVEM.md): a tela de login
	/// (Entrar, Criar conta) e a pergunta "desconectar o outro aparelho?". Quem tem conta lembrada entra
	/// sozinho; outro aparelho entrando derruba este de volta para o login. A conta é do servidor: tudo que
	/// gasta, rende, sorteia ou organiza passa pelo <see cref="GameApi"/>, e esta classe só mostra o que voltou.
	///
	/// O executável do Windows procura uma versão nova ao abrir e, se o jogador aceitar, se troca por ela
	/// (<see cref="Updater"/>, docs/ATUALIZACOES.md).
	///
	/// Argumentos de desenvolvimento (depois de <c>--</c>): <c>--save=nome</c> usa outros arquivos de
	/// save e de conta (outro "aparelho"); <c>--language=nome</c> usa Data/texts/nome.json (padrão: o do
	/// save, ou pt-BR); <c>--server=url</c> usa outro servidor de contas; <c>--updates=url</c> procura
	/// versões novas em outra pasta (só no executável exportado);
	/// <c>--screen=map|campaign|dungeons|summon|shop|monsters|prep|runes|compendium|grimoire|battle</c> abre essa tela direto, no save sem conta
	/// (<c>prep</c>: a preparação da próxima fase da Campanha). Se já existe o arquivo <c>user://nome.json</c>
	/// do <c>--save=nome</c> (um save de teste feito à mão), o jogo abre direto nele, sem login e sem servidor.
	/// </summary>
	public partial class GameRoot : Node
	{
		private const string DefaultSlot = "sigilos";

		/// <summary>De quanto em quanto tempo a coleta de lixo completa passa (<see cref="Collect"/>).</summary>
		private const double CollectSeconds = 10;

		private const string MasterBus = "Master";

		private readonly Random _random = new();
		private readonly AutoBattleRunner _runner = new();
		private readonly MusicPlayer _music = new();
		private readonly Sfx _sfx = new();
		private readonly AutoBattleBadge _badge = new();
		private readonly ChatFeed _chat = new();
		private ChatBubble _bubble = null!;
		private Control _ui = null!;
		private Control _screens = null!;
		private Control? _screen;
		private GameDatabase _database = null!;
		private AccountSession _account = null!;
		private GameApi _api = null!;
		private SaveStore _store = null!;
		private PlayerState _player = null!;

		/// <summary>Aberto num save de teste (<c>--save=nome</c> com o arquivo já existindo): sempre sem servidor e sem virar conta.</summary>
		private bool _testSave;

		private string _slot = DefaultSlot;
		private string _language = ContentLoader.BaseLanguage;

		/// <summary>Há um save aberto (fora da tela de login): só então <see cref="Save"/> grava.</summary>
		private bool _playing;

		private bool _quitting;

		/// <summary>Uma sincronização no meio do jogo já corre (pedidos repetidos não abrem outra pergunta).</summary>
		private bool _resyncing;

		private Updater? _updater;

		/// <summary>As cartas do correio que este save ainda não coletou; nulo antes da primeira busca (ou sem conexão).</summary>
		private IReadOnlyList<Mail>? _mail;

		/// <summary>A janela do correio, enquanto aberta: a busca que chega depois atualiza ela.</summary>
		private MailboxDialog? _mailbox;

		/// <summary>A janela dos amigos, enquanto aberta: a resposta de cada ação refaz a lista nela.</summary>
		private FriendsDialog? _friends;

		/// <summary>O download da versão nova em andamento (Cancelar e fechar o jogo param ele).</summary>
		private System.Threading.CancellationTokenSource? _updateDownload;

		/// <summary>Remonta a tela de agora (depois de uma janela que mudou algo, ou para voltar a ela).</summary>
		private Action _current = () => { };

		// Para onde cada tela de conteúdo volta: vale também depois de uma luta ou da Loja.
		private Action _arenaBack = null!;
		private Action _campaignBack = null!;
		private Action _dungeonsBack = null!;
		private Action _explorationBack = null!;
		private Action _storageBack = null!;

		/// <summary>A busca da tela de Monstros, que fica enquanto o jogo está aberto.</summary>
		private MonsterFilter _monsterFilter = new();

		/// <summary>A busca da coleção na preparação das lutas, que fica enquanto o jogo está aberto.</summary>
		private MonsterFilter _prepFilter = new();
		private Action _summonBack = null!;

		public override void _Ready()
		{
			_campaignBack = _dungeonsBack = _explorationBack = ShowMap;
			_arenaBack = ShowHub;
			_storageBack = _summonBack = ShowHub;
			_slot = Argument("--save=") ?? DefaultSlot;
			_account = new AccountSession(_slot, Argument("--server=") ?? AccountSession.DefaultServer) { Name = "Account" };
			AddChild(_account);
			_collector = new Timer { Name = "Collector", WaitTime = CollectSeconds, Autostart = true, OneShot = true };
			_collector.Timeout += Collect;
			AddChild(_collector);
			_account.Lost += reason => ShowLogin(T(reason == LossReason.Expired ? "account.error_expired" : "account.lost_taken"));
			_account.SyncNeeded += Resync;
			_account.RecoveryKeyIssued += ShowRecoveryKey;
			_account.ConnectionChanged += () =>
			{
				if (_screen is HubScreen hub)
					hub.SetOffline(!_account.Connected);
				RefreshMail();
				RefreshBlocked();
				RefreshChat();
			};

			// Os textos vêm antes dos dados: os nomes dos dados saem no idioma deles. Antes de abrir um save
			// (a tela de login), vale o idioma do último jogo deste aparelho.
			_language = Argument("--language=") ?? _account.Language ?? _account.OfflineStore.Load()?.Language ?? ContentLoader.BaseLanguage;
			ContentLoader.LoadAvatars();
			ContentLoader.LoadTexts(_language);
			_database = ContentLoader.Load();
			UiSession.Database = _database;
			_api = new GameApi(_account, () => _database, _random, () => _testSave || !_account.Playing, () => _player, () => _store.Save(_player));
			UiSession.FeatureSeen = feature => Pref(Preferences.FeaturePrefix + feature);
			UiSession.BattleSpeed = _account.BattleSpeed;
			UiSession.BattleSpeedSaved = speed => _account.BattleSpeed = speed;

			// Ferramenta de conferência: -- --dump-skills=arquivo escreve a descrição de toda habilidade no idioma de agora e sai.
			if (Argument("--dump-skills=") is { Length: > 0 } dump)
			{
				DumpSkills(dump);
				GetTree().Quit();
				return;
			}

			// O Voltar do celular vira Esc (ui_cancel): fecha a janela de cima ou volta de tela.
			GetTree().QuitOnGoBack = false;
			// Fechar a janela solta a sessão antes (Quit).
			GetTree().AutoAcceptQuit = false;

			_ui = new Control { Name = "UI", Theme = GameTheme.Build() };
			_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			AddChild(_ui);
			_screens = new Control { Name = "Screens", MouseFilter = Control.MouseFilterEnum.Ignore };
			_screens.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			_ui.AddChild(_screens);
			_ui.AddChild(_badge);
			_badge.Pressed += OpenAutoBattle;
			// Ferramenta de conferência: -- --summon-sheet=lich --out=arquivo.png junta, lado a lado, a ficha do catálogo de cada variante da família.
			if (Argument("--summon-sheet=") is { Length: > 0 } sheetFamily)
				SummonSheet(sheetFamily, Argument("--out=") ?? "summon_sheet.png");
			AddChild(_runner);
			_runner.RunChanged += run => _badge.Show(run);
			// O nível e as moedas da conta sobem com cada vitória do automático: o Hub, se está aberto, acompanha.
			_runner.Rewarded += () =>
			{
				if (_screen is HubScreen hub)
					hub.Refresh(DateTime.UtcNow);
			};
			// O Chat global: o balão por cima das telas; a conexão da conta traz as linhas.
			_bubble = new ChatBubble(_chat);
			_ui.AddChild(_bubble);
			_bubble.Pressed += () =>
			{
				if (!_chat.Reading)
					ChatDialog.Open(_ui, _chat);
			};
			_chat.Say = _account.Chat.Say;
			_account.Chat.Received += _chat.Add;
			_account.Chat.Refused += _chat.Refuse;
			_account.Chat.LiveChanged += RefreshChat;
			WireRta();
			AddChild(_music);
			AddChild(_sfx);
			ApplyVolume(MasterBus, _account.MasterVolume);
			ApplyVolume(MusicPlayer.Bus, _account.MusicVolume);
			ApplyVolume(Sfx.Bus, _account.EffectsVolume);
			// No PC, a janela fica sempre em 16:9; no celular, a interface ocupa a tela toda.
			// No celular a interface passa a 60 quadros por segundo no máximo (telas de 120 Hz gastariam o
			// dobro de bateria sem ganho num jogo de menus e turnos).
			if (OS.HasFeature("mobile"))
				Engine.MaxFps = 60;
			var aspect = new WindowAspect(OS.HasFeature("mobile") || Argument("--mobile") != null);
			AddChild(aspect);
			// A área segura da tela (o recorte da câmera, os cantos): a interface fica de fora dela.
			void Inset(WindowAspect.Insets insets)
			{
				_ui.OffsetLeft = insets.Left;
				_ui.OffsetTop = insets.Top;
				_ui.OffsetRight = -insets.Right;
				_ui.OffsetBottom = -insets.Bottom;
			}

			aspect.InsetsChanged += Inset;
			Inset(aspect.Current);
			// A faixa que o recuo deixa de fora das telas é do tom do fundo, não do cinza-marrom padrão.
			RenderingServer.SetDefaultClearColor(Palette.Background);
			// A rolagem por arrasto de toda lista (dedo ou mouse, com inércia), vista antes da interface.
			AddChild(new TouchScroll());
			GetWindow().SizeChanged += () => Doodle.RefineAll(GetTree());
			AddChild(new MinimizeGuard(_ui));

			// Com --screen (desenvolvimento), ou depois de escolher jogar sem conta, abre direto o save local.
			var screen = Argument("--screen=");
			_testSave = Argument("--save=") != null && _account.OfflineStore.SavedAt != null;
			if (_testSave || screen != null)
				PlayOffline(screen);
			else
				ShowLogin(resume: _account.Remembered);

			CheckForUpdate();
		}

		public override void _Notification(int what)
		{
			if (!IsNodeReady())
				return;

			if (what == NotificationWMCloseRequest)
				Quit();
			else if (what == NotificationWMGoBackRequest)
				Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = true });
			else if (what == NotificationApplicationPaused)
			{
				// O celular pode matar o jogo em segundo plano: grava a cópia (a conta já está no servidor).
				Save();
			}
			else if (what == NotificationApplicationResumed)
				_account.Wake();
		}

		// Conta -------------------------------------------------------------------------------------

		/// <summary>Abre o jogo de <paramref name="store"/> (o save sem conta ou o da conta) na tela pedida (nula: o Santuário).</summary>
		private void Play(SaveStore store, PlayerState player, string? screen = null)
		{
			_store = store;
			_player = player;
			// Na conta, quem abre o dia (o Convite, a Mana, as fases do tempo) é o servidor, ao baixar.
			if (!_account.Playing)
				Core.Progression.Account.Open(player, _database, DateTime.UtcNow);
			_playing = true;
			_monsterFilter = new MonsterFilter();
			_prepFilter = new MonsterFilter();
			UiSession.Player = player;
			RefreshChat();
			// A chave que chegou no cadastro ou na entrada aparece por cima da primeira tela.
			Callable.From(ShowRecoveryKey).CallDeferred();
			if (Argument("--language=") == null)
				UseLanguage(player.Language ?? _language);

			switch (screen)
			{
				case "map":
					ShowMap();
					break;
				case "campaign":
					Go(Destination.Campaign, ShowMap);
					break;
				case "dungeons":
					Go(Destination.Dungeons, ShowMap);
					break;
				case "exploration":
					Go(Destination.Exploration, ShowMap);
					break;
				case "summon":
					Go(Destination.Summon, ShowHub);
					break;
				case "shop":
					Go(Destination.Shop, ShowHub);
					break;
				case "monsters":
					Go(Destination.Monsters, ShowHub);
					break;
				case "prep":
					PrepStage(_database.Stage(Math.Min(_player.HighestStage + 1, _database.Stages.Count)));
					break;
				case "runes":
					ShowRunes(Teams.Of(_player, Teams.Campaign).FirstOrDefault(), ShowHub);
					break;
				case "compendium":
					Go(Destination.Compendium, ShowHub);
					break;
				case "grimoire":
					ShowHub();
					break;
				case "friends":
					ShowHub();
					Go(Destination.Friends, ShowHub);
					break;
				case "veil":
					ShowVeil();
					break;
				case "battle":
					FightStage(_database.Stage(Math.Min(_player.HighestStage + 1, _database.Stages.Count)));
					break;
				case "tutorial":
					ShowTutorial();
					break;
				default:
					// A conta nova começa pela luta de treino; depois, sempre pelo Santuário.
					if (Tutorial.ShouldStart(_player))
						ShowTutorial();
					else
						ShowHub();
					break;
			}
		}

		private void PlayOffline(string? screen = null)
		{
			var store = _account.OfflineStore;
			Play(store, store.Load() ?? NewPlayer(store.InheritedStarted), screen);
		}

		private PlayerState NewPlayer(DateTime? inheritedStarted = null)
		{
			var player = NewGame.Create(DateTime.UtcNow, _random, _database);
			PlayerSave.InheritStarted(player, inheritedStarted);
			return player;
		}

		/// <summary>
		/// A tela de login. <paramref name="message"/> diz por que o jogo voltou para ela (outro aparelho
		/// entrou, o acesso venceu); com <paramref name="resume"/>, já entra com a conta lembrada.
		/// </summary>
		private void ShowLogin(string? message = null, bool resume = false)
		{
			_playing = false;
			_runner.Dismiss();
			CloseDialogs();
			_chat.Clear();
			RefreshChat();
			var login = new LoginScreen(_account.Email, _account.AccountName, _account.Remembered);
			login.LoginRequested += (email, password) => SignIn(login, () => _account.Login(email, password), false);
			login.RegisterRequested += (email, password, name) => SignIn(login, () => _account.Register(email, password, name), false);
			login.ResumeRequested += () => SignIn(login, _account.Resume, true);
			login.ForgetRequested += _account.Forget;
			login.PasswordResetRequested += email => ResetPassword(login, email);
			Swap(login, () => ShowLogin());
			if (message != null)
				login.ShowMessage(message);
			if (resume)
				SignIn(login, _account.Resume, true);
		}

		/// <summary>
		/// Entra na conta: <paramref name="authenticate"/> (senha, cadastro ou, com <paramref name="resume"/>,
		/// o token guardado), depois a sessão e o save.
		/// </summary>
		private async void SignIn(LoginScreen login, Func<Task<ApiResponse>> authenticate, bool resume)
		{
			login.SetBusy(T("account.connecting"));
			var response = await authenticate();
			if (_screen != login)
				return;

			if (response.Ok)
			{
				await Enter(login, false);
				return;
			}

			login.ShowMessage(AccountError(response, !resume));
			if (!_account.Remembered)
				login.ShowForm();
		}

		/// <summary>Toma a sessão (<paramref name="force"/>: derruba o outro aparelho), resolve o save e abre o Santuário.</summary>
		private async Task Enter(LoginScreen login, bool force)
		{
			login.SetBusy(T("account.claiming"));
			var claim = await _account.Claim(force);
			if (_screen != login)
				return;

			if (claim.Outcome == ClaimOutcome.Busy)
			{
				login.ShowMessage("", false);
				var device = $"[color=#{Palette.Gold.ToHtml(false)}]{(claim.DeviceName ?? "?").Replace("[", "[lb]")}[/color]";
				Dialog.Confirm(login, T("account.busy_title"), T("account.busy_text", device, Seen(claim.LastSeen)), T("account.busy_confirm"), () => _ = Enter(login, true), ButtonKind.Danger);
				return;
			}

			if (claim.Outcome == ClaimOutcome.UpdateRequired)
			{
				login.ShowMessage(T("account.update_required"));
				return;
			}

			if (claim.Outcome == ClaimOutcome.Failed)
			{
				login.ShowMessage(AccountError(claim.Response));
				if (!_account.Remembered)
					login.ShowForm();
				return;
			}

			login.SetBusy(T("account.syncing"));
			var sync = await _api.Sync();
			if (_screen != login)
				return;

			if (!sync.Ok)
			{
				login.ShowMessage(AccountError(sync.Error!));
				return;
			}

			// A conta começa antes do Santuário abrir: ele já sai com o nome dela.
			_account.Begin(() => _playing ? _player : null);
			Play(_account.Store!, sync.Player!);
			if (_account.AccountName == null)
				ChooseName(true);
		}

		/// <summary>
		/// O servidor já tem outra revisão da conta (outro aparelho jogou, a conexão voltou): baixa de novo e, se a cópia
		/// mudou, reabre o Santuário com ela.
		/// </summary>
		private async void Resync()
		{
			if (_resyncing)
				return;

			_resyncing = true;
			var sync = await _api.Sync();
			_resyncing = false;
			if (!_playing || !sync.Downloaded)
				return;

			_runner.Dismiss();
			CloseDialogs();
			Play(_account.Store!, sync.Player!);
		}

		/// <summary>A janela do jogo parado sem conexão (<see cref="RefreshBlocked"/>); nula com o jogo andando.</summary>
		private Dialog? _blocked;

		/// <summary>
		/// Abre ou fecha a janela de sem conexão conforme <see cref="AccountSession.Blocked"/>. Com ela aberta, o
		/// jogo na conta para: a árvore pausa (as lutas, a canalização, as animações), a Batalha automática segura o
		/// relógio, e só a janela e a conta seguem, tentando o servidor. A sincronização que dá certo fecha a janela
		/// e o jogo continua de onde estava.
		/// </summary>
		private void RefreshBlocked()
		{
			if (!_account.Blocked)
			{
				_blocked?.Close();
				return;
			}

			if (_blocked != null)
				return;

			Save();
			var paused = GetTree().Paused;
			GetTree().Paused = true;
			_runner.Held = true;
			var dialog = Dialog.Open(_ui, T("account.blocked_title"), 480, null, "OfflineDialog");
			dialog.ProcessMode = ProcessModeEnum.Always;
			dialog.Dismissable = false;
			dialog.Body.AddChild(Layout.Text(T("account.blocked_text")).Named("Text"));
			var status = Layout.Text("", GameTheme.Faded).Named("Status");
			dialog.Body.AddChild(status);
			GameButton leave = null!, retry = null!;
			leave = dialog.AddAction(T("account.blocked_leave"), async () =>
			{
				// Sai sem esquecer a conta: o login oferece entrar de novo ou jogar sem conta.
				_playing = false;
				leave.Disabled = retry.Disabled = true;
				status.Text = T("account.blocked_leaving");
				await _account.Leave(false);
				ShowLogin();
			}, ButtonKind.Secondary, closes: false);
			retry = dialog.AddAction(T("account.blocked_retry"), async () =>
			{
				retry.Disabled = true;
				status.Text = T("account.connecting");
				await _account.Reconnect();
				if (_blocked != dialog)
					return;
				retry.Disabled = false;
				status.Text = T(_account.Connected ? "account.syncing" : "account.error_offline");
			}, ButtonKind.Primary, closes: false);
			dialog.Closed += () =>
			{
				_blocked = null;
				_runner.Held = false;
				GetTree().Paused = paused;
				// Fechada por fora (uma troca de tela) com o jogo ainda parado: abre de novo.
				Callable.From(RefreshBlocked).CallDeferred();
			};
			_blocked = dialog;
		}

		/// <summary>Ajustes → Sair da conta: solta a sessão, esquece o acesso e volta para o login. Nada a enviar: a conta já está no servidor.</summary>
		private async void SignOut()
		{
			_playing = false;
			Wait(T("account.leaving"));
			await _account.Leave(true);
			ShowLogin();
		}

		/// <summary>Ajustes → Entrar ou criar conta, saindo do jogo sem conta (que sobe para a conta, se ela for nova).</summary>
		private void LeaveOffline()
		{
			// O save de teste nunca vira conta de verdade: sairia do jogo sem conta e subiria para o servidor.
			if (_testSave)
				return;

			Save();
			_account.Offline = false;
			ShowLogin();
		}

		/// <summary>
		/// "Esqueci a senha": troca a senha com a chave de recuperação da conta. Depois, o jogador entra pelo
		/// login com a senha nova (os acessos lembrados da conta deixaram de valer no servidor).
		/// </summary>
		private void ResetPassword(LoginScreen login, string email)
		{
			var dialog = PasswordResetDialog.Open(_ui, email.Length > 0 ? email : _account.Email);
			dialog.ResetSubmitted += async (address, key, password) =>
			{
				dialog.SetBusy();
				var response = await _account.ResetPassword(address, key, password);
				if (!response.Ok)
				{
					dialog.ShowError(AccountError(response));
					return;
				}

				dialog.Close();
				login.ShowMessage(T("account.reset_done"), false);
			};
		}

		private Dialog? _recoveryDialog;

		/// <summary>
		/// A chave de recuperação que o jogador ainda não guardou, por cima de qualquer tela, até o "Já
		/// guardei" (fechar o jogo antes não a perde: ela fica no arquivo da conta).
		/// </summary>
		private void ShowRecoveryKey()
		{
			if (!_playing || _account.PendingRecoveryKey is not { } key || (_recoveryDialog != null && IsInstanceValid(_recoveryDialog)))
				return;

			_recoveryDialog = RecoveryKeyDialog.Open(_ui, key, _account.ConfirmRecoveryKey);
			_recoveryDialog.Closed += () => _recoveryDialog = null;
		}

		/// <summary>
		/// A janela do nome da conta: dar o primeiro nome ou trocar o de agora (Ajustes) ou, com <paramref name="prompt"/>,
		/// pedir um à conta que ainda não tem. Trocar é de graça, uma vez por semana: quem confere e marca a data é o
		/// servidor, junto com o nome. Com o nome aceito, o Santuário se remonta com ele.
		/// </summary>
		private void ChooseName(bool prompt)
		{
			var note = _account.AccountName != null ? T("account.rename_note") : null;
			var dialog = AccountNameDialog.Open(_ui, _account.AccountName, prompt, note);
			dialog.Submitted += async name =>
			{
				dialog.SetBusy();
				var response = await _api.Rename(name);
				if (!response.Ok)
				{
					dialog.ShowError(AccountError(response));
					return;
				}

				dialog.Close();
				RefreshChat();
				if (_screen is HubScreen)
					_current();
			};
		}

		/// <summary>
		/// O balão do Chat global só aparece jogando na conta; ele e a janela mostram a conexão (ao vivo,
		/// conectando com a conta conectada, ou sem conexão).
		/// </summary>
		private void RefreshChat()
		{
			_bubble.Visible = _playing && _account.Playing;
			_chat.Me = _account.AccountName;
			_chat.SetState(_account.Chat.Live, _account.Connected && !_account.Chat.Live);
		}

		/// <summary>Os volumes nos Ajustes: valem na hora e ficam gravados no aparelho.</summary>
		private ConfigVolume[] Volumes() => new[]
		{
			new ConfigVolume("Master", T("config.volume_master"), _account.MasterVolume, volume =>
			{
				_account.MasterVolume = volume;
				ApplyVolume(MasterBus, volume);
			}),
			new ConfigVolume("Music", T("config.volume_music"), _account.MusicVolume, volume =>
			{
				_account.MusicVolume = volume;
				ApplyVolume(MusicPlayer.Bus, volume);
			}),
			new ConfigVolume("Effects", T("config.volume_effects"), _account.EffectsVolume, volume =>
			{
				_account.EffectsVolume = volume;
				ApplyVolume(Sfx.Bus, volume);
			}),
		};

		/// <summary>O volume escolhido (0 a 1) no barramento; 0 cala. As transições da música mexem nos tocadores, não aqui.</summary>
		private static void ApplyVolume(string bus, float volume)
		{
			var index = AudioServer.GetBusIndex(bus);
			if (index < 0)
				return;
			AudioServer.SetBusMute(index, volume <= 0f);
			AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(volume, 0.001f)));
		}

		/// <summary>A conta nos Ajustes.</summary>
		private ConfigAccount AccountSettings()
		{
			if (!_account.Playing)
				return new ConfigAccount(null, null, "", LeaveOffline, SignOut, () => { });

			var status = !_account.Connected ? T("config.offline_account")
				: _account.LastSync is { } at ? T("config.synced", at.ToLocalTime().ToString("t", Culture))
				: "";
			var wait = _account.AccountName != null && AccountRename.NextAt(_player, DateTime.UtcNow) is { } next
				? T("config.rename_wait", next.ToLocalTime().ToString("g", Culture))
				: null;
			return new ConfigAccount(_account.Email, _account.AccountName, status, LeaveOffline, SignOut, () => ChooseName(false), wait);
		}

		/// <summary>Fechar a janela: grava a cópia e solta a sessão (sem esperar o servidor mais que uns segundos).</summary>
		private async void Quit()
		{
			if (_quitting)
				return;
			_quitting = true;
			_updateDownload?.Cancel();
			Save();
			if (_account.Playing)
				await _account.Leave(false);
			GetTree().Quit();
		}

		/// <summary>
		/// O que dizer ao jogador sobre uma resposta do servidor que não deu certo. 401 é senha errada só no
		/// login (<paramref name="login"/>); no resto, é o acesso que venceu.
		/// </summary>
		private static string AccountError(ApiResponse response, bool login = false) => response switch
		{
			{ Unreached: true } => T("account.error_offline"),
			{ Status: 429 } => T("account.error_busy"),
			{ Error: "email_exists" } => T("account.error_exists"),
			{ Error: "invalid_registration" } => T("account.error_registration"),
			{ Error: "invalid_name" } => T("account.error_name", AccountNameDialog.MinLength, AccountNameDialog.MaxLength),
			{ Error: "name_taken" } => T("account.error_name_taken"),
			{ Error: "rename_cooldown" } => T("account.error_rename_cooldown"),
			{ Error: "cloud_unreadable" } => T("account.error_cloud"),
			{ Error: "invalid_key" } => T("account.error_recovery_key"),
			{ Error: "invalid_password" } => T("account.error_password", LoginScreen.MinPassword),
			{ Status: 401 } => T(login ? "account.error_login" : "account.error_expired"),
			_ => T("account.error_server", response.Status),
		};

		/// <summary>Quando o outro aparelho bateu pela última vez, escrito ("agora há pouco", "há 3 min").</summary>
		private static string Seen(DateTimeOffset? lastSeen)
		{
			var minutes = lastSeen is { } at ? (int)(DateTimeOffset.UtcNow - at).TotalMinutes : 0;
			return minutes < 1 ? T("account.seen_now") : T("account.seen_minutes", minutes);
		}

		/// <summary>Uma janela de espera, sem fechar, enquanto o servidor responde.</summary>
		private Dialog Wait(string text)
		{
			var dialog = Dialog.Open(_ui, T("account.wait_title"), 420, null, "WaitDialog");
			dialog.Dismissable = false;
			dialog.Body.AddChild(Layout.Text(text).Named("Text"));
			return dialog;
		}

		/// <summary>Fecha as janelas da tela que sai. A da versão nova fica: ela vale para o jogo, não para uma tela.</summary>
		private void CloseDialogs()
		{
			foreach (var dialog in _ui.GetChildren().OfType<Dialog>().Where(d => d.Name != UpdateDialog.NodeName))
				dialog.Close();
		}

		/// <summary>Põe o jogo no idioma (os textos e os dados, que têm os nomes nele) e o lembra no aparelho.</summary>
		private void UseLanguage(string language)
		{
			_account.Language = language;
			if (language == _language)
				return;

			_language = language;
			ContentLoader.LoadTexts(language);
			_database = ContentLoader.Load();
			UiSession.Database = _database;
			_badge.Show(_runner.Run);
		}

		// Atualização -------------------------------------------------------------------------------

		/// <summary>
		/// O executável exportado do Windows procura uma versão nova ao abrir, sem segurar nada: a resposta
		/// chega com o jogo já na tela. No editor e nas outras plataformas, nunca (o executável seria o do
		/// Godot, ou um APK que não se troca sozinho).
		/// </summary>
		private async void CheckForUpdate()
		{
			if (OS.GetName() != "Windows" || !OS.HasFeature("template")
				|| !Version.TryParse(ProjectSettings.GetSetting("application/config/version").AsString(), out var current))
				return;

			var exe = OS.GetExecutablePath();
			Updater.Cleanup(exe);
			var source = Argument("--updates=") ?? $"{AccountSession.DefaultServer}releases/{Updater.Platform}/";
			_updater = new Updater(new System.Net.Http.HttpClient
			{
				BaseAddress = new Uri(source.EndsWith('/') ? source : source + "/"),
				Timeout = System.Threading.Timeout.InfiniteTimeSpan,
			});
			var check = await _updater.Check(current);
			if (check.State is (UpdateState.Available or UpdateState.Required) && !_quitting)
				OfferUpdate(exe, check.Manifest!, current, check.State == UpdateState.Required);
		}

		/// <summary>A janela da versão nova: baixa ao lado do executável, confere e instala.</summary>
		private void OfferUpdate(string exe, UpdateManifest manifest, Version current, bool required)
		{
			var dialog = UpdateDialog.Open(_ui, manifest.Version, current, manifest.Size, required);
			dialog.QuitRequested += Quit;
			dialog.CancelRequested += () => _updateDownload?.Cancel();
			dialog.BrowserRequested += () => OS.ShellOpen(_updater!.Address(manifest).ToString());
			dialog.UpdateRequested += async () =>
			{
				if (_updateDownload != null)
					return;

				_updateDownload = new System.Threading.CancellationTokenSource();
				dialog.ShowDownloading();
				var result = await _updater!.Download(manifest, Updater.NewPath(exe), new Progress<long>(dialog.ShowProgress), _updateDownload.Token);
				_updateDownload.Dispose();
				_updateDownload = null;
				if (_quitting)
					return;

				switch (result)
				{
					case DownloadResult.Done:
						dialog.ShowInstalling();
						InstallUpdate(exe, dialog);
						break;
					case DownloadResult.Canceled:
						dialog.ShowOffer();
						break;
					case DownloadResult.CannotWrite:
						dialog.ShowOffer(T("update.error_write"), browser: true);
						break;
					default:
						dialog.ShowOffer(T(result == DownloadResult.Corrupt ? "update.error_corrupt" : "update.error_offline"));
						break;
				}
			};
		}

		/// <summary>
		/// Instala o executável baixado: grava e solta a sessão, como ao fechar, e só então
		/// troca os arquivos e fecha. Depois da troca nada mais pode carregar: o Godot lê o resto do jogo do
		/// próprio .exe pelo caminho, que a essa altura já é o do novo. O novo abre quando este terminar.
		/// </summary>
		private async void InstallUpdate(string exe, UpdateDialog dialog)
		{
			if (_quitting)
				return;
			_quitting = true;
			Save();
			if (_account.Playing)
				await _account.Leave(false);

			try
			{
				Updater.Swap(exe);
			}
			catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
			{
				// Nada trocou (o Swap desfaz), mas a conta já saiu: o certo é fechar e abrir de novo.
				_quitting = false;
				dialog.ShowFailed(T("update.error_install"));
				return;
			}

			var arguments = OS.GetCmdlineArgs().ToList();
			if (OS.GetCmdlineUserArgs() is { Length: > 0 } user)
				arguments.AddRange(user.Prepend("--"));
			// Se nem isso abrir, o jogo já está atualizado: o jogador abre de novo.
			Updater.Relaunch(exe, OS.GetProcessId(), arguments);
			GetTree().Quit();
		}

		// Telas -------------------------------------------------------------------------------------

		private void ShowHub()
		{
			var hub = new HubScreen(_database, _player, _testSave ? _slot : _account.Playing ? _account.AccountName : null, _account.Playing && !_account.Connected);
			hub.Requested += destination => Go(destination, ShowHub);
			hub.ConfigRequested += () => ConfigPanel.Open(hub, ContentLoader.Languages(), _language, language =>
			{
				Pref(Preferences.LanguagePrefix + language);
				UseLanguage(language);
				ShowHub();
			}, Volumes(), AccountSettings(), ShowTutorial);
			hub.CollectRequested += () => Do(hub, Request(ServerActionKind.CollectIdle), d => Sfx.PlayIf(d.Idle is { IsEmpty: false }, "rewards.essence"), () => hub.Refresh(DateTime.UtcNow));
			hub.MailRequested += () => OpenMailbox(hub);
			hub.AvatarRequested += (summon, awakened) => Do(hub, Request(ServerActionKind.SetAvatar, flag: awakened, text: summon), null, () => hub.Refresh(DateTime.UtcNow));
			Swap(hub, ShowHub);
			hub.SetMail(_mail?.Count);
			RefreshMail();
			PromptRuneKit(hub);
		}

		/// <summary>
		/// Há um kit de sigilos por abrir (prêmio da fase 20): pergunta o conjunto, e o servidor sorteia os sigilos.
		/// "Depois" o deixa guardado.
		/// </summary>
		private void PromptRuneKit(Control from)
		{
			if (!RuneKits.HasPending(_player))
				return;

			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(from) && from.IsInsideTree() && RuneKits.HasPending(_player))
					RuneKitDialog.Open(from, _player, async set =>
						await Act(Request(ServerActionKind.OpenRuneKit, text: set.ToString())) is { } delta ? delta.Runes : null);
			}).CallDeferred();
		}

		// Correio ------------------------------------------------------------------------------------

		/// <summary>
		/// Busca as cartas da conta (a cada volta ao Santuário e quando a conexão volta) e atualiza o selo e a
		/// janela aberta. A carta que a conta já coletou não aparece.
		/// </summary>
		private async void RefreshMail()
		{
			if (!_account.Connected)
			{
				_mail = null;
				(_screen as HubScreen)?.SetMail(null);
				return;
			}

			var fetch = await _account.FetchMail();
			if (!_playing)
				return;
			if (!fetch.Ok)
			{
				_mailbox?.ShowMessage(T("mail.error"));
				return;
			}

			_mail = Mailbox.Unclaimed(_player, fetch.Mail);
			(_screen as HubScreen)?.SetMail(_mail.Count);
			_mailbox?.Show(_mail);
		}

		/// <summary>A janela do correio: as cartas já buscadas na hora e, depois, as da busca nova.</summary>
		private void OpenMailbox(HubScreen hub)
		{
			var mailbox = MailboxDialog.Open(hub, _database);
			if (!_account.Playing)
			{
				mailbox.ShowMessage(T("mail.no_account"));
				return;
			}

			if (!_account.Connected)
			{
				mailbox.ShowMessage(T("mail.offline"));
				return;
			}

			_mailbox = mailbox;
			mailbox.Closed += () =>
			{
				if (_mailbox == mailbox)
					_mailbox = null;
			};
			mailbox.ClaimRequested += mail => ClaimMail(new[] { mail });
			mailbox.ClaimAllRequested += () => ClaimMail(_mail ?? Array.Empty<Mail>());
			if (_mail != null)
				mailbox.Show(_mail);
			RefreshMail();
		}

		/// <summary>
		/// Coleta, uma carta por vez: o servidor confere a carta, paga e a marca como coletada; o que mudou volta na
		/// resposta. Sem conexão, nada é coletado.
		/// </summary>
		private async void ClaimMail(IEnumerable<Mail> letters)
		{
			var claimed = 0;
			foreach (var mail in letters.ToList())
			{
				if (await Act(Request(ServerActionKind.ClaimMail, text: mail.Id)) != null)
					claimed++;
			}

			if (claimed == 0)
				return;

			Sfx.Play("rewards.daily_reward");
			_mail = Mailbox.Unclaimed(_player, _mail ?? Array.Empty<Mail>());
			if (_screen is HubScreen hub)
			{
				hub.Refresh(DateTime.UtcNow);
				hub.SetMail(_mail.Count);
			}

			_mailbox?.Show(_mail);
		}

		// Amigos -------------------------------------------------------------------------------------

		/// <summary>
		/// A janela dos amigos: só com a conta conectada (quem guarda os amigos é o servidor). Busca a lista ao
		/// abrir e de novo depois de cada ação.
		/// </summary>
		private void OpenFriends()
		{
			var friends = FriendsDialog.Open(_screen ?? _ui);
			if (!_account.Playing)
			{
				friends.ShowMessage(T("friends.no_account"));
				return;
			}

			if (!_account.Connected)
			{
				friends.ShowMessage(T("friends.offline"));
				return;
			}

			_friends = friends;
			friends.Closed += () =>
			{
				if (_friends == friends)
					_friends = null;
			};
			friends.InviteRequested += name => FriendAction(friends, _account.Friends.Invite(name),
				response => T(response.Text("status") == "friends" ? "friends.now_friends" : "friends.invited", name));
			friends.AcceptRequested += friend => FriendAction(friends, _account.Friends.Accept(friend.Id), _ => T("friends.now_friends", friend.Name));
			friends.RemoveRequested += friend => FriendAction(friends, _account.Friends.Remove(friend.Id), null);
			RefreshFriends(friends);
		}

		private async void RefreshFriends(FriendsDialog friends)
		{
			var fetch = await _account.Friends.Fetch();
			if (!_playing || _friends != friends)
				return;
			if (fetch.List is { } list)
				friends.Show(list);
			else
				friends.ShowMessage(FriendsError(fetch.Response));
		}

		/// <summary>
		/// Uma ação nos amigos: o servidor responde, a janela mostra o resultado (<paramref name="done"/>, ou o
		/// porquê da recusa) e a lista de novo.
		/// </summary>
		private async void FriendAction(FriendsDialog friends, Task<ApiResponse> request, Func<ApiResponse, string>? done)
		{
			var response = await request;
			if (!_playing || _friends != friends)
				return;
			if (!response.Ok)
				friends.Notice(FriendsError(response), true);
			else if (done != null)
				friends.Notice(done(response), false);
			RefreshFriends(friends);
		}

		/// <summary>A recusa do servidor nos amigos, escrita: a que ele nomeou, ou a genérica.</summary>
		private static string FriendsError(ApiResponse response) => response switch
		{
			{ Unreached: true } => T("friends.offline"),
			{ Status: 429 } => T("account.error_busy"),
			{ Error: { } error } when Has($"friends.error_{error}") => T($"friends.error_{error}"),
			_ => T("friends.error"),
		};

		/// <summary>
		/// A luta de treino (<see cref="Tutorial"/>), com o Mestre ensinando: no fim, ou ao sair pela pausa,
		/// conta como feita (não abre mais sozinha) e leva à primeira invocação, se a conta ainda não invocou
		/// (o Mestre segue lá, <see cref="Tutorial.GuidesFirstSummon"/>); senão, ao Santuário. Não cobra nem dá nada.
		/// </summary>
		private void ShowTutorial()
		{
			var coach = new TutorialCoach();
			var session = BattleFactory.Create(_database, Tutorial.Team(_database), Tutorial.Encounter(), _random.Next());
			var battle = new BattleScreen(session, T("tutorial.battle_title"), false, coach);
			var done = false;
			void Done()
			{
				if (done)
					return;
				done = true;
				Pref(Preferences.TutorialDone, true);
				if (Tutorial.GuidesFirstSummon(_player, _database))
					Go(Destination.Summon, ShowHub);
				else
					ShowHub();
			}

			battle.Finished += async _ =>
			{
				await coach.End(Tutorial.GuidesFirstSummon(_player, _database) ? T("tutorial.to_summon") : T("tutorial.end_button"));
				Done();
			};
			battle.Closed += _ => Done();
			battle.RestartRequested += _ =>
			{
				done = true;
				ShowTutorial();
			};
			Swap(battle, Done);
		}

		private void ShowMap()
		{
			var map = new MapScreen(_database, _player);
			map.BackRequested += ShowHub;
			map.Requested += destination => Go(destination, ShowMap);
			Swap(map, ShowMap);
		}

		/// <summary>Abre um destino de navegação; <paramref name="back"/> é para onde ele volta.</summary>
		private void Go(Destination destination, Action back)
		{
			switch (destination)
			{
				case Destination.Campaign:
					_campaignBack = back;
					ShowCampaign(null, null);
					break;
				case Destination.Dungeons:
					_dungeonsBack = back;
					ShowDungeons(null);
					break;
				case Destination.Exploration:
					_explorationBack = back;
					ShowExploration(null);
					break;
				case Destination.Summon:
					_summonBack = back;
					ShowSummon();
					break;
				case Destination.Monsters:
					_storageBack = back;
					ShowStorage(null);
					break;
				case Destination.Runes:
					ShowRunes(null, back);
					break;
				case Destination.Shop:
					ShowShop(back);
					break;
				case Destination.Compendium:
					ShowCompendium(back);
					break;
				case Destination.Map:
					ShowMap();
					break;
				case Destination.Arena:
					_arenaBack = back;
					ShowArena();
					break;
				case Destination.Friends:
					OpenFriends();
					break;
			}
		}

		/// <summary>A Campanha na fase <paramref name="selected"/> (nula: a próxima a vencer), com um aviso embaixo.</summary>
		private void ShowCampaign(int? selected, string? message)
		{
			var campaign = new CampaignScreen(_database, _player, selected);
			campaign.BackRequested += () => _campaignBack();
			campaign.FightRequested += PrepStage;
			campaign.ShopRequested += () => ShowShop(() => ShowCampaign(campaign.Selected, null));
			campaign.RepeatRequested += stage => RepeatStage(campaign, stage);
			Swap(campaign, () => ShowCampaign(campaign.Selected, null));
			if (message != null)
				campaign.ShowMessage(message);
		}

		private void ShowDungeons(string? selected, string? message = null)
		{
			var dungeons = new DungeonScreen(_database, _player, selected);
			dungeons.BackRequested += () => _dungeonsBack();
			dungeons.FightRequested += PrepFloor;
			dungeons.ShopRequested += dungeon => ShowShop(() => ShowDungeons(dungeon.Id));
			dungeons.RepeatRequested += (dungeon, floor) => RepeatFloor(dungeons, dungeon, floor);
			Swap(dungeons, () => ShowDungeons(selected));
			if (message != null)
				dungeons.ShowMessage(message);
		}

		/// <summary>
		/// A Exploração Estelar na constelação <paramref name="selected"/> (nula: a próxima a vencer no mês). Num
		/// mês novo a tela já mostra o percurso do começo; quem o recomeça no save é o servidor, na primeira vitória.
		/// </summary>
		private void ShowExploration(int? selected, string? message = null)
		{
			var now = DateTime.UtcNow;
			var exploration = new ExplorationScreen(_database, _player, now, selected);
			exploration.BackRequested += () => _explorationBack();
			exploration.FightRequested += PrepConstellation;
			Swap(exploration, () => ShowExploration(exploration.Selected));
			if (message != null)
				exploration.ShowMessage(message);
		}

		private void ShowSummon()
		{
			var summon = new SummonScreen(_database, _player, Tutorial.GuidesFirstSummon(_player, _database) ? new TutorialCoach(330) : null);
			summon.BackRequested += () => _summonBack();
			summon.ShopRequested += () => ShowShop(ShowSummon);
			summon.MonstersRequested += () =>
			{
				_storageBack = ShowSummon;
				ShowStorage(null);
			};
			summon.SummonRequested += async (count, kind) =>
			{
				var delta = await Act(new ActionRequest { Kind = ServerActionKind.Summon, Count = count, Scroll = kind });
				if (delta == null || !GodotObject.IsInstanceValid(summon))
					return;

				// A invocação já está no servidor (ele sorteou e anunciou no Chat): fechar o jogo na hora não a desfaz.
				summon.ShowResults(ServerActions.Results(_database, delta), null);
			};
			summon.ExchangeRequested += async id =>
			{
				var delta = await Act(Request(ServerActionKind.ExchangeFragments, text: id));
				if (delta == null || !GodotObject.IsInstanceValid(summon))
					return;

				summon.ShowResults(ServerActions.Results(_database, delta), null);
			};
			Swap(summon, ShowSummon);
		}

		private void ShowStorage(int? selected)
		{
			var storage = new StorageScreen(_database, _player, selected, _monsterFilter);
			storage.BackRequested += () =>
			{
				// Saiu da tela: os novos foram vistos.
				Pref(Preferences.SeenMonsters);
				_storageBack();
			};
			storage.FilterChanged += filter => _monsterFilter = filter;
			storage.RunesRequested += id => ShowRunes(id, () => ShowStorage(id));
			storage.InfuseRequested += (id, toMax) => Do(storage, Request(ServerActionKind.Infuse, id, flag: toMax), d => Sfx.Play(d.Count > 0 ? "rewards.level_up" : "rewards.experience"), storage.Refresh);
			storage.AwakenRequested += id => Do(storage, Request(ServerActionKind.Awaken, id), _ => Sfx.Play("rewards.star_up"), storage.Refresh);
			storage.EvolveRequested += id => Do(storage, Request(ServerActionKind.Evolve, id), _ => Sfx.Play("rewards.star_up"), storage.Refresh);
			storage.StoreRequested += id => Do(storage, Request(ServerActionKind.Shelve, ids: new[] { id }, flag: true), _ => Sfx.Play("ui.unequip_item"), storage.Refresh);
			storage.RetrieveRequested += id => Do(storage, Request(ServerActionKind.Shelve, ids: new[] { id }), _ => Sfx.Play("ui.equip_item"), storage.Refresh);
			storage.StoreManyRequested += ids => Do(storage, Request(ServerActionKind.Shelve, ids: ids, flag: true), _ => Sfx.Play("ui.unequip_item"), storage.Refresh);
			storage.RetrieveManyRequested += ids => Do(storage, Request(ServerActionKind.Shelve, ids: ids), _ => Sfx.Play("ui.equip_item"), storage.Refresh);
			storage.FuseRequested += (target, materials) => Do(storage, Request(ServerActionKind.Fuse, target, materials), _ => Sfx.Play("rewards.star_up"), storage.Refresh);
			storage.ReleaseRequested += ids => Do(storage, Request(ServerActionKind.Release, ids: ids), _ => Sfx.Play("rewards.essence"), storage.Refresh);
			storage.LockRequested += id => Do(storage, new ActionRequest { Kind = ServerActionKind.Toggle, Flag = true, Id = id, Index = 0 }, _ => Sfx.Play(_player.Monster(id)!.Locked ? "ui.toggle_on" : "ui.toggle_off"), storage.Refresh);
			storage.FavoriteRequested += id => Do(storage, new ActionRequest { Kind = ServerActionKind.Toggle, Flag = true, Id = id, Index = 1 }, _ => Sfx.Play(_player.Monster(id)!.Favorite ? "ui.toggle_on" : "ui.toggle_off"), storage.Refresh);
			Swap(storage, () => ShowStorage(selected));
		}

		private void ShowShop(Action back)
		{
			var shop = new ShopScreen(_database, _player);
			shop.BackRequested += back;
			shop.BuyRequested += offer =>
			{
				Do(shop, Request(ServerActionKind.BuyShop, text: offer.Id), _ =>
				{
					Sfx.Play("rewards.gold");
					shop.ShowMessage(T("shop.bought", Texts.Amount(offer.Item, offer.Amount)));
				}, shop.Refresh);
			};
			Swap(shop, () => ShowShop(back));
		}

		private void ShowRunes(int? monsterId, Action back)
		{
			var runes = new RuneScreen(_database, _player, monsterId);
			runes.BackRequested += _ => back();
			runes.EquipRequested += (id, monster) => Do(runes, Request(ServerActionKind.EquipRunes, monster, new[] { id }), _ => Sfx.Play("ui.equip_item"), runes.Refresh);
			runes.EquipManyRequested += (ids, monster) => Do(runes, Request(ServerActionKind.EquipRunes, monster, ids), _ => Sfx.Play("ui.equip_item"), runes.Refresh);
			runes.UnequipRequested += id => Do(runes, Request(ServerActionKind.UnequipRunes, ids: new[] { id }), _ => Sfx.Play("ui.unequip_item"), runes.Refresh);
			runes.UpgradeRequested += async (id, target) =>
			{
				var from = Rune(id).Level;
				await UpgradeRune(Rune(id), target);
				if (!GodotObject.IsInstanceValid(runes))
					return;

				runes.Upgraded(id, from);
				runes.Refresh();
			};
			runes.GrindRequested += (id, index, tool) => RuneAction(runes, new ActionRequest { Kind = ServerActionKind.GrindRune, RuneId = id, Index = index, Tool = tool });
			runes.EnchantRequested += (id, index, tool) => RuneAction(runes, new ActionRequest { Kind = ServerActionKind.EnchantRune, RuneId = id, Index = index, Tool = tool });
			runes.SellRequested += id => Do(runes, Request(ServerActionKind.SellRunes, ids: new[] { id }), _ => Sfx.Play("rewards.gold"), runes.Refresh);
			runes.SellManyRequested += ids => Do(runes, Request(ServerActionKind.SellRunes, ids: ids), _ => Sfx.Play("rewards.gold"), runes.Refresh);
			runes.LockRequested += id => Do(runes, Request(ServerActionKind.Toggle, id), _ => Sfx.Play(Rune(id).Locked ? "ui.toggle_on" : "ui.toggle_off"), runes.Refresh);
			runes.ReappraiseRequested += id => RuneAction(runes, new ActionRequest { Kind = ServerActionKind.ReappraiseRune, RuneId = id });
			Swap(runes, () => ShowRunes(monsterId, back));
		}

		private void ShowCompendium(Action back)
		{
			var compendium = new CompendiumScreen(_database, _player);
			compendium.BackRequested += back;
			Swap(compendium, () => ShowCompendium(back));
		}

		// Lutas -------------------------------------------------------------------------------------

		/// <summary>
		/// A preparação de uma luta (<see cref="PrepScreen"/>): a equipe do conteúdo <paramref name="content"/>
		/// se monta lá, e cada troca já fica guardada. <paramref name="auto"/> nula: a luta ainda não tem
		/// Batalha automática.
		/// </summary>
		private void ShowPrep(string title, string content, Encounter encounter, int mana, Action fight, Action<Control>? auto, Action back)
		{
			var prep = new PrepScreen(_database, _player, title, content, encounter, mana, auto != null, _prepFilter);
			prep.BackRequested += back;
			prep.FightRequested += fight;
			if (auto != null)
				prep.AutoBattleRequested += () => auto(prep);
			WirePrep(prep, content);
			Swap(prep, () => ShowPrep(title, content, encounter, mana, fight, auto, back));
		}

		/// <summary>Liga a montagem da equipe (pôr, tirar, trocar, Líder, filtro) de uma preparação ao conteúdo dela.</summary>
		private void WirePrep(PrepScreen prep, string content)
		{
			prep.ToggleRequested += id => Do(prep, Team(TeamOp.Toggle, content, id), _ => Sfx.Play(Teams.Of(_player, content).Contains(id) ? "ui.equip_item" : "ui.unequip_item"), prep.Refresh);
			prep.ReplaceRequested += (outgoing, incoming) => Do(prep, Team(TeamOp.Replace, content, outgoing, incoming), _ => Sfx.Play("ui.equip_item"), prep.Refresh);
			prep.LeaderRequested += id => Do(prep, Team(TeamOp.Leader, content, id), _ => Sfx.Play("ui.equip_item"), prep.Refresh);
			prep.FilterChanged += filter => _prepFilter = filter;
		}

		// Arena Astral -------------------------------------------------------------------------------

		/// <summary>
		/// A Arena: antes de abrir põe a conta em dia (<see cref="Arena.Renew"/>: luta deixada pela metade vira
		/// derrota, Convites recarregam, a semana virada paga o prêmio).
		/// </summary>
		private async void ShowArena(string? message = null)
		{
			// O servidor põe a Arena em dia (luta deixada pela metade vira derrota, Convites, semana) e completa a lista com as defesas de outros jogadores.
			var opened = await Act(new ActionRequest { Kind = ServerActionKind.ArenaOpen });
			if (opened == null)
			{
				if (_screen is HubScreen || _screen is MapScreen)
					return;
				ShowHub();
				return;
			}

			ShowArenaScreen(message, opened.Count > 0, DateTime.UtcNow);
		}

		/// <summary>A aba da Arena em que o jogador estava: a volta de uma preparação ou luta abre nela.</summary>
		private ArenaScreen.View _arenaTab = ArenaScreen.View.Defense;

		private ArenaScreen ShowArenaScreen(string? message, bool abandoned, DateTime now)
		{
			var arena = new ArenaScreen(_database, _player, now) { Filter = _prepFilter, Current = _arenaTab };
			arena.TabChanged += tab => _arenaTab = tab;
			arena.BackRequested += () => _arenaBack();
			arena.OpponentRequested += PrepArenaOpponent;
			arena.RivalRequested += PrepArenaRival;
			arena.ToggleRequested += id => Do(arena, Team(TeamOp.Toggle, Teams.ArenaDefense, id), _ => Sfx.Play(Teams.Of(_player, Teams.ArenaDefense).Contains(id) ? "ui.equip_item" : "ui.unequip_item"), arena.Refresh);
			arena.ReplaceRequested += (outgoing, incoming) => Do(arena, Team(TeamOp.Replace, Teams.ArenaDefense, outgoing, incoming), _ => Sfx.Play("ui.equip_item"), arena.Refresh);
			arena.LeaderRequested += id => Do(arena, Team(TeamOp.Leader, Teams.ArenaDefense, id), _ => Sfx.Play("ui.equip_item"), arena.Refresh);
			arena.FilterChanged += filter => _prepFilter = filter;
			arena.ShopRequested += ShowCosmicShop;
			arena.VeilRequested += () => ShowVeil();
			arena.RefreshRequested += () => Dialog.Confirm(arena, T("arena.refresh"), T("arena.refresh_confirm"), T("arena.refresh"), () =>
				Do(arena, new ActionRequest { Kind = ServerActionKind.ArenaRefresh }, null, () => ShowArena()));
			Swap(arena, () => ShowArena());

			if (message != null)
				arena.ShowMessage(message);
			else if (abandoned)
				arena.ShowMessage(T("arena.abandoned"));

			if (_player.Arena.PayoutUnseen)
			{
				var state = _player.Arena;
				var legendary = state.LastPayoutLegendary > 0 ? T("arena.payout_legendary", state.LastPayoutLegendary) : "";
				Pref(Preferences.ArenaPayoutSeen);
				Dialog.Info(arena, T("arena.payout_title"), T("arena.payout_text", state.LastPayoutGrade ?? "", Texts.Number(state.LastPayoutGold), legendary));
			}

			return arena;
		}

		/// <summary>A preparação contra um da lista: a equipe que ataca (quatro) e a defesa dele.</summary>
		private void PrepArenaOpponent(int index)
		{
			var opponent = Arena.Opponents(_player, _database)[index];
			ShowArenaPrep(opponent.Name, opponent.Team, () => FightArena(Arena.ListKey(index), opponent.Name, opponent.Team));
		}

		private void PrepArenaRival(ArenaRival rival)
		{
			var team = ArenaTeams.Rival(_database, rival);
			ShowArenaPrep(rival.Name, team, () => FightArena(Arena.RivalKey(rival.Id), rival.Name, team));
		}

		private void ShowArenaPrep(string opponent, BattleTeam defense, Action fight)
		{
			var title = T("destination.Arena");
			var prep = new PrepScreen(_database, _player, title, Teams.Arena, ArenaEncounter, 0, false, _prepFilter)
			{
				Defenders = Enumerable.Range(0, defense.Members.Count).Select(i => BattleFactory.DefenderUnit(defense, i)).ToList(),
				DefenderName = opponent,
				Invites = 1,
			};
			prep.BackRequested += () => ShowArena();
			prep.FightRequested += fight;
			WirePrep(prep, Teams.Arena);
			Swap(prep, () => ShowArenaPrep(opponent, defense, fight));
		}

		/// <summary>O encontro vazio que a preparação pede: a Arena não tem onda, a defesa vem pronta.</summary>
		private static readonly Encounter ArenaEncounter = new(1, 1, new List<IReadOnlyList<StageEnemy>>());

		/// <summary>
		/// O duelo: o Convite sai ao começar (sair no meio vale derrota), a sequência de vitórias soma Vida e
		/// Ataque ao time, e o resultado traz os pontos, a sequência e o Astrarium. Estourar as rodadas é empate.
		/// </summary>
		private async void FightArena(string key, string title, BattleTeam defense)
		{
			if (NeedsTeam(Teams.Arena, () => ShowArena(T("arena.no_team"))) || BusyWithAutoBattle(() => FightArena(key, title, defense)))
				return;

			if (_fightStarting)
				return;

			_fightStarting = true;
			BattleReport? begun;
			try
			{
				begun = await StartBattle(new BattleRequest { Kind = BattleKind.Arena, Key = key });
			}
			finally
			{
				_fightStarting = false;
			}

			if (begun == null)
				return;

			if (begun.Problem != EntryProblem.None)
			{
				ShowArena(T("arena.no_invites"));
				return;
			}

			// A sequência que vale é a da hora em que o servidor começou a luta.
			var (health, attack) = Arena.StreakBonus(_player, _database.Arena, begun.Clock);
			var team = PlayerTeam.Build(_player, _database, Teams.Arena);
			var session = BattleFactory.CreateDuel(_database, team, defense, begun.Seed, health, attack);
			var battle = new BattleScreen(session, T("destination.Arena") + " · " + title, false, focusBoss: _player.FocusBoss, hideRival: true);

			battle.Finished += async localVictory =>
			{
				var members = Teams.Of(_player, Teams.Arena)
					.Select(_player.Monster)
					.OfType<OwnedSummon>()
					.Where(m => !m.Stored && _database.HasSummon(m.SummonId))
					.Select(m => (Monster: m, m.Level, m.Experience))
					.ToList();
				var settled = new BattleRequest { Kind = BattleKind.Arena, Key = key, Ticket = begun.Ticket, Actions = battle.Actions.ToList() };
				var report = await FinishBattle(settled);
				if (report == null)
				{
					ShowArena();
					return;
				}

				var monsters = members.Select(m => ResultOf(m.Monster, m.Level, m.Experience)).ToList();
				battle.ShowResult(new BattleOutcome(report.Won, null, monsters, _player.AccountLevel, null, false, Arena: report.Outcome));
			};
			battle.FocusBossChanged += on => Pref(Preferences.FocusBoss, on);
			// Sair no meio não manda nada: o servidor perde a luta pendente na próxima vez que a Arena abre.
			battle.Closed += _ => ShowArena();
			battle.RestartRequested += _ => ShowArena();
			Swap(battle, () => ShowArena());
		}

		// Arena do Véu --------------------------------------------------------------------------------

		private VeilScreen? _veil;
		private OnlineBattle? _online;

		/// <summary>O treino contra um fantasma que está rodando aqui; nulo numa partida pelo servidor.</summary>
		private LocalRta? _local;

		private IRtaChannel RtaChannel => _local != null ? _local : _account.Rta;
		private RtaSeat _veilSeat;
		private string _veilRival = "";

		/// <summary>Liga uma vez a conversa ao vivo da Arena do Véu: o que chega vai para a tela de agora (saguão ou luta).</summary>
		private void WireRta()
		{
			var link = _account.Rta;
			link.Opened += () => _veil?.SetLive(true, false);
			link.Closed += reason =>
			{
				_veil?.SetLive(false, false);
				if (_local == null)
					_online?.Post(new RtaMessage { Type = "error", Error = reason ?? "closed" });
				if (reason != null)
					_veil?.ShowMessage(T("veil.err_generic"));
			};
			link.Received += OnRta;
		}

		private void ShowVeil(string? message = null)
		{
			var screen = new VeilScreen(_database, _player);
			_veil = screen;
			screen.QueueRequested += ghost => _ = ghost ? StartTraining() : SendQueue();
			screen.CancelRequested += () => _ = RtaChannel.Send(new RtaMessage { Type = "cancel" });
			screen.PickRequested += id => _ = RtaChannel.Send(new RtaMessage { Type = "pick", Summon = id });
			screen.BanRequested += id => _ = RtaChannel.Send(new RtaMessage { Type = "ban", Summon = id });
			screen.LeadRequested += id => _ = RtaChannel.Send(new RtaMessage { Type = "lead", Summon = id });
			screen.LeaveRequested += () => _ = RtaChannel.Send(new RtaMessage { Type = "leave" });
			screen.ShopRequested += ShowVeilShop;
			screen.BackRequested += () => ShowArena();
			// Sair da tela sem estar numa luta fecha a conversa: quem some da Arena não fica esperando na fila.
			screen.TreeExiting += () =>
			{
				if (_veil == screen)
					_veil = null;
				if (_online == null)
					CloseRta();
			};
			Swap(screen, () => ShowVeil());
			if (message != null)
				screen.ShowMessage(message);

			if (!_account.Connected)
			{
				screen.SetLive(false, false);
				return;
			}

			screen.SetLive(_account.Rta.Live, !_account.Rta.Live);
			_account.Rta.Open();
		}

		private async Task SendQueue()
		{
			// O servidor monta o baú pelo save que ele guarda, que já é o da conta: nada a enviar antes.
			if (!_account.Rta.Live)
				return;
			await _account.Rta.Send(new RtaMessage { Type = "queue" });
		}

		/// <summary>O Treino: a sala roda aqui contra um fantasma, sem servidor, e as mensagens dela entram pelo mesmo caminho das do servidor.</summary>
		private Task StartTraining()
		{
			if (_local != null)
				return Task.CompletedTask;
			if (_account.Rta.Live)
				_ = _account.Rta.Send(new RtaMessage { Type = "cancel" });
			_local = LocalRta.Start(_database, _player, _account.AccountName ?? T("veil.you"), _random, OnRta);
			if (_local == null)
				_veil?.ShowMessage(T("veil.err_pool"));
			return Task.CompletedTask;
		}

		/// <summary>Fecha o que estiver aberto da Arena do Véu: o treino local e a conexão com o servidor.</summary>
		private void CloseRta()
		{
			_local?.Stop();
			_local = null;
			_account.Rta.Close();
		}

		private void OnRta(RtaMessage message)
		{
			switch (message.Type)
			{
				case "profile":
				case "queued":
					ResyncThen(() => _veil?.Receive(message));
					break;

				case "synced":
					ResyncThen(() => _veil?.Receive(message));
					break;

				case "matched":
					_veilSeat = message.Seat ?? RtaSeat.First;
					_veilRival = message.Opponent ?? "";
					_veil?.Receive(message);
					break;

				case "battle":
					StartOnlineBattle(message);
					break;

				case "turn":
				case "acted":
					_online?.Post(message);
					break;

				case "end":
					if (_online != null)
						_online.Post(message);
					else
					{
						_local = null;
						_veil?.Receive(message);
					}

					break;

				default:
					_veil?.Receive(message);
					break;
			}
		}

		/// <summary>O servidor mudou a conta por conta própria (Véu): baixa a diferença e só então mostra.</summary>
		private async void ResyncThen(Action then)
		{
			await _api.Resync();
			then();
		}

		private void StartOnlineBattle(RtaMessage message)
		{
			if (RtaReplica.From(_database, message) is not { } replica)
			{
				_ = RtaChannel.Send(new RtaMessage { Type = "leave" });
				return;
			}

			var online = new OnlineBattle(replica, _veilSeat, _account.AccountName ?? "", _veilRival, RtaChannel.Send);
			_online = online;
			var battle = new BattleScreen(online, T("veil.title") + " · " + _veilRival);
			battle.Closed += _ =>
			{
				_online = null;
				_local = null;
				ShowVeil();
			};
			Swap(battle, () => { });
		}

		private void ShowVeilShop()
		{
			var shop = new CosmicShopScreen(_database, _player, DateTime.UtcNow, veil: true);
			shop.BackRequested += () => ShowVeil();
			shop.BuyRequested += offer => Do(shop, new ActionRequest { Kind = ServerActionKind.VeilBuy, Text = offer.Id }, _ =>
			{
				Sfx.Play("rewards.gold");
				shop.ShowMessage(T("veil.bought", offer.Name));
			}, shop.Refresh);
			Swap(shop, ShowVeilShop);
		}

		private void ShowCosmicShop()
		{
			var shop = new CosmicShopScreen(_database, _player, DateTime.UtcNow);
			shop.BackRequested += () => ShowArena();
			shop.BuyRequested += offer => Do(shop, new ActionRequest { Kind = ServerActionKind.ArenaBuy, Text = offer.Id }, _ =>
			{
				Sfx.Play("rewards.gold");
				shop.ShowMessage(T("arena.bought", offer.Name));
			}, shop.Refresh);
			Swap(shop, ShowCosmicShop);
		}

		/// <summary>O Lutar de uma fase da Campanha: a preparação, que volta para a fase.</summary>
		private void PrepStage(StageDefinition stage) => ShowPrep(
			T("battle.title_stage", stage.Number, stage.Name),
			Teams.Campaign,
			stage.Encounter,
			stage.Mana,
			() => FightStage(stage),
			Features.IsOpen(_player, _database, Feature.AutoBattle) && Campaign.IsCleared(_player, stage.Number) ? from => RepeatStage(from, stage) : null,
			() => ShowCampaign(stage.Number, null));

		/// <summary>O Lutar de um andar de Masmorra: a preparação, com a equipe da Masmorra.</summary>
		private void PrepFloor(DungeonDefinition dungeon, int floor) => ShowPrep(
			T("battle.title_floor", dungeon.Name, floor),
			dungeon.Id,
			dungeon.Floor(floor).Encounter,
			dungeon.Floor(floor).Mana,
			() => FightFloor(dungeon, floor),
			floor <= Dungeons.Cleared(_player, dungeon) ? from => RepeatFloor(from, dungeon, floor) : null,
			() => ShowDungeons(dungeon.Id));

		/// <summary>O Lutar de uma constelação: a preparação, sem Mana, com o desafio da Exploração do mês.</summary>
		private void PrepConstellation(int number)
		{
			var constellation = _database.Exploration.Constellation(number);
			ShowPrep(
				T("battle.title_constellation", number, constellation.Name),
				Teams.Exploration,
				constellation.Encounter(Exploration.VariationOf(DateTime.UtcNow)),
				0,
				() => FightConstellation(number),
				null,
				() => ShowExploration(number));
		}

		private void FightStage(StageDefinition stage)
		{
			// Fase nova vencida: a Campanha volta já na próxima. Fase repetida: volta nela, para farmar.
			var repeat = Campaign.IsCleared(_player, stage.Number);
			void Back() => ShowCampaign(repeat ? stage.Number : null, null);
			if (NeedsTeam(Teams.Campaign, () => PrepStage(stage)) || BusyWithAutoBattle(() => FightStage(stage)))
				return;

			var problem = Campaign.Check(_player, stage);
			if (problem != EntryProblem.None)
			{
				ShowCampaign(stage.Number, Texts.Refusal(problem, stage.Mana));
				return;
			}

			// A primeira vitória da fase avisa o que ela abriu no Santuário (Features).
			var opened = repeat ? null : Features.OpenedBy(_database, stage.Number);
			Fight(T("battle.title_stage", stage.Number, stage.Name), stage.Encounter, Teams.Campaign, Records.StageKey(stage.Number), new BattleRequest { Kind = BattleKind.Stage, Number = stage.Number }, stage.Mana, Back, () => FightStage(stage), opened, NextStage);

			// Vencida, com Mana para a fase seguinte (e vaga para a runa): o Continuar do resultado já entra nela.
			(int Mana, Action Start)? NextStage()
			{
				if (stage.Number >= _database.Stages.Count)
					return null;
				var following = _database.Stage(stage.Number + 1);
				return Campaign.Check(_player, following) == EntryProblem.None ? (following.Mana, () => FightStage(following)) : null;
			}
		}

		private void FightFloor(DungeonDefinition dungeon, int floor)
		{
			void Back() => ShowDungeons(dungeon.Id);
			if (NeedsTeam(dungeon.Id, () => PrepFloor(dungeon, floor)) || BusyWithAutoBattle(() => FightFloor(dungeon, floor)))
				return;

			var problem = Dungeons.Check(_player, dungeon, floor);
			if (problem != EntryProblem.None)
			{
				ShowDungeons(dungeon.Id, Texts.Refusal(problem, dungeon.Floor(floor).Mana));
				return;
			}

			Fight(T("battle.title_floor", dungeon.Name, floor), dungeon.Floor(floor).Encounter, dungeon.Id, Records.FloorKey(dungeon.Id, floor), new BattleRequest { Kind = BattleKind.Floor, Dungeon = dungeon.Id, Floor = floor }, dungeon.Floor(floor).Mana, Back, () => FightFloor(dungeon, floor));
		}

		/// <summary>
		/// Uma constelação da Exploração Estelar, com o desafio da Exploração do mês. A vitória não custa Mana;
		/// a primeira do mês paga a recompensa da constelação, e o Continuar do resultado já entra na seguinte.
		/// </summary>
		private void FightConstellation(int number)
		{
			var exploration = _database.Exploration;
			void Back() => ShowExploration(null);
			if (NeedsTeam(Teams.Exploration, () => PrepConstellation(number)) || BusyWithAutoBattle(() => FightConstellation(number)))
				return;

			if (Exploration.Check(_player, exploration, number, DateTime.UtcNow) != EntryProblem.None)
			{
				ShowExploration(number, T("exploration.locked", number - 1));
				return;
			}

			// O desafio do mês e o recorde dele são os da hora em que o servidor começa a luta.
			var constellation = exploration.Constellation(number);
			Fight(
				T("battle.title_constellation", number, constellation.Name),
				clock => constellation.Encounter(Exploration.VariationOf(clock)),
				Teams.Exploration,
				clock => Records.ConstellationKey(constellation.Id, Exploration.VariationOf(clock)),
				new BattleRequest { Kind = BattleKind.Constellation, Number = number },
				0,
				Back,
				() => FightConstellation(number),
				next: Next);

			(int Mana, Action Start)? Next()
			{
				var following = number + 1;
				return following <= exploration.Constellations.Count && Exploration.Check(_player, exploration, following, DateTime.UtcNow) == EntryProblem.None
					? (0, () => FightConstellation(following))
					: null;
			}
		}

		/// <summary>Sem ninguém na equipe do conteúdo, abre a preparação (<paramref name="prep"/>) em vez da luta.</summary>
		private bool NeedsTeam(string content, Action prep)
		{
			if (Teams.Of(_player, content).Any(id => _player.Monster(id) is { Stored: false }))
				return false;

			prep();
			return true;
		}

		/// <summary>
		/// Uma luta na tela não divide a equipe com a Batalha automática: com uma rodando, pergunta se é
		/// para parar e lutar (<paramref name="then"/>). Verdadeiro quando a luta não começa agora.
		/// </summary>
		private bool BusyWithAutoBattle(Action then)
		{
			if (!_runner.Running)
				return false;

			Dialog.Confirm(_ui, T("auto.busy_title"), T("auto.busy_text"), T("auto.busy_confirm"), () =>
			{
				_runner.Dismiss();
				then();
			}, ButtonKind.Danger);
			return true;
		}

		/// <summary>
		/// A luta na tela: a vitória cobra a Mana, entrega a recompensa e grava o tempo no recorde
		/// <paramref name="record"/>; o resultado mostra a experiência de cada monstro subindo do ponto em que
		/// estava. Volta para <paramref name="back"/>; <paramref name="again"/> é a mesma luta de novo, pela
		/// porta de entrada (confere Mana e equipe). <paramref name="next"/>, depois da vitória, diz se dá para
		/// seguir direto para a luta seguinte (a Mana dela e como entrar).
		/// </summary>
		private void Fight(string title, Encounter encounter, string content, string record, BattleRequest request, int mana, Action back, Action again, IReadOnlyList<Feature>? opened = null, Func<(int Mana, Action Start)?>? next = null) =>
			Fight(title, _ => encounter, content, _ => record, request, mana, back, again, opened, next);

		/// <summary>A mesma luta, com o encontro e o recorde escolhidos pela hora do servidor.</summary>
		/// <param name="encounterAt">O encontro na hora em que o servidor começou a luta (o desafio da Exploração muda com ela).</param>
		/// <param name="recordAt">O recorde nessa mesma hora.</param>
		private async void Fight(string title, Func<DateTime, Encounter> encounterAt, string content, Func<DateTime, string> recordAt, BattleRequest request, int mana, Action back, Action again, IReadOnlyList<Feature>? opened = null, Func<(int Mana, Action Start)?>? next = null)
		{
			if (_fightStarting)
				return;

			_fightStarting = true;
			BattleReport? begun;
			try
			{
				begun = await StartBattle(request);
			}
			finally
			{
				_fightStarting = false;
			}

			if (begun == null)
				return;

			if (begun.Problem != EntryProblem.None)
			{
				if (_screen is { } from && GodotObject.IsInstanceValid(from))
					Dialog.Info(from, T("serverAction.title"), Texts.Refusal(begun.Problem, mana));
				return;
			}

			var encounter = encounterAt(begun.Clock);
			var record = recordAt(begun.Clock);
			var team = PlayerTeam.Build(_player, _database, content);
			var session = BattleFactory.Create(_database, team, encounter, begun.Seed);
			var battle = new BattleScreen(session, title, _player.AutoBattle, focusBoss: _player.FocusBoss);
			var finished = false;
			(int Mana, Action Start)? following = null;
			battle.Finished += async localVictory =>
			{
				finished = true;
				var before = Teams.Of(_player, content)
					.Select(_player.Monster)
					.OfType<OwnedSummon>()
					.Where(m => !m.Stored && _database.HasSummon(m.SummonId))
					.Select(m => (Monster: m, m.Level, m.Experience))
					.ToList();
				var settled = new BattleRequest { Kind = request.Kind, Number = request.Number, Dungeon = request.Dungeon, Floor = request.Floor, Key = request.Key, Ticket = begun.Ticket, Actions = battle.Actions.ToList() };
				var report = await FinishBattle(settled);
				if (report == null)
				{
					back();
					return;
				}

				if (report.Problem != EntryProblem.None)
				{
					if (_screen is { } refused && GodotObject.IsInstanceValid(refused))
						Dialog.Info(refused, T("serverAction.title"), Texts.Refusal(report.Problem, mana));
					back();
					return;
				}

				if (report.Won != localVictory)
					GD.PushWarning($"A luta local deu {(localVictory ? "vitória" : "derrota")} e o servidor decidiu o contrário (semente {begun.Seed}).");

				var victory = report.Won;
				var reward = victory ? report.Reward?.ToReward(_database) : null;
				var newBest = victory && report.NewBest;
				var result = before.Select(b => ResultOf(b.Monster, b.Level, b.Experience)).ToList();
				var tips = victory ? null : DefeatAdvice.For(_player, _database, before.Select(b => b.Monster).ToList(), encounter);
				following = victory ? next?.Invoke() : null;
				battle.ShowResult(new BattleOutcome(victory, reward, result, _player.AccountLevel, Records.Best(_player, record), newBest, tips, victory ? opened : null), following?.Mana, report.Seconds);
			};
			battle.FocusBossChanged += on => Pref(Preferences.FocusBoss, on);
			battle.RuneSellRequested += rune => _ = Act(Request(ServerActionKind.SellRunes, ids: new[] { rune.Id }));
			battle.RuneLockRequested += rune =>
			{
				if (!rune.Locked)
					_ = Act(Request(ServerActionKind.Toggle, rune.Id));
			};
			battle.Closed += auto =>
			{
				Pref(Preferences.AutoBattle, auto);
				back();
			};
			battle.NextRequested += auto =>
			{
				Pref(Preferences.AutoBattle, auto);
				following?.Start();
			};
			battle.RestartRequested += auto =>
			{
				// A Mana só sai na vitória: recomeçar no meio é abrir a mesma luta, com outra semente; depois
				// do fim, é entrar de novo pela porta (que confere a Mana).
				Pref(Preferences.AutoBattle, auto);
				if (finished)
					again();
				else
					Fight(title, encounterAt, content, recordAt, request, mana, back, again, opened, next);
			};
			Swap(battle, back);
		}

		private bool _fightStarting;

		/// <summary>
		/// O começo de uma luta pelo <see cref="GameApi"/>: a semente, o bilhete e a hora do servidor (no automático, também o
		/// resultado e quanto ele leva na tela), ou a porta fechada. Nada é pago aqui. Nulo, com aviso (sem <paramref name="quiet"/>),
		/// se não deu para começar.
		/// </summary>
		private async Task<BattleReport?> StartBattle(BattleRequest request, bool quiet = false) =>
			Answered(await _api.Start(request), quiet);

		/// <summary>
		/// O fim da luta do bilhete pelo <see cref="GameApi"/>: o servidor confere e, na vitória, paga; o que mudou já está na
		/// cópia. Nulo, com aviso (sem <paramref name="quiet"/>), se não deu para terminar.
		/// </summary>
		private async Task<BattleReport?> FinishBattle(BattleRequest request, bool quiet = false) =>
			Answered(await _api.Finish(request), quiet);

		private BattleReport? Answered(BattleCall call, bool quiet)
		{
			if (!call.Ok && !quiet && _screen is { } screen && GodotObject.IsInstanceValid(screen))
				Dialog.Info(screen, T("serverBattle.title"), T("serverBattle." + call.Error));
			return call.Report;
		}

		// Batalha automática ------------------------------------------------------------------------

		/// <summary>
		/// Abre a escolha de quantas lutas e começa a Batalha automática, que segue sozinha fora das telas;
		/// a janela dela abre em seguida (e pode ser fechada sem parar nada). Já havendo uma rodando,
		/// pergunta antes de trocar.
		/// </summary>
		/// <param name="prep">Sem equipe no conteúdo, a preparação abre no lugar.</param>
		private void StartAutoBattle(Control from, string title, string content, Func<EntryProblem> check, int mana, Encounter encounter, BattleRequest request, Action prep)
		{
			if (NeedsTeam(content, prep))
				return;

			void Setup() => AutoBattleSetup.Open(from, title, mana, _player.Mana, runs =>
			{
				var run = new AutoBattleRun(title, content, mana, runs);
				_runner.Start(run, check, async () =>
				{
					// A equipe de antes da luta: o servidor luta com ela ao começar, e a luta que a tela mostra é a mesma.
					var team = PlayerTeam.Build(_player, _database, content);
					var begun = await StartBattle(Auto(request), quiet: true);
					if (begun == null)
						return null;

					if (begun.Problem != EntryProblem.None)
						return new AutoFight(false, 0, null, null, begun.Problem);

					// Só para mostrar: a mesma luta (mesma equipe, mesma semente) para a janela e a tela cheia. A recompensa
					// vem só no fim, depois do tempo que o servidor diz que ela leva na tela.
					var fight = new AutoBattleFight(BattleFactory.Create(_database, team, encounter, begun.Seed), _player.FocusBoss);
					var settled = Auto(request);
					settled.Ticket = begun.Ticket;
					return new AutoFight(begun.Won, begun.Duration, fight, async () =>
					{
						var report = await FinishBattle(settled, quiet: true);
						if (report == null)
							return null;
						var victory = report.Won && report.Problem == EntryProblem.None;
						return new AutoSettled(victory, victory ? report.Reward?.ToReward(_database) : null, report.Problem);
					}, EntryProblem.None);
				}, UiSession.NotifyChanged);
				OpenAutoBattle();
			});

			if (_runner.Running)
				Dialog.Confirm(from, T("auto.replace_title"), T("auto.replace_text", _runner.Run!.Title), T("auto.replace_confirm"), () =>
				{
					_runner.Dismiss();
					Setup();
				}, ButtonKind.Danger);
			else
				Setup();
		}

		/// <summary>Uma cópia do pedido para o automático (o servidor luta sozinho ao começar).</summary>
		private static BattleRequest Auto(BattleRequest request) =>
			new() { Kind = request.Kind, Number = request.Number, Dungeon = request.Dungeon, Floor = request.Floor, Key = request.Key, Auto = true };

		private void RepeatStage(Control from, StageDefinition stage) => StartAutoBattle(
			from,
			T("battle.title_stage", stage.Number, stage.Name),
			Teams.Campaign,
			() => Campaign.Check(_player, stage),
			stage.Mana,
			stage.Encounter,
			new BattleRequest { Kind = BattleKind.Stage, Number = stage.Number },
			() => PrepStage(stage));

		private void RepeatFloor(Control from, DungeonDefinition dungeon, int floor) => StartAutoBattle(
			from,
			T("battle.title_floor", dungeon.Name, floor),
			dungeon.Id,
			() => Dungeons.Check(_player, dungeon, floor),
			dungeon.Floor(floor).Mana,
			dungeon.Floor(floor).Encounter,
			new BattleRequest { Kind = BattleKind.Floor, Dungeon = dungeon.Id, Floor = floor },
			() => PrepFloor(dungeon, floor));

		/// <summary>A janela da Batalha automática, por cima de qualquer tela; fechar não para nada.</summary>
		private void OpenAutoBattle()
		{
			if (_runner.Run is not { } run)
				return;

			var dialog = AutoBattleDialog.Open(_ui, run, _player, new AutoBattleActions
			{
				Stop = _runner.Stop,
				Resume = _runner.Resume,
				Dismiss = _runner.Dismiss,
				SetRuns = _runner.SetRuns,
				SellRune = rune => Do(_ui, Request(ServerActionKind.SellRunes, ids: new[] { rune.Id }), null, () =>
				{
					run.Notify();
					RefreshCurrent();
				}),
				LockRune = rune => Do(_ui, Request(ServerActionKind.Toggle, rune.Id), null, () =>
				{
					run.Notify();
					RefreshCurrent();
				}),
				LockMonster = monster => Do(_ui, new ActionRequest { Kind = ServerActionKind.Toggle, Flag = true, Id = monster.Id, Index = 0 }, null, () =>
				{
					run.Notify();
					RefreshCurrent();
				}),
				UpgradeRune = async (rune, target) =>
				{
					var before = rune.Level;
					await UpgradeRune(rune, target);
					// Com a tela de Runas por baixo, a ficha dela refaz a melhora (e mantém o verde: ver RefreshCurrent).
					(_screen as RuneScreen)?.Upgraded(rune.Id, before);

					// Sem run.Notify(): a janela não se refaz. A ficha e a runa da grade atualizam no lugar
					// (AutoBattleDialog.Sheet), e o que a janela mostra da corrida não muda com a melhora.
					RefreshCurrent();
				},
				ManageRunes = () =>
				{
					if (_screen is not RuneScreen)
						ShowRunes(null, _current);
				},
			});

			// A tela de baixo só é refeita quando a janela fecha (ver RefreshCurrent): remontá-la a cada runa
			// vendida ou melhorada travava a janela no meio da melhora.
			_autoDialog = dialog;
			dialog.Closed += () => Callable.From(() =>
			{
				_autoDialog = null;
				if (!_autoStale)
					return;

				_autoStale = false;
				if (_screen is RuneScreen runes)
					runes.Refresh();
				else if (_screen is not BattleScreen)
					_current();
			}).CallDeferred();
		}

		/// <summary>A janela da Batalha automática, enquanto aberta: a tela de baixo espera por ela para se refazer.</summary>
		private Dialog? _autoDialog;

		/// <summary>A janela da Batalha automática mudou algo que a tela de baixo mostra, e ela ainda não foi refeita.</summary>
		private bool _autoStale;

		/// <summary>
		/// Atualiza a tela de agora quando uma janela por cima mudou algo que ela mostra (moedas, runas). A tela de
		/// Runas só se atualiza: remontá-la perderia a runa escolhida, o filtro e o nível em que cada runa estava
		/// quando ela abriu (o verde do que subiu). As outras ficam para quando a janela fechar: refazê-las na
		/// hora, por baixo de uma janela que também se refaz, é o que dava o solavanco.
		/// </summary>
		private void RefreshCurrent()
		{
			if (_screen is RuneScreen runes)
				runes.Refresh();
			else if (_screen is not BattleScreen)
			{
				if (_autoDialog != null)
					_autoStale = true;
				else
					_current();
			}
		}

		// Infraestrutura ----------------------------------------------------------------------------

		/// <summary>
		/// Um gesto do jogador que muda a conta: o servidor faz (latência por toque), e só com o resultado o jogo toca o som
		/// (<paramref name="done"/>, só se deu certo) e refaz a tela. Recusado: som de indisponível.
		/// </summary>
		private async void Do(Node screen, ActionRequest request, Action<ActionDelta>? done, Action refresh)
		{
			var delta = await Act(request);
			if (!GodotObject.IsInstanceValid(screen))
				return;

			if (delta != null)
				done?.Invoke(delta);
			else
				Sfx.Play("ui.action_unavailable");
			refresh();
		}

		/// <summary>Grava uma escolha do jogador na conta (ver <see cref="Preferences"/>), sem esperar.</summary>
		private void Pref(string key, bool value = false) => _ = Act(new ActionRequest { Kind = ServerActionKind.Preference, Text = key, Flag = value });

		private static ActionRequest Team(TeamOp op, string content, int id = 0, int id2 = 0) =>
			new() { Kind = ServerActionKind.Team, Op = op, Text = content, Id = id, Id2 = id2 };

		private static ActionRequest Request(ServerActionKind kind, int id = 0, IEnumerable<int>? ids = null, bool flag = false, string text = "") =>
			new() { Kind = kind, Id = id, Ids = ids?.ToList() ?? new(), Flag = flag, Text = text };

		private Core.Runes.Rune Rune(int id) => _player.Runes.First(r => r.Id == id);

		/// <summary>Melhora a runa até <paramref name="target"/>; a que chega a +15 o servidor anuncia no Chat global.</summary>
		private async Task UpgradeRune(Core.Runes.Rune rune, int target)
		{
			var delta = await Act(new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = rune.Id, Target = target });
			Sfx.PlayIf(delta != null, "rewards.rune");
		}

		/// <summary>Pedra de Afiar, Gema Encantada ou Gema de Reavaliação numa runa: o servidor faz, e a tela refaz com o resultado.</summary>
		private async void RuneAction(RuneScreen runes, ActionRequest request)
		{
			var delta = await Act(request);
			Sfx.PlayIf(delta != null, "grimoire.magic_ink");
			if (GodotObject.IsInstanceValid(runes))
				runes.Refresh();
		}

		/// <summary>
		/// Uma ação pelo <see cref="GameApi"/> (gasta, rende, sorteia ou organiza): o que mudou já está na cópia. Sem conexão ou
		/// com a cópia velha, avisa; recusada, só o som de quem chamou. Nulo se não foi feita.
		/// </summary>
		private async Task<ActionDelta?> Act(ActionRequest request)
		{
			var outcome = await _api.Act(request);
			if (!outcome.Ok && outcome.Error != "refused" && _screen is { } screen && GodotObject.IsInstanceValid(screen))
				Dialog.Info(screen, T("serverAction.title"), T("serverAction." + outcome.Error));
			return outcome.Delta;
		}

		/// <summary>Um monstro no resultado da luta: o retrato e a barra de experiência do antes até agora.</summary>
		private ResultMonster ResultOf(OwnedSummon monster, int level, int experience)
		{
			var summon = _database.Summon(monster.SummonId);
			return new ResultMonster(summon.NameFor(monster.Awakened), Art.Creature(summon.Image), Palette.Of(summon.Element), ResultMonster.StepsOf(monster, level, experience))
			{
				MaxLevel = Leveling.IsMaxLevel(monster),
				Summon = summon,
				Monster = monster,
			};
		}

		/// <summary>
		/// Uma coleta de lixo completa, em segundo plano. Cada objeto do Godot criado pelo C# (estilo, tween,
		/// temporizador) fica vivo até o coletor soltar o lado C# dele, e o coletor quase não faz a coleta
		/// completa: a memória do C# é pequena, então ele não vê pressa. Numa luta, que refaz ícones e números a
		/// cada ação, isso juntava milhares de objetos por minuto e a memória crescia sem parar.
		/// </summary>
		private void Collect()
		{
			// A coleta solta de uma vez os objetos do Godot que ela encontra e pode dar um solavanco de alguns
			// quadros: com o dedo na tela (rolando, tocando) espera um pouco, até <see cref="CollectPatience"/>.
			if (Time.GetTicksMsec() - _lastTouch < TouchRestMs && _postponed < CollectPatience)
			{
				_postponed++;
				_collector.Start(1);
				return;
			}

			_postponed = 0;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);
			_collector.Start(CollectSeconds);
		}

		/// <summary>Quantas esperas de 1 s a coleta aguenta, no máximo, antes de passar mesmo com o dedo na tela.</summary>
		private const int CollectPatience = 8;

		/// <summary>Quanto tempo, em ms, sem toque conta como "o jogador parou".</summary>
		private const ulong TouchRestMs = 1200;

		private Timer _collector = null!;
		private int _postponed;
		private ulong _lastTouch;

		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventScreenTouch or InputEventScreenDrag or InputEventMouseButton or InputEventMouseMotion { ButtonMask: not 0 })
				_lastTouch = Time.GetTicksMsec();
		}

		/// <summary>Grava a cópia aberta no aparelho. Nada sobe: na conta, o servidor já tem tudo.</summary>
		private void Save()
		{
			if (_playing)
				_store.Save(_player);
		}

		/// <summary>
		/// Troca a tela. A nova leva o nome da classe (<c>RuneScreen</c>): é a raiz do caminho de todo nó
		/// dela. <paramref name="reshow"/> é como remontá-la (para voltar a ela depois de uma janela).
		/// </summary>
		private void Swap(Control screen, Action reshow)
		{
			if (_screen != null)
				Layout.Discard(_screen);
			_screen = screen;
			_current = reshow;
			screen.Name = screen.GetType().Name;
			_screens.AddChild(screen);
			Sfx.Fallback("ui.panel_open", 1);
			_bubble.Follow(screen);
			// A Batalha toca na tela de luta; todo o resto é o Plano Celestial (a Batalha automática não tem tela).
			_music.Play(screen is BattleScreen ? MusicPlayer.Track.Battle : MusicPlayer.Track.Celestial);
		}

		/// <summary>
		/// A colagem das fichas do catálogo (<see cref="MonsterSummary"/>, nas estrelas naturais, nível 1) de todas as
		/// variantes da família <paramref name="family"/> (o prefixo do id: "lich" para lich_fire, lich_water...), lado a lado,
		/// salva em <paramref name="path"/> (PNG). Sai do jogo no fim. Use com -- --save=debugger.
		/// </summary>
		private async void SummonSheet(string family, string path)
		{
			for (var i = 0; i < 90; i++)
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var order = new[] { Element.Neutral, Element.Fire, Element.Water, Element.Wind, Element.Light, Element.Dark };
			var variants = _database.Summons.Where(s => s.Id.StartsWith(family + "_", StringComparison.Ordinal)).OrderBy(s => Array.IndexOf(order, s.Element)).ToList();
			var shots = new List<(Image Image, Rect2I Crop)>();
			foreach (var variant in variants)
			{
				var dialog = MonsterSummary.Open(_ui, variant, null);
				for (var i = 0; i < 40; i++)
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				var image = GetViewport().GetTexture().GetImage();
				var panel = dialog.Body.GetParent().GetParent().GetParent() as Control;
				var box = panel?.GetGlobalRect() ?? new Rect2(Vector2.Zero, image.GetSize());
				var crop = new Rect2I((int)box.Position.X - 8, (int)box.Position.Y - 8, (int)box.Size.X + 16, (int)box.Size.Y + 16).Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
				shots.Add((image, crop));
				dialog.Close();
				for (var i = 0; i < 30; i++)
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}

			if (shots.Count > 0)
			{
				var width = shots.Sum(shot => shot.Crop.Size.X);
				var height = shots.Max(shot => shot.Crop.Size.Y);
				var sheet = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
				sheet.Fill(new Color(0.05f, 0.04f, 0.09f));
				var x = 0;
				foreach (var (image, crop) in shots)
				{
					sheet.BlitRect(image, crop, new Vector2I(x, 0));
					x += crop.Size.X;
				}

				sheet.SavePng(path);
				GD.Print($"SHEET {path} ({variants.Count} variantes)");
			}
			else
			{
				GD.PushError($"--summon-sheet: nenhuma variante com o prefixo \"{family}_\".");
			}

			GetTree().Quit();
		}

		/// <summary>Uma linha por habilidade (e por versão desperta): id, nome e a descrição lida na tela.</summary>
		private void DumpSkills(string path)
		{
			// -- --level=N: os números no nível N do aprimoramento (os que subiram, em dourado).
			var level = int.TryParse(Argument("--level="), out var asked) ? asked : 1;
			var lines = new List<string>();
			foreach (var summon in _database.Summons)
			{
				foreach (var skill in summon.Skills)
				{
					lines.Add($"{summon.Id} | {skill.Name} | {Texts.Describe(skill, false, level).Replace("\n", " // ")}");
					if (skill.AwakenedEffects.Count > 0 || skill.Passive is { AwakenedValue: > 0 })
						lines.Add($"{summon.Id} | {skill.Name} (desperta) | {Texts.Describe(skill, true, level)}");
				}
			}

			foreach (var enemy in _database.Enemies)
			{
				foreach (var skill in enemy.Skills)
					lines.Add($"inimigo {enemy.Id} | {skill.Name} | {Texts.Describe(skill).Replace("\n", " // ")}");
			}

			foreach (var constellation in _database.Exploration.Constellations)
			{
				foreach (var rule in constellation.Influence.Rules)
					lines.Add($"influência {constellation.Id} | {Texts.Name(rule.Side)} | {Texts.Describe(rule.Prepared, false)}");
			}

			System.IO.File.WriteAllLines(path, lines);
		}

		private static string? Argument(string prefix) => OS.GetCmdlineUserArgs()
			.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
	}
}
