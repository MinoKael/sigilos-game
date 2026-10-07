using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Player;
using HttpClient = System.Net.Http.HttpClient;

namespace Sigilos.GameEntry.Account
{
	public enum LossReason
	{
		/// <summary>Outro aparelho entrou na conta.</summary>
		Taken,

		/// <summary>
		/// Outro aparelho está com a conta quando a conexão voltou, depois de este jogar sem ela. O que se
		/// jogou aqui não vira backup: fica no aparelho e entra na conta na próxima entrada (com a pergunta, se
		/// a conta também mudou lá).
		/// </summary>
		TakenWhileAway,

		/// <summary>O acesso venceu e não renova: entrar de novo com a senha.</summary>
		Expired,
	}

	/// <summary>
	/// Dois saves que mudaram desde a última sincronização, para o jogador escolher.
	/// <paramref name="Adopting"/>: o local é o save sem conta deste aparelho, que subiria para a conta.
	/// </summary>
	public sealed record SaveConflict(PlayerState Local, DateTimeOffset? LocalSavedAt, PlayerState Cloud, DateTimeOffset CloudSavedAt, bool Adopting);

	/// <summary>O fim da sincronização: o save que vale agora (<see cref="Downloaded"/> se veio da nuvem), ou o erro.</summary>
	public sealed record SyncResult(PlayerState? Player, bool Downloaded, ApiResponse? Error)
	{
		public bool Ok => Player != null;
	}

	/// <summary>
	/// A conta deste aparelho, do login ao "Sair da conta": junta o <see cref="AuthClient"/>, a
	/// <see cref="SessionLock"/>, o <see cref="CloudSave"/> e o <see cref="AccountStore"/>, e cuida do tempo
	/// (o batimento e o envio periódico). Quem mostra as telas e troca o save aberto é o GameRoot.
	///
	/// O fluxo (docs/SAVE_NUVEM.md, seção "Contas"):
	/// 1. Entrar: <see cref="Login"/> (ou <see cref="Register"/>, ou <see cref="Resume"/> com o token
	///    guardado), <see cref="Claim"/> a sessão e <see cref="Sync"/>, que resolve o save desta conta.
	///    Daí <see cref="Begin"/> liga o batimento e o envio.
	/// 2. Jogando: o save local é a verdade e a nuvem é a cópia. Logo depois de cada ação que muda a
	///    conta (<see cref="SaveSoon"/>), a cada <see cref="UploadSeconds"/>, ao pausar e ao sair,
	///    <see cref="Flush"/> envia se mudou. Sem rede, nada muda para o jogador.
	///    - Outro aparelho tomou a conta: <see cref="Lost"/>. O que não subiu vira backup, nada é enviado.
	///    - A nuvem mudou por fora (o envio volta com conflito): <see cref="SyncNeeded"/>, e o GameRoot
	///      sincroniza de novo.
	/// 3. Sem internet: a conta lembrada abre com o save deste aparelho (<see cref="Begin"/> sem conexão), e
	///    a rede caindo no meio dá no mesmo. A cada batimento o jogo tenta de novo; <see cref="Connected"/>
	///    diz como está, e <see cref="ConnectionChanged"/> avisa a queda e a volta. Ao reconectar, toma a
	///    sessão sem forçar e pede <see cref="SyncNeeded"/>: o que se jogou sem rede sobe, ou o jogo pergunta,
	///    se a conta também mudou em outro aparelho. Se outro aparelho está com a conta nessa hora, este cai
	///    (<see cref="LossReason.TakenWhileAway"/>) sem perder nada.
	///    - Sem rede, o jogo na conta segue só por <see cref="OfflineLimitSeconds"/> de jogo; depois para
	///      (<see cref="Blocked"/>) até uma sincronização dar certo. A conta do tempo fica no aparelho: a
	///      conexão que volta e cai antes de sincronizar, fechar o jogo ou entrar de novo não a zeram.
	/// 4. Sair: <see cref="Flush"/> e <see cref="Leave"/>.
	///
	/// Cada conta tem o seu save no aparelho (<c>nome.account-id.json</c>); o do jogo sem conta
	/// (<see cref="OfflineStore"/>) sobe para a primeira conta que entrar sem save na nuvem.
	/// </summary>
	public partial class AccountSession : Node
	{
		/// <summary>O servidor de verdade; <c>-- --server=url</c> troca (um servidor local, para testar).</summary>
		public const string DefaultServer = "https://sigilos.minopavel.duckdns.org/";

