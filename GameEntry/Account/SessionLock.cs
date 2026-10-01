using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	public enum ClaimOutcome
	{
		/// <summary>A sessão é deste aparelho (<see cref="SessionLock.SessionId"/>).</summary>
		Claimed,

		/// <summary>Outro aparelho está com a conta e bateu há pouco: o jogo pergunta antes de tomar à força.</summary>
		Busy,

		/// <summary>Não deu para perguntar (sem rede, conta fora, servidor com erro).</summary>
		Failed,
	}

	/// <summary>O resultado de tomar a sessão; em <see cref="ClaimOutcome.Busy"/>, qual aparelho está com ela e quando bateu.</summary>
	public sealed record ClaimResult(ClaimOutcome Outcome, ApiResponse Response)
	{
		public string? DeviceName => Response.Text("deviceName");

		public DateTimeOffset? LastSeen => Response.Time("lastSeen");
	}

	public enum BeatOutcome
	{
		/// <summary>A sessão continua deste aparelho.</summary>
		Alive,

		/// <summary>Outro aparelho tomou a conta: este cai para o login.</summary>
		Taken,

		/// <summary>O token não renova mais: o jogador entra de novo com a senha.</summary>
		Expired,

		/// <summary>Não deu para saber (sem rede): continua jogando e confere no próximo batimento.</summary>
		Unknown,
	}

	/// <summary>
	/// A trava "um aparelho por vez", do lado do jogo. O servidor guarda, por conta, qual sessão vale. Este
	/// aparelho toma a sessão ao entrar (<see cref="Claim"/>), bate o ponto a cada <see cref="BeatSeconds"/>
	/// (<see cref="Beat"/>) e solta ao sair (<see cref="Release"/>).
	///
	/// - Tomar sem forçar é recusado se outro aparelho bateu há menos de 30 s. O jogo pergunta e, se o
	///   jogador quiser, toma à força: o outro cai no próximo batimento dele.
	/// - O servidor só aceita o batimento (e o envio do save) de uma sessão que bateu há menos de 30 s. Por
	///   isso o batimento é a cada 20 s, e um "sessão tomada" pode ser só o aparelho que dormiu (celular em
	///   segundo plano, rede caída). Antes de derrubar o jogador, <see cref="Recover"/> toma de novo, sem
	///   forçar: se ninguém mais está com a conta, segue com uma sessão nova; se outro aparelho está, aí sim
	///   este caiu.
	/// </summary>
	public sealed class SessionLock
	{
		public const double BeatSeconds = 20;

		private readonly AuthClient _auth;
		private readonly string _deviceId;
		private readonly string _deviceName;
		private Task<BeatOutcome>? _recovering;

		public SessionLock(AuthClient auth, string deviceId, string deviceName)
		{
			_auth = auth;
			_deviceId = deviceId;
			_deviceName = deviceName;
		}

		/// <summary>A sessão deste aparelho; nula antes de tomar, depois de soltar ou quando outro tomou.</summary>
		public Guid? SessionId { get; private set; }

		public async Task<ClaimResult> Claim(bool force)
		{
			var response = await _auth.Call(HttpMethod.Post, "session/claim", new { deviceId = _deviceId, deviceName = _deviceName, force });
			if (response.Ok && Guid.TryParse(response.Text("sessionId"), out var id))
			{
				SessionId = id;
				return new ClaimResult(ClaimOutcome.Claimed, response);
			}

			return new ClaimResult(response.Conflict("session_active") ? ClaimOutcome.Busy : ClaimOutcome.Failed, response);
		}

		public async Task<BeatOutcome> Beat()
		{
			if (SessionId is not { } id)
				return BeatOutcome.Taken;

			var response = await _auth.Call(HttpMethod.Post, "session/heartbeat", new { sessionId = id });
			if (response.Ok)
				return BeatOutcome.Alive;
			return response.Conflict("session_taken") ? await Recover() : Outcome(response);
		}

		/// <summary>
		/// O servidor disse "sessão tomada" (no batimento ou no envio do save): toma de novo, sem forçar.
		/// Quem pergunta ao mesmo tempo espera a mesma resposta.
		/// </summary>
		public Task<BeatOutcome> Recover()
		{
			if (_recovering is { IsCompleted: false })
				return _recovering;
			_recovering = RecoverOnce();
			return _recovering;
		}

		/// <summary>Solta a sessão (Sair da conta, fechar o jogo): o próximo aparelho entra sem pergunta.</summary>
		public async Task Release()
		{
			if (SessionId is not { } id)
				return;
			SessionId = null;
			await _auth.Call(HttpMethod.Post, "session/release", new { sessionId = id });
		}

		/// <summary>Esquece a sessão sem falar com o servidor (outro aparelho já a tomou).</summary>
		public void Drop() => SessionId = null;

		private async Task<BeatOutcome> RecoverOnce()
		{
			var claim = await Claim(false);
			switch (claim.Outcome)
			{
				case ClaimOutcome.Claimed:
					return BeatOutcome.Alive;
				case ClaimOutcome.Busy:
					SessionId = null;
					return BeatOutcome.Taken;
				default:
					return Outcome(claim.Response);
			}
		}

		private static BeatOutcome Outcome(ApiResponse response) => response.Unauthorized ? BeatOutcome.Expired : BeatOutcome.Unknown;
	}
}
