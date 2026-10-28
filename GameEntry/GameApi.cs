using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.GameEntry.Account;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// O único caminho do jogo até as regras que mudam a conta: ações (<see cref="Act"/>), lutas (<see cref="Start"/> e
	/// <see cref="Finish"/>), a troca de nome e as cópias baixadas de novo. As telas só mostram; tudo que gasta, rende,
	/// sorteia ou organiza passa por aqui.
	///
	/// - Um pedido por vez: a cópia tem uma revisão só, então um pedido espera o anterior voltar em vez de falhar.
	/// - Na conta, quem faz é o servidor (<see cref="AccountSession"/>), e a cópia daqui só muda pelo que ele devolve.
	/// - No save local de teste (<c>--save=</c>), as mesmas regras rodam no aparelho, com o mesmo livro de bilhetes das
	///   lutas e a hora UTC, e o save é gravado a cada mudança.
	/// - Os objetos que vêm na resposta (a runa ganha, os monstros) são trocados pelos da cópia, com o mesmo Id: as
	///   telas comparam por referência.
	///
	/// Quem avisa o jogador de uma recusa é o GameRoot, pelo erro de cada resposta.
	/// </summary>
	public sealed class GameApi
	{
		/// <summary>Quantas vezes o fim de uma luta espera o tempo que o servidor diz faltar antes de desistir.</summary>
		private const int FinishAttempts = 3;

		/// <summary>A folga, em segundos, somada à espera que o servidor pede.</summary>
		private const double FinishSlack = 0.25;

		private readonly SemaphoreSlim _gate = new(1, 1);
		private readonly BattleTickets _tickets = new();
		private readonly AccountSession _account;
		private readonly Func<GameDatabase> _database;
		private readonly Random _random;
		private readonly Func<bool> _local;
		private readonly Func<PlayerState> _player;
		private readonly Action _save;

		/// <param name="database">Os dados de agora (trocar o idioma os carrega de novo).</param>
		/// <param name="local">Verdadeiro quando as regras rodam no aparelho (o save local de teste).</param>
		/// <param name="player">A cópia aberta.</param>
		/// <param name="save">Grava a cópia aberta no aparelho (só o save local de teste usa).</param>
		public GameApi(AccountSession account, Func<GameDatabase> database, Random random, Func<bool> local, Func<PlayerState> player, Action save)
		{
			_account = account;
			_database = database;
			_random = random;
			_local = local;
			_player = player;
			_save = save;
		}

		/// <summary>Uma ação: o que mudou, ou o erro (<see cref="ActionOutcome"/>).</summary>
		public Task<ActionOutcome> Act(ActionRequest request) => Gated(async () =>
		{
			var player = _player();
			if (_local())
			{
				var delta = ServerActions.Apply(_random, _database(), player, request, new ActionContext(DateTime.UtcNow, _ => null));
				if (delta == null)
					return new ActionOutcome(null, "refused");
				_save();
				return new ActionOutcome(delta, null);
			}

			var outcome = await _account.Act(request);
			if (outcome.Delta is { } changed)
				Own(_player(), changed);
			return outcome;
		});

		/// <summary>O começo de uma luta: a semente e o bilhete, ou a porta fechada (<see cref="BattleReport.Problem"/>).</summary>
		public Task<BattleCall> Start(BattleRequest request) => Gated(() => Battle(request, true));

		/// <summary>
		/// O fim da luta do bilhete: o servidor confere e, na vitória, paga. Chegando antes da hora (a Batalha automática
		/// tem de durar o que a luta leva na tela), espera o que falta, fora da fila, e pede de novo.
		/// </summary>
		public async Task<BattleCall> Finish(BattleRequest request)
		{
			for (var attempt = 1; ; attempt++)
			{
				var call = await Gated(() => Battle(request, false));
				if (call.Report is not { ReadyIn: > 0 } early)
					return call;
				if (attempt >= FinishAttempts)
					return new BattleCall(null, "refused");
				await Task.Delay(TimeSpan.FromSeconds(early.ReadyIn + FinishSlack));
			}
		}

		/// <summary>Dá ou troca o nome da conta (só na conta: no save local de teste, volta sem conexão).</summary>
		public Task<ApiResponse> Rename(string name) => Gated(() =>
			_local() ? Task.FromResult(new ApiResponse(0, "offline", default)) : _account.Rename(name));

		/// <summary>Baixa a cópia da conta de novo e troca a aberta (<see cref="AccountSession.Sync"/>), na vez dela na fila.</summary>
		public Task<SyncResult> Sync() => Gated(_account.Sync);

		/// <summary>Traz para a cópia aberta o que o servidor mudou por conta própria (<see cref="AccountSession.Resync"/>).</summary>
		public Task<bool> Resync() => Gated(_account.Resync);

		private async Task<BattleCall> Battle(BattleRequest request, bool begin)
		{
			var player = _player();
			if (_local())
			{
				BattleReport? report;
				var missing = false;
				report = begin
					? _tickets.Start(Guid.Empty, _random, _database(), player, request)
					: _tickets.Finish(Guid.Empty, _random, _database(), player, request, out missing);
				if (report == null)
					return new BattleCall(null, missing ? "ticket" : "refused");
				if (report.Patch.Count > 0)
					_save();
				return new BattleCall(report, null);
			}

			var call = await _account.Battle(request, begin);
			if (call.Report is { } answered)
				Own(_player(), answered);
			return call;
		}

		private async Task<T> Gated<T>(Func<Task<T>> work)
		{
			await _gate.WaitAsync();
			try
			{
				return await work();
			}
			finally
			{
				_gate.Release();
			}
		}

		/// <summary>Troca a runa e os monstros da resposta pelos da cópia, com o mesmo Id.</summary>
		private static void Own(PlayerState player, ActionDelta delta)
		{
			if (delta.Rune is { } rune)
				delta.Rune = Own(player, rune);
			for (var i = 0; i < delta.Runes.Count; i++)
				delta.Runes[i] = Own(player, delta.Runes[i]);
		}

		/// <summary>Troca a runa, as runas extras e o monstro da recompensa pelos da cópia, com o mesmo Id.</summary>
		private static void Own(PlayerState player, BattleReport report)
		{
			if (report.Reward is not { } reward)
				return;
			if (reward.Rune is { } rune)
				reward.Rune = Own(player, rune);
			for (var i = 0; i < reward.BonusRunes.Count; i++)
				reward.BonusRunes[i] = Own(player, reward.BonusRunes[i]);
			if (reward.Monster is { } monster)
				reward.Monster = player.Monsters.FirstOrDefault(m => m.Id == monster.Id) ?? monster;
		}

		private static Core.Runes.Rune Own(PlayerState player, Core.Runes.Rune rune) =>
			player.Runes.FirstOrDefault(r => r.Id == rune.Id) ?? rune;
	}
}
