using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O melhor tempo de cada luta vencida, em segundos: uma chave por fase da Campanha, por andar de
	/// Masmorra e por constelação em cada Exploração. O tempo é o da luta na tela (sem a pausa), na velocidade que o jogador escolheu.
	/// </summary>
	public static class Records
	{
		public static string StageKey(int stage) => $"stage{stage}";

		public static string FloorKey(string dungeonId, int floor) => $"{dungeonId}{floor}";

		/// <summary>Uma constelação numa das três Explorações: os desafios são outros, o tempo também.</summary>
		public static string ConstellationKey(string constellationId, int variation) => $"star_{constellationId}{variation + 1}";

		/// <summary>O melhor tempo gravado; null se a luta nunca foi vencida na tela.</summary>
		public static double? Best(PlayerState player, string key) =>
			player.BestTimes.TryGetValue(key, out var best) ? best : null;

		/// <summary>Grava o tempo de uma vitória se ele bate o melhor. Devolve verdadeiro quando bateu.</summary>
		public static bool Submit(PlayerState player, string key, double seconds)
		{
			if (seconds <= 0 || player.BestTimes.TryGetValue(key, out var best) && best <= seconds)
				return false;

			player.BestTimes[key] = seconds;
			return true;
		}
	}
}
