using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O melhor tempo de cada luta vencida, em segundos: uma chave por fase da Campanha e por andar de
	/// Masmorra. O tempo é o da luta na tela (sem a pausa), na velocidade que o jogador escolheu. Junto do
	/// tempo fica a equipe que o fez (<see cref="Team"/>).
	/// </summary>
	public static class Records
	{
		public static string StageKey(int stage) => $"stage{stage}";

		public static string FloorKey(string dungeonId, int floor) => $"{dungeonId}{floor}";

		/// <summary>O melhor tempo gravado; null se a luta nunca foi vencida na tela.</summary>
		public static double? Best(PlayerState player, string key) =>
			player.BestTimes.TryGetValue(key, out var best) ? best : null;

		/// <summary>A equipe do melhor tempo, na ordem dela (a Líder primeiro); vazia se o tempo é de antes de a equipe ser gravada.</summary>
		public static IReadOnlyList<RecordMember> Team(PlayerState player, string key) =>
			player.BestTeams.TryGetValue(key, out var team) ? team : [];

		/// <summary>
		/// O melhor tempo do andar mais fundo vencido de cada Masmorra, quando é de antes de a equipe ser
		/// gravada, fica com a equipe salva hoje para aquela Masmorra: foi a última a avançar ali, a mais
		/// provável de ter feito o tempo. Os outros tempos antigos ficam sem equipe; os que já têm não mudam.
		/// </summary>
		public static void FillDeepestTeams(PlayerState player, GameDatabase database)
		{
			foreach (var dungeon in database.Dungeons)
			{
				if (!player.DungeonFloors.TryGetValue(dungeon.Id, out var cleared) || cleared <= 0)
					continue;

				var key = FloorKey(dungeon.Id, cleared);
				if (!player.BestTimes.ContainsKey(key) || player.BestTeams.ContainsKey(key))
					continue;

				var team = Teams.Of(player, dungeon.Id)
					.Select(player.Monster)
					.OfType<OwnedSummon>()
					.Where(m => !m.Stored && database.HasSummon(m.SummonId))
					.Select(m => new RecordMember(m.SummonId, m.Awakened))
					.ToList();
				if (team.Count > 0)
					player.BestTeams[key] = team;
			}
		}

		/// <summary>Grava o tempo de uma vitória, e a equipe dela, se ele bate o melhor. Devolve verdadeiro quando bateu.</summary>
		public static bool Submit(PlayerState player, string key, double seconds, IEnumerable<RecordMember>? team = null)
		{
			if (seconds <= 0 || player.BestTimes.TryGetValue(key, out var best) && best <= seconds)
				return false;

			player.BestTimes[key] = seconds;
			if (team != null)
				player.BestTeams[key] = team.ToList();
			return true;
		}
	}
}