		public const double UploadSeconds = 60;

		/// <summary>Quanto esperar depois da última ação para enviar (<see cref="SaveSoon"/>): ações seguidas viram um envio só.</summary>
		public const double SoonSeconds = 2;

		/// <summary>Quanto se joga na conta sem o servidor, desde a última sincronização, antes de o jogo parar (<see cref="Blocked"/>).</summary>
		public const double OfflineLimitSeconds = 120;

		/// <summary>Sem conexão, o batimento tenta de novo mais vezes: a volta da rede aparece logo.</summary>
		private const double RetrySeconds = 10;

		/// <summary>Sem conexão, a conta do tempo vai para o aparelho a cada tanto (fechar o jogo não a zera).</summary>
		private const double OfflineSaveSeconds = 5;

		/// <summary>Um quadro conta no máximo isto: o tempo com o jogo suspenso (o celular em segundo plano) não é jogo.</summary>
		private const double MaxFrameSeconds = 0.25;

		/// <summary>Quanto sair (fechar o jogo, Sair da conta) espera o servidor antes de seguir sem ele.</summary>
		private static readonly TimeSpan LeaveTimeout = TimeSpan.FromSeconds(3);

		private readonly string _slot;
		private readonly AccountStore _data;
		private readonly HttpClient _http;
		private readonly AuthClient _auth;
		private readonly SessionLock _lock;
		private readonly CloudSave _cloud;
		private readonly CloudMail _mail;
		// Os relógios seguem com o jogo pausado (a pausa da luta, o jogo parado sem conexão): a sessão continua
		// viva e a volta da rede aparece. A conta do tempo sem conexão (_Process) para com o jogo.
		private readonly Timer _beat = new() { Name = "Heartbeat", WaitTime = SessionLock.BeatSeconds, ProcessMode = ProcessModeEnum.Always };
		private readonly Timer _upload = new() { Name = "Upload", WaitTime = UploadSeconds, ProcessMode = ProcessModeEnum.Always };
		private readonly Timer _soon = new() { Name = "Soon", WaitTime = SoonSeconds, OneShot = true, ProcessMode = ProcessModeEnum.Always };
		private Func<PlayerState?> _player = () => null;
		private Task<bool>? _flushing;
		private Task<SyncResult>? _syncing;
		private Task<BeatOutcome>? _checking;

		/// <summary>O servidor não respondeu desde o último batimento que deu certo (ou o jogo abriu sem ele).</summary>
		private bool _away;

		public AccountSession(string slot, string server)
		{
			_slot = slot;
			_data = AccountStore.Load(slot);
			OfflineStore = new SaveStore(slot);
			_http = new HttpClient
			{
				BaseAddress = new Uri(server.EndsWith('/') ? server : server + "/"),
				Timeout = TimeSpan.FromSeconds(15),
			};
			_auth = new AuthClient(_http);
			if (_data.RefreshToken != null && _data.UserId is { } user)
				_auth.Restore(_data.RefreshToken, user);
			_auth.RefreshTokenChanged += token =>
			{
				_data.RefreshToken = token;
				_data.UserId = token == null ? null : _auth.UserId ?? _data.UserId;
				_data.Name = token == null ? null : _auth.Name;
				_data.Save();
			};
			_auth.Expired += () => Lose(LossReason.Expired);
			_auth.RecoveryKeyIssued += key =>
			{
				_data.RecoveryKey = key;
				_data.Save();
				RecoveryKeyIssued?.Invoke();
			};
			_lock = new SessionLock(_auth, _data.DeviceId, DeviceName());
			_cloud = new CloudSave(_auth);
			_mail = new CloudMail(_auth);
			Chat = new ChatLink(_http.BaseAddress!, _auth.Token);
			Friends = new CloudFriends(_auth);
		}

