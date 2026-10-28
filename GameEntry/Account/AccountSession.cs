using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using HttpClient = System.Net.Http.HttpClient;

namespace Sigilos.GameEntry.Account
{
	public enum LossReason
	{
		/// <summary>Outro aparelho entrou na conta. Nada se perde: a conta é do servidor e este aparelho só tinha a cópia.</summary>
		Taken,

		/// <summary>O acesso venceu e não renova: entrar de novo com a senha.</summary>
		Expired,
	}

	/// <summary>O fim da sincronização: o save que vale agora (<see cref="Downloaded"/> se veio da nuvem), ou o erro.</summary>
	public sealed record SyncResult(PlayerState? Player, bool Downloaded, ApiResponse? Error)
	{
		public bool Ok => Player != null;
	}

	/// <summary>
	/// A conta deste aparelho, do login ao "Sair da conta": junta o <see cref="AuthClient"/>, a
	/// <see cref="SessionLock"/>, o <see cref="CloudSave"/> e o <see cref="AccountStore"/>, e cuida do batimento. Quem
	/// mostra as telas e troca o save aberto é o GameRoot; quem escolhe entre esta conta e o save local de teste é o
	/// <see cref="GameApi"/>.
	///
	/// A conta é do servidor. Ele decide tudo que gasta, rende ou sorteia, e o aparelho guarda só a cópia: ela muda
	/// apenas pelo que o servidor devolve (<see cref="StatePatch"/>), e nada sobe do aparelho.
	/// 1. Entrar: <see cref="Login"/> (ou <see cref="Register"/>, ou <see cref="Resume"/> com o token
	///    guardado), <see cref="Claim"/> a sessão e <see cref="Sync"/>, que baixa a cópia (e pede ao servidor um
	///    jogo novo, na conta sem save). Daí <see cref="Begin"/> liga o batimento.
	/// 2. Jogando: <see cref="Act"/>, <see cref="Battle"/> e <see cref="Rename"/> pedem ao servidor, a partir da revisão da
	///    cópia, e aplicam o que voltou.
	///    - Outro aparelho tomou a conta: <see cref="Lost"/>.
	///    - O servidor já tem outra revisão (409): <see cref="SyncNeeded"/>, e o GameRoot baixa de novo.
	/// 3. Sem internet não há jogo na conta (<see cref="Blocked"/>): a cada batimento o jogo tenta de novo,
	///    <see cref="ConnectionChanged"/> avisa a queda e a volta, e a volta pede <see cref="SyncNeeded"/>.
	/// 4. Sair: <see cref="Leave"/>.
	///
	/// Cada conta tem a sua cópia no aparelho (<c>nome.account-id.json</c>). O <see cref="OfflineStore"/> é só o do
	/// save local de teste.
	/// </summary>
	public partial class AccountSession : Node
	{
		/// <summary>O servidor de verdade; <c>-- --server=url</c> troca (um servidor local, para testar).</summary>
		public const string DefaultServer = "https://sigilos.minopavel.duckdns.org/";

		/// <summary>Quanto se joga na conta sem o servidor, desde a última sincronização, antes de o jogo parar (<see cref="Blocked"/>).</summary>
		public const double OfflineLimitSeconds = 0;

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
		private Func<PlayerState?> _player = () => null;
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
			Rta = new RtaLink(_http.BaseAddress!, _auth.Token);
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

		/// <summary>A conta caiu durante o jogo: o GameRoot volta para o login com o aviso.</summary>
		public event Action<LossReason>? Lost;

		/// <summary>
		/// É preciso baixar a cópia de novo durante o jogo (o GameRoot chama <see cref="Sync"/>): o servidor já tem outra
		/// revisão, ou a conexão voltou depois de um tempo sem ela.
		/// </summary>
		public event Action? SyncNeeded;

		/// <summary><see cref="Connected"/> ou <see cref="Blocked"/> mudou.</summary>
		public event Action? ConnectionChanged;

		/// <summary>O save local de teste (<c>--save=</c>), que nunca vai para a conta.</summary>
		public SaveStore OfflineStore { get; }

