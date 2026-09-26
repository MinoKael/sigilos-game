using System;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Ociosidade (GDD, seção 11): com o jogo fechado, os círculos de invocação continuam canalizando
	/// Essência, Ouro e Mana. Essência e Ouro crescem com a fase mais alta vencida; a Mana enche
	/// <see cref="Mana.PerHour"/> por hora até o máximo, e o que passaria dele se perde. O acúmulo para
	/// em 12 horas. Uma vez por dia, a Canalização Rápida entrega 2 horas na hora.
	///
	/// O relógio entra como parâmetro (<c>now</c>): os testes escolhem a hora sem mexer no sistema.
	/// A fração que não fecha uma unidade fica guardada para a próxima coleta.
	/// </summary>
	public static class Idle
	{
		public const double CapHours = 12;
		public const double QuickChannelHours = 2;

		public static double EssencePerHour(int highestStage) => 120 + 27 * highestStage;
		public static double GoldPerHour(int highestStage) => 2 + 0.1 * highestStage;

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
			var essence = player.IdleEssenceCarry + EssencePerHour(player.HighestStage) * hours;
			var gold = player.IdleGoldCarry + GoldPerHour(player.HighestStage) * hours;
			var mana = player.IdleManaCarry + Mana.PerHour * hours;
			var room = Mana.Room(player);
			var reward = new IdleReward((int)Math.Floor(essence), (int)Math.Floor(gold), Math.Min((int)Math.Floor(mana), room), hours);

			if (commit)
			{
				player.IdleEssenceCarry = essence - reward.Essence;
				player.IdleGoldCarry = gold - reward.Gold;
				player.IdleManaCarry = reward.Mana < room ? mana - reward.Mana : 0;
				player.Essence += reward.Essence;
				player.Gold += reward.Gold;
				player.Mana += reward.Mana;
			}

			return reward;
		}
	}
}