		/// <summary>Chegou a chave de recuperação da conta (<see cref="PendingRecoveryKey"/>): o GameRoot mostra.</summary>
		public event Action? RecoveryKeyIssued;

		/// <summary>A chave de recuperação que o jogador ainda não confirmou ter guardado; nula sem nenhuma.</summary>
		public string? PendingRecoveryKey => _data.RecoveryKey;

		/// <summary>O jogador guardou a chave: o aparelho a esquece.</summary>
		public void ConfirmRecoveryKey()
		{
			_data.RecoveryKey = null;
			_data.Save();
		}

		/// <summary>A conta caiu durante o jogo: o GameRoot volta para o login com o aviso. O segundo valor diz se o progresso que não subiu virou backup.</summary>
		public event Action<LossReason, bool>? Lost;

		/// <summary>
		/// É preciso sincronizar de novo durante o jogo (o GameRoot chama <see cref="Sync"/>): a nuvem mudou por
		/// fora, ou a conexão voltou depois de um tempo sem ela.
		/// </summary>
		public event Action? SyncNeeded;

		/// <summary><see cref="Connected"/> ou <see cref="Blocked"/> mudou.</summary>
		public event Action? ConnectionChanged;

		/// <summary>O save do jogo sem conta.</summary>
		public SaveStore OfflineStore { get; }

		/// <summary>O save da conta de agora neste aparelho; nulo sem conta.</summary>
		public SaveStore? Store => _data.UserId is { } user ? new SaveStore($"{_slot}.account-{user:N}") : null;

		/// <summary>Entrou e está jogando na conta (entre <see cref="Begin"/> e sair ou cair).</summary>
		public bool Playing { get; private set; }

		/// <summary>Jogando na conta e falando com o servidor; falso jogando sem internet.</summary>
		public bool Connected => Playing && _lock.SessionId != null && !_away;

		/// <summary>
		/// O jogo na conta parou: <see cref="OfflineLimitSeconds"/> jogados sem o servidor desde a última
		/// sincronização. Só uma sincronização que dá certo solta (<see cref="Reconnect"/>); a conexão que volta e
		/// cai antes dela não.
		/// </summary>
		public bool Blocked => Playing && _data.OfflineSeconds >= OfflineLimitSeconds;

		/// <summary>O jogador escolheu jogar sem conta: o jogo abre direto, sem a tela de login.</summary>
		public bool Offline
		{
			get => _data.Offline;
			set
			{
				_data.Offline = value;
				_data.Save();
			}
		}

		/// <summary>O idioma do último jogo deste aparelho (a tela de login sai nele).</summary>
		public string? Language
		{
			get => _data.Language;
			set
			{
				if (_data.Language == value)
					return;
				_data.Language = value;
				_data.Save();
			}
		}

		/// <summary>O volume geral deste aparelho, de 0 a 1.</summary>
		public float MasterVolume
		{
			get => _data.MasterVolume;
			set
			{
				_data.MasterVolume = value;
				_data.Save();
			}
		}

		/// <summary>O volume da música deste aparelho, de 0 a 1.</summary>
		public float MusicVolume
		{
			get => _data.MusicVolume;
			set
			{
				_data.MusicVolume = value;
				_data.Save();
			}
		}

		/// <summary>O e-mail da última conta que entrou neste aparelho.</summary>
		public string? Email => _data.Email;

		/// <summary>O nome da conta, único no servidor; nulo numa conta criada antes do nome existir.</summary>
		public string? AccountName => _data.Name;

		/// <summary>O Chat global: aberto enquanto <see cref="Connected"/> (caído, abre de novo no próximo batimento).</summary>
		public ChatLink Chat { get; }