		/// <summary>O save da conta de agora neste aparelho; nulo sem conta.</summary>
		public SaveStore? Store => _data.UserId is { } user ? new SaveStore($"{_slot}.account-{user:N}") : null;

		/// <summary>Entrou e está jogando na conta (entre <see cref="Begin"/> e sair ou cair).</summary>
		public bool Playing { get; private set; }

		/// <summary>Jogando na conta e falando com o servidor; falso jogando sem internet.</summary>
		public bool Connected => Playing && _lock.SessionId != null && !_away;

		/// <summary>
		/// O jogo na conta parou: sem o servidor não há jogo, porque cada regra e cada gasto são dele e o aparelho só mostra a
		/// cópia. Só uma sincronização que dá certo solta (<see cref="Reconnect"/>).
		/// </summary>
		public bool Blocked => Playing && !Connected;

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

		/// <summary>O volume dos efeitos sonoros deste aparelho, de 0 a 1.</summary>
		public float EffectsVolume
		{
			get => _data.EffectsVolume;
			set
			{
				_data.EffectsVolume = value;
				_data.Save();
			}
		}

		/// <summary>A velocidade da luta deste aparelho: o índice em <see cref="Core.Battle.BattlePace.Speeds"/>, igual no manual e no automático.</summary>
		public int BattleSpeed
		{
			get => _data.BattleSpeed;
			set
			{
				if (_data.BattleSpeed == value)
					return;
				_data.BattleSpeed = value;
				_data.Save();
			}
		}

		/// <summary>O dia (UTC) em que a janela do check-in abriu sozinha neste aparelho: ela abre uma vez por dia.</summary>
		public DateTime? CheckInShown
		{
			get => _data.CheckInShown;
			set
			{
				if (_data.CheckInShown == value)
					return;
				_data.CheckInShown = value;
				_data.Save();
			}
		}

		/// <summary>O e-mail da última conta que entrou neste aparelho.</summary>
		public string? Email => _data.Email;

		/// <summary>O nome da conta, único no servidor; nulo numa conta criada antes do nome existir.</summary>
		public string? AccountName => _data.Name;

		/// <summary>O Chat global: aberto enquanto <see cref="Connected"/> (caído, abre de novo no próximo batimento).</summary>
		public ChatLink Chat { get; }

		/// <summary>A Arena do Véu ao vivo: aberta só enquanto o jogador está nela (a tela abre e fecha).</summary>
		public RtaLink Rta { get; }

		/// <summary>Os amigos da conta no servidor (só com <see cref="Connected"/> responde).</summary>
		public CloudFriends Friends { get; }

		/// <summary>Há um token guardado: dá para entrar sem senha (<see cref="Resume"/>).</summary>
		public bool Remembered => _auth.SignedIn;

		/// <summary>A última resposta do servidor que mudou ou baixou a cópia, nesta execução.</summary>
		public DateTimeOffset? LastSync { get; private set; }

