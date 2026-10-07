using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// A conversa com o servidor de contas (docs/SERVIDOR_PROPRIO.md): criar conta, entrar, renovar o
	/// acesso e mandar as outras perguntas já com o token (<see cref="Call"/>). Não sabe de Godot nem de
	/// arquivo: quem guarda o token de renovação é o <see cref="AccountStore"/>, avisado por
	/// <see cref="RefreshTokenChanged"/>. A senha nunca fica guardada.
	///
	/// - O token de acesso vale 15 minutos e mora só na memória. Vencido, ou recusado com 401, é trocado
	///   por um novo com o de renovação, e a pergunta se repete uma vez.
	/// - O de renovação vale 30 dias e muda a cada uso: o velho deixa de valer. Por isso duas renovações
	///   ao mesmo tempo viram uma só (a segunda usaria um token já trocado e derrubaria a conta).
	/// - Renovação recusada é conta fora (<see cref="Expired"/>): o jogador entra de novo com a senha.
	/// </summary>
	public sealed class AuthClient
	{
		private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

		/// <summary>Folga antes do vencimento do token de acesso: renova um pouco antes de ele cair.</summary>
		private static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);

		private readonly HttpClient _http;
		private string? _accessToken;
		private DateTimeOffset _accessExpires;
		private Task<ApiResponse>? _refreshing;

		/// <param name="http">Com o endereço do servidor em <c>BaseAddress</c> (terminado em /).</param>
		public AuthClient(HttpClient http) => _http = http;

		public string? RefreshToken { get; private set; }

		/// <summary>O id da conta, lido do token de acesso (o servidor não o manda à parte).</summary>
		public Guid? UserId { get; private set; }

		/// <summary>O nome da conta, que vem junto de cada par de tokens; nulo numa conta criada antes do nome existir.</summary>
		public string? Name { get; private set; }

		public bool SignedIn => RefreshToken != null;

		/// <summary>O token de renovação mudou (entrou, renovou ou saiu): quem guarda grava o novo (nulo ao sair).</summary>
		public event Action<string?>? RefreshTokenChanged;

		/// <summary>O servidor recusou a renovação: o token venceu ou foi trocado em outro lugar.</summary>
		public event Action? Expired;

		/// <summary>
		/// O servidor mandou a chave de recuperação da conta (no cadastro, ou na primeira entrada de uma conta
		/// de antes da chave): é a única vez que ela vem.
		/// </summary>
		public event Action<string>? RecoveryKeyIssued;

		/// <summary>Volta com o token guardado de uma vez anterior; o de acesso vem na primeira pergunta.</summary>
		public void Restore(string refreshToken, Guid userId)
		{
			RefreshToken = refreshToken;
			UserId = userId;
			_accessToken = null;
		}

		public async Task<ApiResponse> Register(string email, string password, string invite, string name)
		{
			var response = await Send(HttpMethod.Post, "auth/register", new { email, password, inviteCode = invite, name }, null);
			if (response.Ok)
				TakeRecoveryKey(response);
			return response;
		}

		/// <summary>Troca a senha com a chave de recuperação da conta. Os acessos lembrados da conta deixam de valer.</summary>
		public Task<ApiResponse> ResetPassword(string email, string recoveryKey, string password) =>
			Send(HttpMethod.Post, "auth/reset", new { email, recoveryKey, password }, null);

		public async Task<ApiResponse> Login(string email, string password, string deviceId)
		{
			var response = await Send(HttpMethod.Post, "auth/login", new { email, password, deviceId }, null);
			if (response.Ok)
				Accept(response);
			return response;
		}

		/// <summary>Esquece os tokens (Sair da conta). Não fala com o servidor.</summary>
		public void SignOut()
		{
			_accessToken = null;
			UserId = null;
			Name = null;
			if (RefreshToken == null)
				return;
			RefreshToken = null;
			RefreshTokenChanged?.Invoke(null);
		}

		/// <summary>
		/// Troca o token de renovação por um par novo. Pedidos ao mesmo tempo esperam a mesma troca.
		/// Recusada (401), a conta sai (<see cref="Expired"/>).
		/// </summary>
		public Task<ApiResponse> Refresh()
		{
			if (_refreshing is { IsCompleted: false })
				return _refreshing;
			_refreshing = RefreshOnce();
			return _refreshing;
		}

		/// <summary>Uma pergunta com o token de acesso, renovado antes se venceu e uma vez mais se o servidor recusar.</summary>
		public async Task<ApiResponse> Call(HttpMethod method, string path, object? body = null)
		{
			if (RefreshToken == null)
				return new ApiResponse(401, "signed_out", default);

			if (_accessToken == null || DateTimeOffset.UtcNow >= _accessExpires)
			{
				var refreshed = await Refresh();
				if (!refreshed.Ok)
					return refreshed;
			}

			var response = await Send(method, path, body, _accessToken);
			if (!response.Unauthorized)
				return response;

			var again = await Refresh();
			return again.Ok ? await Send(method, path, body, _accessToken) : again;
		}

		/// <summary>
		/// Um token de acesso válido para quem fala com o servidor fora do HTTP (o Chat global), renovado antes
		/// se venceu; nulo fora da conta ou se a renovação falhou.
		/// </summary>
		public async Task<string?> Token()
		{
			if (RefreshToken == null)
				return null;
			if (_accessToken != null && DateTimeOffset.UtcNow < _accessExpires)
				return _accessToken;
			var refreshed = await Refresh();
			return refreshed.Ok ? _accessToken : null;
		}

		private async Task<ApiResponse> RefreshOnce()
		{
			if (RefreshToken == null)
				return new ApiResponse(401, "signed_out", default);

			var response = await Send(HttpMethod.Post, "auth/refresh", new { refreshToken = RefreshToken }, null);
			if (response.Ok)
				Accept(response);
			else if (response.Unauthorized)
			{
				SignOut();
				Expired?.Invoke();
			}

			return response;
		}

		/// <summary>Guarda o par de tokens de uma resposta de login ou renovação.</summary>
		private void Accept(ApiResponse response)
		{
			_accessToken = response.Text("accessToken");
			_accessExpires = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(response.Number("expiresIn") ?? 900) - Margin;
			UserId = _accessToken != null ? UserIdOf(_accessToken) ?? UserId : UserId;
			Name = response.Text("name");
			RefreshToken = response.Text("refreshToken");
			RefreshTokenChanged?.Invoke(RefreshToken);
			TakeRecoveryKey(response);
		}

		private void TakeRecoveryKey(ApiResponse response)
		{
			if (response.Text("recoveryKey") is { Length: > 0 } key)
				RecoveryKeyIssued?.Invoke(key);
		}

		private async Task<ApiResponse> Send(HttpMethod method, string path, object? body, string? token)
		{
			using var request = new HttpRequestMessage(method, path);
			if (token != null)
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			if (body != null)
				request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

			try
			{
				using var response = await _http.SendAsync(request);
				var text = await response.Content.ReadAsStringAsync();
				var json = Parse(text);
				var error = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
				return new ApiResponse((int)response.StatusCode, error, json);
			}
			catch (HttpRequestException)
			{
				return ApiResponse.Unreachable;
			}
			catch (OperationCanceledException)
			{
				// O tempo esgotado do HttpClient chega como cancelamento.
				return ApiResponse.Unreachable;
			}
		}

		private static JsonElement Parse(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return default;
			try
			{
				using var document = JsonDocument.Parse(text);
				return document.RootElement.Clone();
			}
			catch (JsonException)
			{
				return default;
			}
		}

		/// <summary>O <c>user_id</c> do miolo do JWT. Não confere a assinatura: quem confere é o servidor.</summary>
		private static Guid? UserIdOf(string token)
		{
			var parts = token.Split('.');
			if (parts.Length < 2)
				return null;

			var payload = parts[1].Replace('-', '+').Replace('_', '/');
			payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
			try
			{
				using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
				return document.RootElement.TryGetProperty("user_id", out var id) && Guid.TryParse(id.GetString(), out var user) ? user : null;
			}
			catch (FormatException)
			{
				return null;
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}
}