		/// <summary>Os amigos da conta no servidor (só com <see cref="Connected"/> responde).</summary>
		public CloudFriends Friends { get; }

		/// <summary>Há um token guardado: dá para entrar sem senha (<see cref="Resume"/>).</summary>
		public bool Remembered => _auth.SignedIn;

		/// <summary>A última sincronização que deu certo nesta execução.</summary>
		public DateTimeOffset? LastSync { get; private set; }

		/// <summary>O save mudou desde a última sincronização e ainda não subiu.</summary>
		public bool Pending => Playing && _player() is { } player && CloudSync.Changed(PlayerSave.ToJson(player), _data.CurrentSync);

		public override void _Ready()
		{
			AddChild(_beat);
			AddChild(_upload);
			AddChild(_soon);
			_beat.Timeout += async () => Handle(await Check());
			_upload.Timeout += () => _ = Flush();
			_soon.Timeout += () => _ = Flush();
		}

		public override void _ExitTree() => _http.Dispose();

		/// <summary>A conta do tempo jogado sem conexão.</summary>
		public override void _Process(double delta)
		{
			if (!Playing || !_away || Blocked)
				return;

			var before = _data.OfflineSeconds;
			_data.OfflineSeconds += Math.Min(delta, MaxFrameSeconds);
			if (Blocked || (int)(before / OfflineSaveSeconds) != (int)(_data.OfflineSeconds / OfflineSaveSeconds))
				_data.Save();
			if (Blocked)
				ConnectionChanged?.Invoke();
		}

		/// <summary>Cria a conta (com o convite e o nome) e já entra nela.</summary>
		/// <summary>Esqueci a senha: troca a senha com a chave de recuperação da conta.</summary>
		public Task<ApiResponse> ResetPassword(string email, string recoveryKey, string password) => _auth.ResetPassword(email.Trim(), recoveryKey.Trim(), password);

		public async Task<ApiResponse> Register(string email, string password, string invite, string name)
		{
			var response = await _auth.Register(email.Trim(), password, invite.Trim(), name);
			return response.Ok ? await Login(email, password) : response;
		}

		public async Task<ApiResponse> Login(string email, string password)
		{
			email = email.Trim();
			var response = await _auth.Login(email, password, _data.DeviceId);
			if (response.Ok)
			{
				_data.Email = email.ToLowerInvariant();
				_data.Offline = false;
				_data.UserId = _auth.UserId;
				_data.Save();
			}

			return response;
		}

		/// <summary>Entra com o token guardado, sem senha.</summary>
		public Task<ApiResponse> Resume() => _auth.Refresh();

		/// <summary>As cartas do correio desta conta que o servidor ainda não viu coletadas.</summary>
		public Task<MailFetch> FetchMail() => _mail.Fetch();

		/// <summary>Avisa o servidor que a carta foi coletada (a recompensa já está no save).</summary>
		public Task<ApiResponse> ClaimMail(string id) => _mail.Claim(id);

		/// <summary>Troca o nome da conta. O servidor recusa nome fora da regra (<c>invalid_name</c>) ou já usado (<c>name_taken</c>).</summary>
		public async Task<ApiResponse> Rename(string name)
		{
			var response = await _auth.Call(System.Net.Http.HttpMethod.Put, "account/name", new { name });
			if (response.Ok)
			{
				_data.Name = response.Text("name");
				_data.Save();
			}

			return response;
		}

		/// <summary>Esquece a conta guardada sem falar com o servidor (Usar outra conta).</summary>
		public void Forget()
		{
			Stop();
			_lock.Drop();
			_auth.SignOut();
		}

		/// <summary>Toma a sessão: <paramref name="force"/> derruba o outro aparelho.</summary>
		public Task<ClaimResult> Claim(bool force) => _lock.Claim(force);