		public override void _Ready()
		{
			AddChild(_beat);
			_beat.Timeout += async () => Handle(await Check());
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

		/// <summary>Esqueci a senha: troca a senha com a chave de recuperação da conta.</summary>
		public Task<ApiResponse> ResetPassword(string email, string recoveryKey, string password) => _auth.ResetPassword(email.Trim(), recoveryKey.Trim(), password);

		/// <summary>Cria a conta (com o nome) e já entra nela.</summary>
		public async Task<ApiResponse> Register(string email, string password, string name)
		{
			var response = await _auth.Register(email.Trim(), password, name);
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

		/// <summary>
		/// Dá ou troca o nome da conta. O servidor recusa nome fora da regra (<c>invalid_name</c>), já usado
		/// (<c>name_taken</c>) ou a troca antes da semana passar (<c>rename_cooldown</c>); aceito, a data da troca entra
		/// na cópia daqui.
		/// </summary>
		public async Task<ApiResponse> Rename(string name)
		{
			if (!Connected || _player() is not { } player || Store is not { } store || _lock.SessionId is not { } session)
				return new ApiResponse(0, "offline", default);

			var response = await _cloud.Rename(session, Revision, name);
			if (response.Unreached)
			{
				Handle(BeatOutcome.Unknown);
				return response;
			}

			if (response.Ok && response.Number("revision") is { } revision)
			{
				if (response.Text("patch") is { } text && JsonNode.Parse(text) is JsonObject { Count: > 0 } patch)
					StatePatch.Apply(player, patch);
				Mark(revision, store.Save(player));
				_data.Name = response.Text("name");
				_data.Save();
				return response;
			}

			if (Stale(response))
				SyncNeeded?.Invoke();
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
		/// Baixa a conta do servidor: o que vale é sempre o dele, e o aparelho só guarda a cópia. Conta sem save (nova, ou de
		/// antes de a versão nova zerar as contas) pede ao servidor que comece um jogo novo. Pedidos enquanto uma
		/// sincronização corre esperam a mesma.
		/// </summary>
		public Task<SyncResult> Sync()
		{
			if (_syncing is { IsCompleted: false })
				return _syncing;
			_syncing = SyncOnce();
			return _syncing;
		}

		private async Task<SyncResult> SyncOnce()
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
			if (cloud == null || PlayerSave.IsObsolete(cloud.Json))
			{
				if (cloud != null)
					store.Backup(cloud.Json, "cloud");

				var started = _lock.SessionId is { } opened ? await _cloud.Init(opened) : new ApiResponse(409, "session_taken", default);
				if (!started.Ok)
				{
					if (started.Unreached)
						Handle(BeatOutcome.Unknown);
					return new SyncResult(null, false, started);
				}

				download = await _cloud.Download();
				if (!download.Ok || download.Copy == null)
					return new SyncResult(null, false, download.Response);
				cloud = download.Copy;
			}

			var cloudPlayer = Parse(cloud.Json);
			if (cloudPlayer == null)
				return new SyncResult(null, false, new ApiResponse(422, "cloud_unreadable", default));

			var local = store.Load() != null ? store.Read() : null;
			if (local == cloud.Json)
			{
				Mark(cloud.Revision, local);
				return new SyncResult(store.Load(), false, null);
			}

			store.Write(cloud.Json);
			Mark(cloud.Revision, cloud.Json);
			return new SyncResult(cloudPlayer, true, null);
		}

		/// <summary>
		/// Liga o batimento; <paramref name="player"/> devolve a cópia aberta. Sem <paramref name="connected"/>, o
		/// batimento tenta reconectar.
		/// </summary>
		public void Begin(Func<PlayerState?> player, bool connected = true)
		{
			_player = player;
			_away = !connected;
			Playing = true;
			_beat.Start(connected ? SessionLock.BeatSeconds : RetrySeconds);
			if (connected)
				Chat.Open();
		}

		/// <summary>
		/// Pede ao servidor uma ação (gastar, render, sortear, organizar): ele a faz sobre o save dele e devolve o que mudou,
		/// que entra na cópia daqui no mesmo quadro. Sem conexão, nada é feito.
		/// </summary>
		public async Task<ActionOutcome> Act(ActionRequest request)
		{
			if (!Connected || _player() is not { } player || Store is not { } store || _lock.SessionId is not { } session)
				return new ActionOutcome(null, "offline");

			var response = await _cloud.Action(session, Revision, request);
			if (response.Unreached)
			{
				Handle(BeatOutcome.Unknown);
				return new ActionOutcome(null, "offline");
			}

			if (response.Ok && response.Text("delta") is { } text && ActionDelta.FromJson(text) is { } delta && response.Number("revision") is { } revision)
			{
				if (delta.Patch is { Count: > 0 } patch)
					StatePatch.Apply(player, patch);
				Mark(revision, store.Save(player));
				return new ActionOutcome(delta, null);
			}

			if (Stale(response))
			{
				SyncNeeded?.Invoke();
				return new ActionOutcome(null, "conflict");
			}

			return new ActionOutcome(null, "refused");
		}

		/// <summary>
		/// O começo (<paramref name="begin"/>) ou o fim de uma luta do servidor. O começo devolve a semente e o bilhete (e,
		/// na Arena, já gasta o Convite); o fim leva o bilhete, e o servidor confere, paga e devolve o que mudou. Sem conexão,
		/// nada é feito.
		/// </summary>
		public async Task<BattleCall> Battle(BattleRequest request, bool begin)
		{
			if (!Connected || _player() is not { } player || Store is not { } store || _lock.SessionId is not { } session)
				return new BattleCall(null, "offline");

			var response = await _cloud.Battle(session, Revision, request, begin);
			if (response.Unreached)
			{
				Handle(BeatOutcome.Unknown);
				return new BattleCall(null, "offline");
			}

			if (response.Ok && response.Text("report") is { } text && BattleReport.FromJson(text) is { } report && response.Number("revision") is { } revision)
			{
				if (report.Patch.Count > 0)
					StatePatch.Apply(player, report.Patch);
				Mark(revision, store.Save(player));
				return new BattleCall(report, null);
			}

			if (Stale(response))
			{
				SyncNeeded?.Invoke();
				return new BattleCall(null, "conflict");
			}

			return new BattleCall(null, response.Error == "battle_ticket" ? "ticket" : "refused");
		}

		/// <summary>A revisão da cópia daqui: os pedidos partem dela.</summary>
		private long Revision => _data.CurrentSync?.Revision ?? 0;

		/// <summary>O servidor recusou porque a cópia daqui não é mais a dele, ou a sessão é de outro aparelho.</summary>
		private static bool Stale(ApiResponse response) =>
			response.Conflict("save_conflict") || response.Conflict("session_taken") || response.Conflict("save_unreadable");

		/// <summary>
		/// Sai da conta: para o batimento e solta a sessão (sem esperar mais que <see cref="LeaveTimeout"/>).
		/// <paramref name="forget"/> esquece o token (Sair da conta); sem ele, a próxima abertura entra sozinha.
		/// Nada a enviar: o servidor já tem tudo.
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

		/// <summary>
		/// Traz da nuvem o que o servidor mudou por conta própria (o resultado de uma partida do Véu, o perfil): baixa a conta e leva a
		/// diferença à cópia daqui. O servidor vence; o aparelho não tem o que perder, porque nada se escreve nele.
		/// </summary>
		public async Task<bool> Resync()
		{
			if (!Connected || _player() is not { } player || Store is not { } store)
				return false;

			var download = await _cloud.Download();
			if (download.Copy is not { } copy || PlayerSave.FromJson(copy.Json) is not { } cloud)
				return false;

			StatePatch.Apply(player, StatePatch.Diff(StatePatch.Snapshot(player), cloud));
			Mark(copy.Revision, store.Save(player));
			return true;
		}

		/// <summary>A cópia daqui ficou igual à do servidor nesta revisão: anota, e a conta do tempo sem conexão volta a zero.</summary>
		private void Mark(long revision, string? json)
		{
			var blocked = Blocked;
			_data.OfflineSeconds = 0;
			_data.MarkSynced(new SyncPoint(revision, CloudSync.Hash(json ?? "")));
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
		/// A conexão caiu ou voltou. Na volta, o servidor pode ter mudado a conta (outro aparelho jogou nesse meio tempo):
		/// pede para baixar de novo. Sem conexão, o batimento tenta mais vezes.
		/// </summary>
		private void SetAway(bool away)
		{
			if (!Playing || _away == away)
				return;
			_away = away;
			_beat.Start(away ? RetrySeconds : SessionLock.BeatSeconds);
			if (away)
			{
				Chat.Close();
				Rta.Close();
			}

			ConnectionChanged?.Invoke();
			if (!away)
				SyncNeeded?.Invoke();
		}

		/// <summary>A conta caiu (tomada por outro aparelho, ou o acesso venceu). Nada a guardar: a cópia daqui já é a do servidor.</summary>
		private void Lose(LossReason reason)
		{
			if (!Playing)
				return;

			Stop();
			_lock.Drop();
			Lost?.Invoke(reason);
		}

		private void Stop()
		{
			Playing = false;
			_away = false;
			_player = () => null;
			_beat.Stop();
			Chat.Close();
			Rta.Close();
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
