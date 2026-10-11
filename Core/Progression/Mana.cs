using System;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Mana (GDD, seção 12): a vitória, na Campanha ou numa Masmorra, custa Mana; a derrota não custa
	/// nada, mas só começa a luta quem tem a Mana dela. A canalização (<see cref="Idle"/>) recarrega até
	/// o dobro do máximo, que vai de 100 no nível 1 da conta a 300 no nível 100. Só a Loja e a subida de
	/// nível da conta passam disso.
	/// </summary>
	public static class Mana
	{
		public const int BaseMax = 100;
		public const int MaxFromLevels = 200;

		/// <summary>Em quantas horas de canalização a Mana máxima do nível 1 enche, do zero.</summary>
		public const double RefillHours = 5;

		/// <summary>Uma de Mana a cada 3 minutos de canalização: a do nível 1 enche em <see cref="RefillHours"/>.</summary>
		public const double PerHour = BaseMax / RefillHours;

		/// <summary>+2 por nível da conta, e o nível 100 fecha nos 300 (+2).</summary>
		public static int Max(PlayerState player) =>
			BaseMax + MaxFromLevels * (Math.Clamp(player.AccountLevel, 1, Account.MaxLevel) - 1) / (Account.MaxLevel - 1);

		/// <summary>Quanto a canalização ainda pode pôr: até o dobro do máximo, nada com a Mana nele ou acima.</summary>
		public static int Room(PlayerState player) => Math.Max(0, (Max(player) * 2) - player.Mana);

		/// <summary>Enche até o máximo, sem tirar o que já passou dele.</summary>
		public static void Refill(PlayerState player) => player.Mana = Math.Max(player.Mana, Max(player));

		/// <summary>
		/// Se a luta pode começar: aberta e com a Mana da vitória na mão. Conteúdo que solta runa espera o
		/// inventário de runas ter espaço (<see cref="RuneInventory.Capacity"/>).
		/// </summary>
		public static EntryProblem Check(PlayerState player, bool unlocked, int cost, bool dropsRunes) =>
			!unlocked ? EntryProblem.Locked
			: dropsRunes && RuneInventory.IsFull(player) ? EntryProblem.RunesFull
			: player.Mana < cost ? EntryProblem.NoMana
			: EntryProblem.None;

		/// <summary>Cobra a Mana de uma vitória. Nunca deixa negativa.</summary>
		public static int Spend(PlayerState player, int cost)
		{
			var spent = Math.Min(cost, player.Mana);
			player.Mana -= spent;
			return spent;
		}
	}
}
