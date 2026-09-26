using System;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O nível da conta (GDD, seção 12), de 1 a <see cref="MaxLevel"/>. Toda vitória dá a experiência da
	/// luta também à conta, na Campanha e nas Masmorras. Cada nível dá Ouro, enche a Mana e aumenta a
	/// Mana máxima (<see cref="Mana.Max"/>: 60 no nível 1, 120 no 60).
	/// </summary>
	public static class Account
	{
		public const int LevelUpGold = 20;
		public const int MaxLevel = 60;

		/// <summary>Experiência para sair de <paramref name="level"/> e chegar ao próximo.</summary>
		public static int ExperienceToNext(int level) => 100 * level;

		/// <summary>Soma experiência; cada nível ganho dá Ouro, e a Mana enche. Devolve os níveis ganhos.</summary>
		public static int GiveExperience(PlayerState player, int amount)
		{
			if (player.AccountLevel >= MaxLevel) return 0;
			var gained = 0;
			player.AccountExperience += Math.Max(0, amount);
			while (player.AccountLevel < MaxLevel && player.AccountExperience >= ExperienceToNext(player.AccountLevel))
			{
				player.AccountExperience -= ExperienceToNext(player.AccountLevel);
				player.AccountLevel++;
				player.Gold += LevelUpGold;
				gained++;
			}

			if (player.AccountLevel >= MaxLevel)
				player.AccountExperience = 0;
			if (gained > 0)
				Mana.Refill(player);
			return gained;
		}
	}
}