		/// <summary>
		/// Baixa a nuvem e decide qual save vale (<see cref="CloudSync"/>), perguntando ao jogador quando os
		/// dois mudaram (<paramref name="preferCloud"/>: verdadeiro fica o da nuvem). O que perde vira backup
		/// no aparelho. Conta sem save em lugar nenhum começa um jogo novo (<paramref name="newGame"/>).
		/// Pedidos enquanto uma sincronização corre esperam a mesma (uma pergunta só).
		/// </summary>
		public Task<SyncResult> Sync(Func<PlayerState> newGame, Func<SaveConflict, Task<bool>> preferCloud)
		{
			if (_syncing is { IsCompleted: false })
				return _syncing;
			_syncing = SyncOnce(newGame, preferCloud);
			return _syncing;
		}

		private async Task<SyncResult> SyncOnce(Func<PlayerState> newGame, Func<SaveConflict, Task<bool>> preferCloud)
		{
			if (Store is not { } store)
				return new SyncResult(null, false, new ApiResponse(401, "signed_out", default));

			var download = await _cloud.Download();
			if (!download.Ok)
			{
				if (download.Response.Unreached)
					Handle(BeatOutcome.Unknown);
				return new SyncResult(null, false, download.Response);
			}

			var cloud = download.Copy;
			var cloudRevision = cloud?.Revision ?? 0;

			// O save da nuvem é de antes de a versão nova zerar as contas (PlayerSave.IsObsolete): vira backup no
			// aparelho e conta como nuvem vazia; o jogo novo sobe por cima dele.
			if (cloud != null && PlayerSave.IsObsolete(cloud.Json))
			{
				store.Backup(cloud.Json, "cloud");
				cloud = null;
			}

			var cloudPlayer = cloud != null ? Parse(cloud.Json) : null;
			if (cloud != null && cloudPlayer == null)
				return new SyncResult(null, false, new ApiResponse(422, "cloud_unreadable", default));

			// O save desta conta neste aparelho. Sem ele, o save sem conta é candidato a subir para ela.
			var synced = _data.CurrentSync;
			var localPlayer = store.Load();
			var local = localPlayer != null ? store.Read() : null;
			var adopting = false;
			if (local == null && OfflineStore.Load() is { } offline)
			{
				localPlayer = offline;
				local = OfflineStore.Read();
				synced = null;
				adopting = true;
			}

			var action = CloudSync.Decide(local, synced, cloud);
			var chose = action == SyncAction.Ask;
			if (chose)
			{
				var conflict = new SaveConflict(localPlayer!, adopting ? OfflineStore.SavedAt : store.SavedAt, cloudPlayer!, cloud!.SavedAt, adopting);
				action = await preferCloud(conflict) ? SyncAction.Download : SyncAction.Upload;
			}

			switch (action)
			{
				case SyncAction.InSync:
					if (adopting)
						store.Write(local!);
					Mark(cloud!.Revision, local!);
					return new SyncResult(localPlayer, false, null);

				case SyncAction.Download:
					// O que só o aparelho tinha não se perde: fica guardado (o save sem conta fica onde está).
					if (local != null && !adopting && CloudSync.Changed(local, synced))
						store.Backup();
					store.Write(cloud!.Json);
					Mark(cloud.Revision, cloud.Json);
					return new SyncResult(cloudPlayer, true, null);

				default:
					var player = localPlayer ?? newGame();
					var json = local ?? store.Save(player);
					if (adopting)
						OfflineStore.MoveTo(store);
					if (chose)
						store.Backup(cloud!.Json, "cloud");

					// Sem rede agora, sobe no próximo envio: o save local já é o que vale.
					await Push(json, cloudRevision);
					return new SyncResult(player, false, null);
			}
		}

		/// <summary>
		/// Liga o batimento e o envio periódico; <paramref name="player"/> devolve o save aberto. Sem
		/// <paramref name="connected"/> (a conta lembrada aberta sem internet), o batimento tenta reconectar.
		/// </summary>
		public void Begin(Func<PlayerState?> player, bool connected = true)
		{
			_player = player;
			_away = !connected;
			Playing = true;
			_beat.Start(connected ? SessionLock.BeatSeconds : RetrySeconds);
			_upload.Start();
			if (connected)
				Chat.Open();
		}

