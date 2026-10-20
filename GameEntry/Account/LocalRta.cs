using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Rta;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// O Treino contra um fantasma da Arena do Véu: a mesma sala do servidor (<see cref="RtaRoom"/>) rodando aqui, sem
	/// conexão, contra um adversário que joga sozinho e não vale Áster nem Ecos. O jogador cai num dos dois lados ao
	/// acaso, como numa partida de verdade. As mensagens voltam na linha principal, uma por quadro.
	/// </summary>
	public sealed class LocalRta : IRtaChannel
	{
		private readonly CancellationTokenSource _stop = new();
		private readonly RtaRoom _room;
		private readonly RtaSeat _seat;

		private LocalRta(RtaRoom room, RtaSeat seat)
		{
			_room = room;
			_seat = seat;
		}

		public bool Live => !_stop.IsCancellationRequested;

		/// <summary>Chegou uma mensagem da sala, como chegaria do servidor.</summary>
		public event Action<RtaMessage>? Received;

		/// <summary>Monta o treino com o baú do jogador; nulo se ele não tem homúnculos diferentes para a montagem.</summary>
		public static LocalRta? Start(GameDatabase database, PlayerState player, string name, Random random, Action<RtaMessage> received)
		{
			if (RtaPlayer.Check(database, Veil.Pool(player, database)) is not { } pool)
				return null;

			var aster = player.Veil.Aster;
			var bench = ArenaTeams.Bench(database, database.Arena.GradeOf(aster).Ghost, random, 14);
			var seat = random.Next(2) == 0 ? RtaSeat.First : RtaSeat.Second;
			LocalRta? local = null;
			void Deliver(RtaMessage message) => Callable.From(() => local?.Received?.Invoke(message)).CallDeferred();

			var human = new RtaPlayer(name, aster, pool, Deliver);
			var ghostName = database.Arena.Names.Count > 0 ? database.Arena.Names[random.Next(database.Arena.Names.Count)] : "Fantasma";
			var ghost = new RtaPlayer(ghostName, aster, bench, _ => { }, bot: true);
			var room = new RtaRoom(database, seat == RtaSeat.First ? human : ghost, seat == RtaSeat.First ? ghost : human, random.Next(1, int.MaxValue), false);
			local = new LocalRta(room, seat);
			local.Received += received;
			_ = local.Run();
			return local;
		}

		private async Task Run()
		{
			try
			{
				await _room.Run(_stop.Token);
			}
			catch (OperationCanceledException)
			{
			}
		}

		public Task<bool> Send(RtaMessage message)
		{
			if (message.Type is "pick" or "ban" or "lead" or "act" or "leave" && Live)
				_room.Post(_seat, message);
			return Task.FromResult(Live);
		}

		/// <summary>Encerra o treino (o jogador saiu da tela).</summary>
		public void Stop() => _stop.Cancel();
	}
}
