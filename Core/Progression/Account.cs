using System;
using System.Collections.Generic;
using System.Linq;
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

		/// <summary>
		/// Os retratos que a conta pode usar: cada variante que ela tem (na coleção ou no Baú) e, de quem
		/// ela tem uma cópia desperta, também a forma desperta. Na ordem em que chegaram à conta.
		/// </summary>
		public static IReadOnlyList<(string Summon, bool Awakened)> Avatars(PlayerState player) => player.Monsters
			.OrderBy(m => m.Id)
			.SelectMany(m => m.Awakened ? new[] { (m.SummonId, false), (m.SummonId, true) } : new[] { (m.SummonId, false) })
			.Distinct()
			.ToList();

		/// <summary>Troca o retrato da conta por um de <see cref="Avatars"/>; outro qualquer é recusado.</summary>
		public static bool SetAvatar(PlayerState player, string summonId, bool awakened)
		{
			if (!Avatars(player).Contains((summonId, awakened)))
				return false;
			player.Avatar = summonId;
			player.AvatarAwakened = awakened;
			return true;
		}

		/// <summary>Experiência para sair de <paramref name="level"/> e chegar ao próximo (a conta ganha o mesmo que cada monstro).</summary>
		public static int ExperienceToNext(int level) => 300 * level;

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
