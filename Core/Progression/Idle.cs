using System;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Ociosidade (GDD, seção 11): com o jogo fechado, os círculos de invocação continuam canalizando
	/// Pergaminhos, Essência e Pó de Sigilo. A taxa cresce com a fase mais alta vencida e o acúmulo
	/// para em 12 horas. Uma vez por dia, a Canalização Rápida entrega 2 horas na hora.
	///
	/// O relógio entra como parâmetro (<c>now</c>): os testes escolhem a hora sem mexer no sistema.
	/// A fração que não fecha uma unidade fica guardada para a próxima coleta.
	/// </summary>
	public static class Idle
	{
		public const double CapHours = 12;
		public const double QuickChannelHours = 2;

		public static double ScrollsPerHour(int highestStage) => 0.12 + 0.004 * highestStage;
		public static double EssencePerHour(int highestStage) => 60 + 12 * highestStage;
		public static double DustPerHour(int highestStage) => 6 + 1.5 * highestStage;

		/// <summary>Horas acumuladas agora, já com o teto.</summary>
		public static double PendingHours(PlayerState player, DateTime now)
		{
			var hours = (now - player.LastIdleCollect).TotalHours;
			return Math.Clamp(hours, 0, CapHours);
		}

		/// <summary>Quanto uma coleta daria agora, sem coletar.</summary>
		public static IdleReward Preview(PlayerState player, DateTime now) => Reward(player, PendingHours(player, now), commit: false);

		/// <summary>Coleta o que acumulou e zera o relógio.</summary>
		public static IdleReward Collect(PlayerState player, DateTime now)
		{
			var reward = Reward(player, PendingHours(player, now), commit: true);
			player.LastIdleCollect = now;
			return reward;
		}

		public static bool CanQuickChannel(PlayerState player, DateTime now) => player.LastQuickChannel.Date < now.Date;

		/// <summary>2 horas de recompensa na hora, uma vez por dia. Não mexe no relógio da ociosidade.</summary>
		public static IdleReward QuickChannel(PlayerState player, DateTime now)
		{
			if (!CanQuickChannel(player, now))
				return new IdleReward(0, 0, 0, 0);

			player.LastQuickChannel = now;
			return Reward(player, QuickChannelHours, commit: true);
		}

		private static IdleReward Reward(PlayerState player, double hours, bool commit)
		{
			var scrolls = player.IdleScrollCarry + ScrollsPerHour(player.HighestStage) * hours;
			var essence = player.IdleEssenceCarry + EssencePerHour(player.HighestStage) * hours;
			var dust = player.IdleDustCarry + DustPerHour(player.HighestStage) * hours;
			var reward = new IdleReward((int)Math.Floor(scrolls), (int)Math.Floor(essence), (int)Math.Floor(dust), hours);

			if (commit)
			{
				player.IdleScrollCarry = scrolls - reward.Scrolls;
				player.IdleEssenceCarry = essence - reward.Essence;
				player.IdleDustCarry = dust - reward.Dust;
				player.Scrolls += reward.Scrolls;
				player.Essence += reward.Essence;
				player.Dust += reward.Dust;
			}

			return reward;
		}
	}
}