		/// <summary>
		/// Grava e envia o save se ele mudou desde a última sincronização. Verdadeiro se a nuvem ficou igual
		/// ao aparelho (sem mudança conta como sim).
		/// </summary>
		public Task<bool> Flush()
		{
			if (_flushing is { IsCompleted: false })
				return _flushing;
			_flushing = FlushOnce();
			return _flushing;
		}

		/// <summary>
		/// Uma ação mudou a conta (subir nível, melhorar runa, fundir, invocar, vencer, bloquear...): envia
		/// para a nuvem daqui a <see cref="SoonSeconds"/>, contando de novo a cada ação. O envio a cada
		/// <see cref="UploadSeconds"/> continua por baixo, para o que muda sozinho (a canalização).
		/// </summary>
		public void SaveSoon()
		{
			if (Playing)
				_soon.Start();
		}

		/// <summary>
		/// Sai da conta: para o batimento e solta a sessão (sem esperar mais que <see cref="LeaveTimeout"/>).
		/// <paramref name="forget"/> esquece o token (Sair da conta); sem ele, a próxima abertura entra sozinha.
		/// Envie antes com <see cref="Flush"/>.
		/// </summary>
		public async Task Leave(bool forget)
		{
			Stop();
			await Task.WhenAny(_lock.Release(), Task.Delay(LeaveTimeout));
			if (forget)
				_auth.SignOut();
		}

		/// <summary>O jogo voltou do segundo plano: confere a sessão na hora, sem esperar o próximo batimento.</summary>
		public async void Wake()
		{
			if (!Playing)
				return;
			_beat.Start();
			Handle(await Check());
		}

		/// <summary>
		/// O jogo parado sem conexão (<see cref="Blocked"/>), no "Tentar de novo": tenta o servidor agora e,
		/// respondendo, sincroniza; é a sincronização que dá certo que solta o jogo.
		/// </summary>
		public async Task Reconnect()
		{
			if (!Playing)
				return;
			_beat.Start();
			var outcome = await Check();
			Handle(outcome);
			if (outcome == BeatOutcome.Alive && Blocked)
				SyncNeeded?.Invoke();
		}

		/// <summary>
		/// O batimento. Sem sessão (o jogo abriu sem internet), tenta tomar uma, sem forçar. Pedidos enquanto um
		/// corre esperam o mesmo (sem conexão, ele tenta a cada <see cref="RetrySeconds"/>).
		/// </summary>
		private Task<BeatOutcome> Check()
		{
			if (_checking is { IsCompleted: false })
				return _checking;
			_checking = _lock.SessionId == null ? _lock.Recover() : _lock.Beat();
			return _checking;
		}

		private async Task<bool> FlushOnce()
		{
			// Sincronizando, ou sem conexão: quem envia é a sincronização (a de agora, ou a da volta da rede).
			if (!Playing || _syncing is { IsCompleted: false } || _away || Store is not { } store || _player() is not { } player)
				return false;

			var json = store.Save(player);
			var synced = _data.CurrentSync;
			if (!CloudSync.Changed(json, synced))
				return true;

			// Sem sessão ainda (sem internet desde a abertura): o save fica no aparelho até reconectar.
			if (_lock.SessionId == null)
				return false;

			var response = await Push(json, synced?.Revision ?? 0);
			if (response.Ok)
				return true;
			if (response.Unreached)
				Handle(BeatOutcome.Unknown);
			else if (Playing && (response.Conflict("save_conflict") || response.Conflict("revision_not_newer")))
				SyncNeeded?.Invoke();
			return false;
		}

