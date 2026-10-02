using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Summoning;
using Sigilos.GameEntry.Account;
using Sigilos.GameEntry.Update;
using Sigilos.UI;
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
	/// barra de baixo, a Monstros, Runas, Equipes, Loja, Grimório, Compêndio e Ajustes; cada tela volta
	/// para onde veio (seta, Esc ou o Voltar do celular). A Batalha automática roda fora das telas
	/// (<see cref="AutoBattleRunner"/>), com o aviso flutuante no alto de todas.
	///
	/// Antes do Santuário vem a conta (<see cref="AccountSession"/>, docs/SAVE_NUVEM.md): a tela de login
	/// (Entrar, Criar conta, Jogar sem conta), a pergunta "desconectar o outro aparelho?" e, quando o
	/// aparelho e a nuvem mudaram, "qual progresso usar?". Quem escolheu jogar sem conta abre direto no
	/// save local; quem tem conta lembrada entra sozinho. Outro aparelho entrando derruba este de volta
	/// para o login.
	///
	/// O executável do Windows procura uma versão nova ao abrir e, se o jogador aceitar, se troca por ela
	/// (<see cref="Updater"/>, docs/ATUALIZACOES.md).
	///
	/// Argumentos de desenvolvimento (depois de <c>--</c>): <c>--save=nome</c> usa outros arquivos de
	/// save e de conta (outro "aparelho"); <c>--language=nome</c> usa Data/texts/nome.json (padrão: o do
	/// save, ou pt-BR); <c>--server=url</c> usa outro servidor de contas; <c>--updates=url</c> procura
	/// versões novas em outra pasta (só no executável exportado);
	/// <c>--screen=map|campaign|dungeons|summon|shop|monsters|teams|runes|compendium|grimoire|battle</c> abre essa tela direto, no save sem conta.
	/// </summary>
	public partial class GameRoot : Node
	{
		private const string DefaultSlot = "sigilos";

		/// <summary>De quanto em quanto tempo a coleta de lixo completa passa (<see cref="Collect"/>).</summary>
		private const double CollectSeconds = 10;

		private readonly Random _random = new();
		private readonly AutoBattleRunner _runner = new();
		private readonly AutoBattleBadge _badge = new();
		private Control _ui = null!;
		private Control _screens = null!;
		private Control? _screen;
		private GameDatabase _database = null!;
		private AccountSession _account = null!;
		private SaveStore _store = null!;
		private PlayerState _player = null!;
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

		/// <summary>O download da versão nova em andamento (Cancelar e fechar o jogo param ele).</summary>
		private System.Threading.CancellationTokenSource? _updateDownload;

		/// <summary>Remonta a tela de agora (depois de uma janela que mudou algo, ou para voltar a ela).</summary>
		private Action _current = () => { };

		// Para onde cada tela de conteúdo volta: vale também depois de uma luta ou da Loja.
		private Action _campaignBack = null!;
		private Action _dungeonsBack = null!;
		private Action _storageBack = null!;
		private Action _summonBack = null!;

		public override void _Ready()
		{
			_campaignBack = _dungeonsBack = ShowMap;
			_storageBack = _summonBack = ShowHub;
			_account = new AccountSession(Argument("--save=") ?? DefaultSlot, Argument("--server=") ?? AccountSession.DefaultServer) { Name = "Account" };
			AddChild(_account);
			var collector = new Timer { Name = "Collector", WaitTime = CollectSeconds, Autostart = true };
			collector.Timeout += Collect;
			AddChild(collector);
			_account.Lost += (reason, backedUp) => ShowLogin(T(reason switch
			{
				LossReason.Expired => "account.error_expired",
				LossReason.TakenWhileAway => "account.lost_away",
				_ => backedUp ? "account.lost_taken_backup" : "account.lost_taken",
			}));
			_account.SyncNeeded += Resync;
			_account.ConnectionChanged += () =>
			{
				if (_screen is HubScreen hub)
					hub.SetOffline(!_account.Connected);
				RefreshMail();
			};

			// Os textos vêm antes dos dados: os nomes dos dados saem no idioma deles. Antes de abrir um save
			// (a tela de login), vale o idioma do último jogo deste aparelho.
			_language = Argument("--language=") ?? _account.Language ?? _account.OfflineStore.Load()?.Language ?? ContentLoader.BaseLanguage;
			ContentLoader.LoadTexts(_language);
			_database = ContentLoader.Load();
			UiSession.Database = _database;

			// O Voltar do celular vira Esc (ui_cancel): fecha a janela de cima ou volta de tela.
			GetTree().QuitOnGoBack = false;
			// Fechar a janela espera o save subir para a nuvem (Quit).
			GetTree().AutoAcceptQuit = false;

			_ui = new Control { Name = "UI", Theme = GameTheme.Build() };
			_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			AddChild(_ui);
			_screens = new Control { Name = "Screens", MouseFilter = Control.MouseFilterEnum.Ignore };
			_screens.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			_ui.AddChild(_screens);
			_ui.AddChild(_badge);
			_badge.Pressed += OpenAutoBattle;
			AddChild(_runner);
			_runner.RunChanged += run => _badge.Show(run);
			// No PC, a janela fica sempre em 16:9.
			AddChild(new WindowAspect());

			// Com --screen (desenvolvimento), ou depois de escolher jogar sem conta, abre direto o save local.
			var screen = Argument("--screen=");
			if (screen != null || _account.Offline)
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
				// O celular pode matar o jogo em segundo plano: grava e já manda para a nuvem.
				Save();
				if (_account.Playing)
					_ = _account.Flush();
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
			_playing = true;
			UiSession.Player = player;
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
				case "summon":
					Go(Destination.Summon, ShowHub);
					break;
				case "shop":
					Go(Destination.Shop, ShowHub);
					break;
				case "monsters":
					Go(Destination.Monsters, ShowHub);
					break;
				case "teams":
					Go(Destination.Teams, ShowHub);
					break;
				case "runes":
					ShowRunes(Teams.Of(_player, Teams.Campaign).FirstOrDefault(), ShowHub);
					break;
				case "compendium":
					Go(Destination.Compendium, ShowHub);
					break;
				case "grimoire":
					Go(Destination.Grimoire, ShowHub);
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
			Play(store, store.Load() ?? NewPlayer(), screen);
		}

		private PlayerState NewPlayer() => NewGame.Create(DateTime.Now, _random, _database);

		/// <summary>
		/// A tela de login. <paramref name="message"/> diz por que o jogo voltou para ela (outro aparelho
		/// entrou, o acesso venceu); com <paramref name="resume"/>, já entra com a conta lembrada.
		/// </summary>
		private void ShowLogin(string? message = null, bool resume = false)
		{
			_playing = false;
			_runner.Dismiss();
			CloseDialogs();
			var login = new LoginScreen(_account.Email, _account.AccountName, _account.Remembered);
			login.LoginRequested += (email, password) => SignIn(login, () => _account.Login(email, password), false);
			login.RegisterRequested += (email, password, invite, name) => SignIn(login, () => _account.Register(email, password, invite, name), false);
			login.ResumeRequested += () => SignIn(login, _account.Resume, true);
			login.ForgetRequested += _account.Forget;
			login.OfflineRequested += () =>
			{
				_account.Offline = true;
				PlayOffline();
			};
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

			if (resume && response.Unreached && PlayAccountOffline())
				return;

			login.ShowMessage(AccountError(response, !resume));
			if (!_account.Remembered)
				login.ShowForm();
		}

		/// <summary>
		/// Sem internet, a conta lembrada abre com o save deste aparelho; o batimento tenta reconectar e, quando
		/// conseguir, sincroniza. Falso se este aparelho não tem save desta conta (nunca entrou nela aqui).
		/// </summary>
		private bool PlayAccountOffline()
		{
			if (_account.Store is not { } store || store.Load() is not { } player)
				return false;

			_account.Begin(() => _playing ? _player : null, connected: false);
			Play(store, player);
			return true;
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

			if (claim.Outcome == ClaimOutcome.Failed)
			{
				if (claim.Response.Unreached && PlayAccountOffline())
					return;

				login.ShowMessage(AccountError(claim.Response));
				if (!_account.Remembered)
					login.ShowForm();
				return;
			}

			login.SetBusy(T("account.syncing"));
			var sync = await _account.Sync(NewPlayer, AskConflict);
			if (_screen != login)
				return;

			if (!sync.Ok)
			{
				if (sync.Error!.Unreached && PlayAccountOffline())
					return;

				login.ShowMessage(AccountError(sync.Error!));
				return;
			}

			// A conta começa antes do Santuário abrir: ele já sai com o nome dela.
			_account.Begin(() => _playing ? _player : null);
			Play(_account.Store!, sync.Player!);
			if (_account.AccountName == null)
				ChooseName(true);
		}

		/// <summary>"Qual progresso usar?": verdadeiro fica o da nuvem.</summary>
		private Task<bool> AskConflict(SaveConflict conflict)
		{
			var choice = new TaskCompletionSource<bool>();
			SaveConflictDialog.Open(_ui, conflict.Local, conflict.LocalSavedAt, conflict.Cloud, conflict.CloudSavedAt, conflict.Adopting, cloud => choice.TrySetResult(cloud));
			return choice.Task;
		}

		/// <summary>
		/// A nuvem mudou por fora durante o jogo, ou a conexão voltou depois de jogar sem ela: sincroniza de novo
		/// (perguntando, se os dois lados mudaram) e, se ficar o da nuvem, reabre o Santuário com ele.
		/// </summary>
		private async void Resync()
		{
			if (_resyncing)
				return;

			_resyncing = true;
			Save();
			var sync = await _account.Sync(NewPlayer, AskConflict);
			_resyncing = false;
			if (!_playing || !sync.Downloaded)
				return;

			_runner.Dismiss();
			CloseDialogs();
			Play(_account.Store!, sync.Player!);
		}

		/// <summary>Ajustes → Sair da conta: envia o que falta (perguntando, se não der), solta a sessão e volta para o login.</summary>
		private async void SignOut()
		{
			var wait = Wait(T("account.leaving"));
			var flushed = await _account.Flush();
			wait.Close();
			if (flushed)
				Leave();
			else
				Dialog.Confirm(_ui, T("account.signout_offline_title"), T("account.signout_offline_text"), T("config.sign_out"), Leave, ButtonKind.Danger);
		}

		private async void Leave()
		{
			_playing = false;
			Wait(T("account.leaving"));
			await _account.Leave(true);
			ShowLogin();
		}

		/// <summary>Ajustes → Entrar ou criar conta, saindo do jogo sem conta (que sobe para a conta, se ela for nova).</summary>
		private void LeaveOffline()
		{
			Save();
			_account.Offline = false;
			ShowLogin();
		}

		/// <summary>
		/// A janela do nome da conta: trocar (Ajustes) ou, com <paramref name="prompt"/>, pedir um à conta que
		/// ainda não tem. Com o nome aceito, o Santuário se remonta com ele.
		/// </summary>
		private void ChooseName(bool prompt)
		{
			var dialog = AccountNameDialog.Open(_ui, _account.AccountName, prompt);
			dialog.Submitted += async name =>
			{
				dialog.SetBusy();
				var response = await _account.Rename(name);
				if (!response.Ok)
				{
					dialog.ShowError(AccountError(response));
					return;
				}

				dialog.Close();
				if (_screen is HubScreen)
					_current();
			};
		}

		/// <summary>A conta nos Ajustes.</summary>
		private ConfigAccount AccountSettings()
		{
			if (!_account.Playing)
				return new ConfigAccount(null, null, "", LeaveOffline, SignOut, () => { });

			var status = !_account.Connected ? T("config.offline_account")
				: _account.Pending ? T("config.pending")
				: _account.LastSync is { } at ? T("config.synced", at.ToLocalTime().ToString("t", Culture))
				: "";
			return new ConfigAccount(_account.Email, _account.AccountName, status, LeaveOffline, SignOut, () => ChooseName(false));
		}

		/// <summary>Fechar a janela: grava, envia o que falta e solta a sessão (sem esperar o servidor mais que uns segundos).</summary>
		private async void Quit()
		{
			if (_quitting)
				return;
			_quitting = true;
			_updateDownload?.Cancel();
			Save();
			if (_account.Playing)
				await Task.WhenAny(FlushAndLeave(), Task.Delay(TimeSpan.FromSeconds(5)));
			GetTree().Quit();
		}

		private async Task FlushAndLeave()
		{
			await _account.Flush();
			await _account.Leave(false);
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
			{ Error: "invalid_invite" } => T("account.error_invalid_invite"),
			{ Error: "invalid_registration" } => T("account.error_registration"),
			{ Error: "invalid_name" } => T("account.error_name", AccountNameDialog.MinLength, AccountNameDialog.MaxLength),
			{ Error: "name_taken" } => T("account.error_name_taken"),
			{ Error: "cloud_unreadable" } => T("account.error_cloud"),
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
		/// Instala o executável baixado: grava, envia o que falta e solta a sessão, como ao fechar, e só então
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
				await Task.WhenAny(FlushAndLeave(), Task.Delay(TimeSpan.FromSeconds(5)));

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
			var hub = new HubScreen(_database, _player, _account.Playing ? _account.AccountName : null, _account.Playing && !_account.Connected);
			hub.Requested += destination => Go(destination, ShowHub);
			hub.ConfigRequested += () => ConfigPanel.Open(hub, ContentLoader.Languages(), _language, language =>
			{
				_player.Language = language;
				Save();
				UseLanguage(language);
				ShowHub();
			}, AccountSettings(), ShowTutorial);
			hub.CollectRequested += () => Change(() => Idle.Collect(_player, DateTime.Now), () => hub.Refresh(DateTime.Now));
			hub.MailRequested += () => OpenMailbox(hub);
			Swap(hub, ShowHub);
			hub.SetMail(_mail?.Count);
			RefreshMail();
		}

		// Correio ------------------------------------------------------------------------------------

		/// <summary>
		/// Busca as cartas da conta (a cada volta ao Santuário e quando a conexão volta) e atualiza o selo e a
		/// janela aberta. A carta que este save já coletou, mas o servidor ainda não sabe (a conexão caiu no
		/// meio), não aparece: o aviso ao servidor vai de novo.
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

			foreach (var claimed in fetch.Mail.Where(m => _player.ClaimedMail.Contains(m.Id)))
				_ = _account.ClaimMail(claimed.Id);
			_mail = Mailbox.Unclaimed(_player, fetch.Mail);
			(_screen as HubScreen)?.SetMail(_mail.Count);
			_mailbox?.Show(_mail);
		}

		/// <summary>A janela do correio: as cartas já buscadas na hora e, depois, as da busca nova.</summary>
		private void OpenMailbox(HubScreen hub)
		{
			var mailbox = MailboxDialog.Open(hub);
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
		/// Coleta: a recompensa entra no save e o save é gravado antes de o servidor saber, então a conexão
		/// caindo no meio não perde nada (o aviso vai de novo na próxima busca).
		/// </summary>
		private void ClaimMail(IEnumerable<Mail> letters)
		{
			var claimed = letters.Where(mail => Mailbox.Claim(_player, mail)).ToList();
			if (claimed.Count == 0)
				return;

			Save();
			foreach (var mail in claimed)
				_ = _account.ClaimMail(mail.Id);
			_mail = Mailbox.Unclaimed(_player, _mail ?? Array.Empty<Mail>());
			if (_screen is HubScreen hub)
			{
				hub.Refresh(DateTime.Now);
				hub.SetMail(_mail.Count);
			}

			_mailbox?.Show(_mail);
		}

		/// <summary>
		/// A luta de treino (<see cref="Tutorial"/>), com o Mestre ensinando: no fim, ou ao sair pela pausa,
		/// conta como feita (não abre mais sozinha) e leva ao Santuário. Não cobra nem dá nada.
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
				_player.TutorialDone = true;
				Save();
				ShowHub();
			}

			battle.Finished += async _ =>
			{
				await coach.End();
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
				case Destination.Teams:
					ShowTeams(Teams.Campaign, back);
					break;
				case Destination.Shop:
					ShowShop(back);
					break;
				case Destination.Compendium:
					ShowCompendium(back);
					break;
				case Destination.Grimoire:
					ShowGrimoire(back);
					break;
				case Destination.Map:
					ShowMap();
					break;
			}
		}

		/// <summary>A Campanha na fase <paramref name="selected"/> (nula: a próxima a vencer), com um aviso embaixo.</summary>
		private void ShowCampaign(int? selected, string? message)
		{
			var campaign = new CampaignScreen(_database, _player, selected);
			campaign.BackRequested += () => _campaignBack();
			campaign.FightRequested += FightStage;
			campaign.TeamRequested += () => ShowTeams(Teams.Campaign, () => ShowCampaign(campaign.Selected, null));
			campaign.ShopRequested += () => ShowShop(() => ShowCampaign(campaign.Selected, null));
			campaign.RepeatRequested += stage => StartAutoBattle(
				campaign,
				T("battle.title_stage", stage.Number, stage.Name),
				Teams.Campaign,
				() => Campaign.Check(_player, stage),
				stage.Mana,
				stage.Encounter,
				() => Campaign.ApplyVictory(_random, _player, stage, _database),
				() => ShowCampaign(stage.Number, null));
			Swap(campaign, () => ShowCampaign(campaign.Selected, null));
			if (message != null)
				campaign.ShowMessage(message);
		}

		private void ShowDungeons(string? selected, string? message = null)
		{
			var dungeons = new DungeonScreen(_database, _player, selected);
			dungeons.BackRequested += () => _dungeonsBack();
			dungeons.FightRequested += FightFloor;
			dungeons.TeamRequested += dungeon => ShowTeams(dungeon.Id, () => ShowDungeons(dungeon.Id));
			dungeons.ShopRequested += dungeon => ShowShop(() => ShowDungeons(dungeon.Id));
			dungeons.RepeatRequested += (dungeon, floor) => StartAutoBattle(
				dungeons,
				T("battle.title_floor", dungeon.Name, floor),
				dungeon.Id,
				() => Dungeons.Check(_player, dungeon, floor),
				dungeon.Floor(floor).Mana,
				dungeon.Floor(floor).Encounter,
				() => Dungeons.ApplyVictory(_random, _player, dungeon, floor),
				() => ShowDungeons(dungeon.Id));
			Swap(dungeons, () => ShowDungeons(selected));
			if (message != null)
				dungeons.ShowMessage(message);
		}

		private void ShowSummon()
		{
			var summon = new SummonScreen(_database, _player);
			summon.BackRequested += () => _summonBack();
			summon.ShopRequested += () => ShowShop(ShowSummon);
			summon.MonstersRequested += () =>
			{
				_storageBack = ShowSummon;
				ShowStorage(null);
			};
			summon.SummonRequested += count =>
			{
				var results = SummonRitual.Perform(_random, _database, _player, count);
				if (results.Count == 0)
					return;

				Teams.FillCampaign(_player, results.Select(r => r.Monster).OrderByDescending(m => _database.Summon(m.SummonId).Rarity));
				Save();
				// O gasto de Pergaminhos e os monstros sobem para a nuvem antes de o resultado aparecer: fechar
				// o jogo na hora não desfaz a invocação. O envio corre durante o ritual.
				summon.ShowResults(results, _account.Connected ? _account.Flush() : null);
			};
			Swap(summon, ShowSummon);
		}

		private void ShowStorage(int? selected)
		{
			var storage = new StorageScreen(_database, _player, selected);
			storage.BackRequested += () => _storageBack();
			storage.RunesRequested += id => ShowRunes(id, () => ShowStorage(id));
			storage.InfuseRequested += (id, toMax) => Change(() =>
			{
				var monster = _player.Monster(id)!;
				Leveling.Infuse(_player, monster, toMax ? int.MaxValue : Leveling.InfuseCosts(monster).Next);
			}, storage.Refresh);
			storage.AwakenRequested += id => Change(() => Awakening.Awaken(_player, _player.Monster(id)!, _database.Summon(_player.Monster(id)!.SummonId)), storage.Refresh);
			storage.EvolveRequested += id => Change(() => Evolution.Evolve(_player, _player.Monster(id)!), storage.Refresh);
			storage.StoreRequested += id => Change(() => Roster.Store(_player, id), storage.Refresh);
			storage.RetrieveRequested += id => Change(() => Roster.Retrieve(_player, id), storage.Refresh);
			storage.StoreManyRequested += ids => Change(() => Roster.StoreMany(_player, ids), storage.Refresh);
			storage.RetrieveManyRequested += ids => Change(() => Roster.RetrieveMany(_player, ids), storage.Refresh);
			storage.FuseRequested += (target, materials) => Change(() => Fusion.FuseMany(_random, _player, _database, target, materials), storage.Refresh);
			storage.ReleaseRequested += ids => Change(() => Fusion.ReleaseMany(_player, _database, ids), storage.Refresh);
			storage.LockRequested += id => Change(() => _player.Monster(id)!.Locked = !_player.Monster(id)!.Locked, storage.Refresh);
			storage.FavoriteRequested += id => Change(() => _player.Monster(id)!.Favorite = !_player.Monster(id)!.Favorite, storage.Refresh);
			Swap(storage, () => ShowStorage(selected));
		}

		private void ShowShop(Action back)
		{
			var shop = new ShopScreen(_database, _player);
			shop.BackRequested += back;
			shop.BuyRequested += offer => Change(() =>
			{
				if (Shop.Buy(_player, offer))
					shop.ShowMessage(T("shop.bought", Texts.Amount(offer.Item, offer.Amount)));
			}, shop.Refresh);
			Swap(shop, () => ShowShop(back));
		}

		private void ShowTeams(string content, Action back)
		{
			var teams = new TeamScreen(_database, _player, content);
			teams.BackRequested += back;
			teams.ToggleRequested += (key, id) => Change(() => Teams.Toggle(_player, key, id), teams.Refresh);
			teams.LeaderRequested += (key, id) => Change(() => Teams.MakeLeader(_player, key, id), teams.Refresh);
			Swap(teams, () => ShowTeams(content, back));
		}

		private void ShowRunes(int? monsterId, Action back)
		{
			var runes = new RuneScreen(_database, _player, monsterId);
			runes.BackRequested += _ => back();
			runes.EquipRequested += (id, monster) => Change(() => RuneInventory.Equip(_player, Rune(id), monster), runes.Refresh);
			runes.UnequipRequested += id => Change(() => RuneInventory.Unequip(_player, Rune(id)), runes.Refresh);
			runes.UpgradeRequested += (id, target) => Change(() => RuneInventory.Upgrade(_random, _player, Rune(id), target), runes.Refresh);
			runes.GrindRequested += (id, index, tool) => Change(() => RuneInventory.Grind(_random, _player, Rune(id), index, tool), runes.Refresh);
			runes.EnchantRequested += (id, index, tool) => Change(() => RuneInventory.Enchant(_random, _player, Rune(id), index, tool), runes.Refresh);
			runes.SellRequested += id => Change(() => RuneInventory.Sell(_player, Rune(id)), runes.Refresh);
			runes.SellManyRequested += ids => Change(() => RuneInventory.SellAll(_player, _player.Runes.Where(r => ids.Contains(r.Id))), runes.Refresh);
			runes.LockRequested += id => Change(() => Rune(id).Locked = !Rune(id).Locked, runes.Refresh);
			runes.ReappraiseRequested += id => Change(() => Core.Runes.RuneReappraisal.Reappraise(_player, Rune(id)), runes.Refresh);
			Swap(runes, () => ShowRunes(monsterId, back));
		}

		private void ShowCompendium(Action back)
		{
			var compendium = new CompendiumScreen();
			compendium.BackRequested += back;
			Swap(compendium, () => ShowCompendium(back));
		}

		private void ShowGrimoire(Action back)
		{
			var grimoire = new GrimoireScreen(_database, _player);
			grimoire.BackRequested += back;
			Swap(grimoire, () => ShowGrimoire(back));
		}

		// Lutas -------------------------------------------------------------------------------------

		private void FightStage(StageDefinition stage)
		{
			// Fase nova vencida: a Campanha volta já na próxima. Fase repetida: volta nela, para farmar.
			var repeat = Campaign.IsCleared(_player, stage.Number);
			void Back() => ShowCampaign(repeat ? stage.Number : null, null);
			if (NeedsTeam(Teams.Campaign, Back) || BusyWithAutoBattle(() => FightStage(stage)))
				return;

			var problem = Campaign.Check(_player, stage);
			if (problem != EntryProblem.None)
			{
				ShowCampaign(stage.Number, Texts.Refusal(problem, stage.Mana));
				return;
			}

			// A primeira vitória da fase avisa o que ela abriu no Santuário (Features).
			var opened = repeat ? null : Features.OpenedBy(_database, stage.Number);
			Fight(T("battle.title_stage", stage.Number, stage.Name), stage.Encounter, Teams.Campaign, Records.StageKey(stage.Number), () => Campaign.ApplyVictory(_random, _player, stage, _database), Back, () => FightStage(stage), opened, NextStage);

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
			if (NeedsTeam(dungeon.Id, Back) || BusyWithAutoBattle(() => FightFloor(dungeon, floor)))
				return;

			var problem = Dungeons.Check(_player, dungeon, floor);
			if (problem != EntryProblem.None)
			{
				ShowDungeons(dungeon.Id, Texts.Refusal(problem, dungeon.Floor(floor).Mana));
				return;
			}

			Fight(T("battle.title_floor", dungeon.Name, floor), dungeon.Floor(floor).Encounter, dungeon.Id, Records.FloorKey(dungeon.Id, floor), () => Dungeons.ApplyVictory(_random, _player, dungeon, floor), Back, () => FightFloor(dungeon, floor));
		}

		/// <summary>Sem ninguém na equipe do conteúdo, abre a tela de Equipes em vez da luta.</summary>
		private bool NeedsTeam(string content, Action back)
		{
			if (Teams.Of(_player, content).Any(id => _player.Monster(id) is { Stored: false }))
				return false;

			ShowTeams(content, back);
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
		private void Fight(string title, Encounter encounter, string content, string record, Func<VictoryReward> victoryReward, Action back, Action again, IReadOnlyList<Feature>? opened = null, Func<(int Mana, Action Start)?>? next = null)
		{
			Save();
			var team = PlayerTeam.Build(_player, _database, content);
			var session = BattleFactory.Create(_database, team, encounter, _random.Next());
			var battle = new BattleScreen(session, title, _player.AutoBattle, focusBoss: _player.FocusBoss);
			var finished = false;
			(int Mana, Action Start)? following = null;
			battle.Finished += victory =>
			{
				finished = true;
				var before = Teams.Of(_player, content)
					.Select(_player.Monster)
					.OfType<OwnedSummon>()
					.Where(m => !m.Stored && _database.HasSummon(m.SummonId))
					.Select(m => (Monster: m, m.Level, m.Experience))
					.ToList();
				var reward = victory ? victoryReward() : null;
				var newBest = victory && Records.Submit(_player, record, battle.Elapsed);
				Save();
				var result = before.Select(b => ResultOf(b.Monster, b.Level, b.Experience)).ToList();
				var tips = victory ? null : DefeatAdvice.For(_player, _database, before.Select(b => b.Monster).ToList(), encounter);
				following = victory ? next?.Invoke() : null;
				battle.ShowResult(new BattleOutcome(victory, reward, result, _player.AccountLevel, Records.Best(_player, record), newBest, tips, victory ? opened : null), following?.Mana);
			};
			battle.FocusBossChanged += on =>
			{
				_player.FocusBoss = on;
				Save();
			};
			battle.RuneSellRequested += rune =>
			{
				RuneInventory.Sell(_player, rune);
				Save();
			};
			battle.RuneLockRequested += rune =>
			{
				rune.Locked = true;
				Save();
			};
			battle.Closed += auto =>
			{
				_player.AutoBattle = auto;
				Save();
				back();
			};
			battle.NextRequested += auto =>
			{
				_player.AutoBattle = auto;
				Save();
				following?.Start();
			};
			battle.RestartRequested += auto =>
			{
				// A Mana só sai na vitória: recomeçar no meio é abrir a mesma luta, com outra semente; depois
				// do fim, é entrar de novo pela porta (que confere a Mana).
				_player.AutoBattle = auto;
				if (finished)
					again();
				else
					Fight(title, encounter, content, record, victoryReward, back, again, opened, next);
			};
			Swap(battle, back);
		}

		// Batalha automática ------------------------------------------------------------------------

		/// <summary>
		/// Abre a escolha de quantas lutas e começa a Batalha automática, que segue sozinha fora das telas;
		/// a janela dela abre em seguida (e pode ser fechada sem parar nada). Já havendo uma rodando,
		/// pergunta antes de trocar.
		/// </summary>
		private void StartAutoBattle(Control from, string title, string content, Func<EntryProblem> check, int mana, Encounter encounter, Func<VictoryReward> victory, Action back)
		{
			if (NeedsTeam(content, back))
				return;

			void Setup() => AutoBattleSetup.Open(from, title, mana, _player.Mana, runs =>
			{
				var run = new AutoBattleRun(title, content, mana, runs);
				_runner.Start(run, check, () =>
				{
					var session = BattleFactory.Create(_database, PlayerTeam.Build(_player, _database, content), encounter, _random.Next());
					var log = new List<BattleEvent>();
					// A escolha da pausa vale aqui também, lida a cada luta.
					var won = AutoBattle.Run(session, log, _player.FocusBoss);
					return (won, BattlePace.Seconds(log, BattlePace.AutoBattleFactor));
				}, victory, () =>
				{
					Save();
					UiSession.NotifyChanged();
				});
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

		/// <summary>A janela da Batalha automática, por cima de qualquer tela; fechar não para nada.</summary>
		private void OpenAutoBattle()
		{
			if (_runner.Run is not { } run)
				return;

			AutoBattleDialog.Open(_ui, run, _player, new AutoBattleActions
			{
				Stop = _runner.Stop,
				Resume = _runner.Resume,
				Dismiss = _runner.Dismiss,
				SetRuns = _runner.SetRuns,
				SellRune = rune =>
				{
					if (RuneInventory.Sell(_player, rune) > 0)
						run.Sold.Add(rune.Id);
					Save();
					run.Notify();
					RefreshCurrent();
				},
				LockRune = rune =>
				{
					rune.Locked = !rune.Locked;
					Save();
					run.Notify();
					RefreshCurrent();
				},
				LockMonster = monster =>
				{
					monster.Locked = !monster.Locked;
					Save();
					run.Notify();
					RefreshCurrent();
				},
				UpgradeRune = (rune, target) =>
				{
					RuneInventory.Upgrade(_random, _player, rune, target);
					Save();
					run.Notify();
					RefreshCurrent();
				},
				ManageRunes = () =>
				{
					if (_screen is not RuneScreen)
						ShowRunes(null, _current);
				},
			});
		}

		/// <summary>Remonta a tela de agora quando uma janela por cima mudou algo que ela mostra (moedas, runas).</summary>
		private void RefreshCurrent()
		{
			if (_screen is not BattleScreen)
				_current();
		}

		// Infraestrutura ----------------------------------------------------------------------------

		/// <summary>Aplica uma regra, salva e atualiza a tela.</summary>
		private void Change(Action change, Action refresh)
		{
			change();
			Save();
			refresh();
		}

		private Core.Runes.Rune Rune(int id) => _player.Runes.First(r => r.Id == id);

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
		private static void Collect() => GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);

		/// <summary>Grava no aparelho e, jogando na conta, manda para a nuvem logo em seguida (as ações seguidas viram um envio só).</summary>
		private void Save()
		{
			if (!_playing)
				return;
			_store.Save(_player);
			_account.SaveSoon();
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
		}

		private static string? Argument(string prefix) => OS.GetCmdlineUserArgs()
			.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
	}
}