		/// <summary>
		/// Envia <paramref name="json"/> por cima da revisão <paramref name="baseRevision"/>. "Sessão tomada"
		/// pode ser só o aparelho que dormiu: toma de novo sem forçar e tenta uma vez mais.
		/// </summary>
		private async Task<ApiResponse> Push(string json, long baseRevision)
		{
			for (var attempt = 0; ; attempt++)
			{
				if (_lock.SessionId is not { } session)
					return new ApiResponse(409, "session_taken", default);

				var response = await _cloud.Upload(session, baseRevision, baseRevision + 1, json);
				if (response.Ok)
				{
					Mark(baseRevision + 1, json);
					return response;
				}

				if (!response.Conflict("session_taken") || attempt > 0)
					return response;

				var outcome = await _lock.Recover();
				if (outcome != BeatOutcome.Alive)
				{
					Handle(outcome);
					return response;
				}
			}
		}

		/// <summary>A nuvem ficou igual ao aparelho: anota, e a conta do tempo sem conexão volta a zero.</summary>
		private void Mark(long revision, string json)
		{
			var blocked = Blocked;
			_data.OfflineSeconds = 0;
			_data.MarkSynced(new SyncPoint(revision, CloudSync.Hash(json)));
			LastSync = DateTimeOffset.Now;
			if (blocked)
				ConnectionChanged?.Invoke();
		}

		private void Handle(BeatOutcome outcome)
		{
			switch (outcome)
			{
				case BeatOutcome.Alive:
					SetAway(false);
					if (Playing && !Chat.Live)
						Chat.Open();
					break;
				case BeatOutcome.Unknown:
					SetAway(true);
					break;
				case BeatOutcome.Taken:
					Lose(LossReason.Taken);
					break;
				case BeatOutcome.Expired:
					Lose(LossReason.Expired);
					break;
			}
		}

		/// <summary>
		/// A conexão caiu ou voltou. Na volta, a nuvem pode ter mudado (outro aparelho jogou nesse meio tempo) e
		/// o aparelho pode ter progresso que não subiu: pede para sincronizar de novo. Sem conexão, o batimento
		/// tenta mais vezes.
		/// </summary>
		private void SetAway(bool away)
		{
			if (!Playing || _away == away)
				return;
			_away = away;
			_beat.Start(away ? RetrySeconds : SessionLock.BeatSeconds);
			if (away)
				Chat.Close();
			ConnectionChanged?.Invoke();
			if (!away)
				SyncNeeded?.Invoke();
		}

		/// <summary>
		/// A conta caiu.
		/// - Tomada por outro aparelho com este conectado: o progresso que não subiu (no máximo um envio) vira
		///   backup e não vai para a nuvem; ao entrar de novo aqui, vem o da nuvem, sem pergunta.
		/// - Tomada enquanto este estava sem conexão: o que se jogou sem rede fica no save do aparelho, e a
		///   próxima entrada aqui o leva para a conta (<see cref="LossReason.TakenWhileAway"/>).
		/// </summary>
		private void Lose(LossReason reason)
		{
			if (!Playing)
				return;

			var player = _player();
			if (reason == LossReason.Taken && _away)
				reason = LossReason.TakenWhileAway;
			Stop();
			_lock.Drop();
			var store = Store;
			var json = store != null && player != null ? store.Save(player) : null;
			var backedUp = reason == LossReason.Taken && json != null && CloudSync.Changed(json, _data.CurrentSync) && store!.Backup() != null;
			Lost?.Invoke(reason, backedUp);
		}

		private void Stop()
		{
			Playing = false;
			_away = false;
			_player = () => null;
			_beat.Stop();
			_upload.Stop();
			_soon.Stop();
			Chat.Close();
		}

		private static PlayerState? Parse(string json)
		{
			try
			{
				return PlayerSave.FromJson(json);
			}
			catch (JsonException)
			{
				return null;
			}
		}

		/// <summary>O nome que o outro aparelho vê na pergunta "sua conta está aberta em...".</summary>
		private static string DeviceName() =>
			OS.HasFeature("mobile") ? OS.GetModelName() : $"{OS.GetName()} ({System.Environment.MachineName})";
	}
}
