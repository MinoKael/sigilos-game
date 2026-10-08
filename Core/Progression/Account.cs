using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
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
		/// Até onde a coleção cresce com a Expansão de Coleção da Loja (começa em
		/// <see cref="PlayerState.StartingCollectionCapacity"/>). Chegou aqui, a oferta esgota.
		/// </summary>
		public const int MaxCollectionCapacity = 500;

		/// <summary>
		/// Os retratos que a conta pode usar: o padrão (<see cref="SpecialAvatars.Default"/>); cada variante
		/// que ela tem (na coleção ou no Baú) e, de quem ela tem uma cópia desperta, também a forma desperta;
		/// depois, os liberados pelo correio (<see cref="PlayerState.AvatarUnlocks"/>), de monstro ou especiais.
		/// Na ordem em que chegaram à conta.
		/// </summary>
		public static IReadOnlyList<AccountAvatar> Avatars(PlayerState player) => new[] { new AccountAvatar(SpecialAvatars.Default, false, AvatarKind.Special) }
			.Concat(player.Monsters
				.Where(m => !m.IsInfusionCore)
				.OrderBy(m => m.Id)
				.SelectMany(m => m.Awakened ? new[] { (m.SummonId, false), (m.SummonId, true) } : new[] { (m.SummonId, false) })
				.Select(a => new AccountAvatar(a.Item1, a.Item2, AvatarKind.Summon)))
			.Concat(player.AvatarUnlocks.Select(Unlocked))
			.Distinct()
			.ToList();

		/// <summary>O retrato de agora; sem escolha (ou com um que não está mais em <see cref="Avatars"/>, como o de um monstro solto), o padrão.</summary>
		public static AccountAvatar Current(PlayerState player)
		{
			var current = player.Avatar is { } id ? Of(id, player.AvatarAwakened) : default;
			return player.Avatar != null && Avatars(player).Contains(current) ? current : new AccountAvatar(SpecialAvatars.Default, false, AvatarKind.Special);
		}

		/// <summary>
		/// Completa, ao abrir a conta, o que um save anterior aos campos não tem: o começo da conta passa a ser
		/// agora (<see cref="PlayerState.Started"/>) e o recorde do andar mais fundo de cada Masmorra ganha a
		/// equipe (<see cref="Records.FillDeepestTeams"/>). O que já existe não muda.
		/// </summary>
		public static void Open(PlayerState player, GameDatabase database, DateTime now)
		{
			player.Started ??= now;
			Records.FillDeepestTeams(player, database);
		}

		private const string AwakenedSuffix = ":awakened";

		/// <summary>O tipo sai do id: o que está em <see cref="SpecialAvatars"/> é especial (e não tem forma desperta); o resto, monstro.</summary>
		private static AccountAvatar Of(string id, bool awakened) => SpecialAvatars.Has(id)
			? new AccountAvatar(id, false, AvatarKind.Special)
			: new AccountAvatar(id, awakened, AvatarKind.Summon);

		private static AccountAvatar Unlocked(string key) =>
			key.EndsWith(AwakenedSuffix) ? Of(key[..^AwakenedSuffix.Length], true) : Of(key, false);

		/// <summary>Libera um retrato sem ter o monstro, ou um especial (presente do correio). Repetir não faz nada.</summary>
		public static void UnlockAvatar(PlayerState player, string id, bool awakened)
		{
			var key = awakened && !SpecialAvatars.Has(id) ? id + AwakenedSuffix : id;
			if (!player.AvatarUnlocks.Contains(key))
				player.AvatarUnlocks.Add(key);
		}

		/// <summary>Troca o retrato da conta por um de <see cref="Avatars"/>; outro qualquer é recusado. O padrão volta a ser nulo no save.</summary>
		public static bool SetAvatar(PlayerState player, string id, bool awakened)
		{
			var avatar = Of(id, awakened);
			if (!Avatars(player).Contains(avatar))
				return false;
			player.Avatar = avatar.Id == SpecialAvatars.Default ? null : avatar.Id;
			player.AvatarAwakened = avatar.Awakened;
			return true;
		}

		/// <summary>Experiência para sair de <paramref name="level"/> e chegar ao próximo (a conta ganha o mesmo que cada monstro).</summary>
		public static int ExperienceToNext(int level) => 300 * level;

		/// <summary>
		/// Soma experiência; cada nível ganho dá Ouro e o prêmio de marco dele (<see cref="Milestones.ForAccountLevel"/>),
		/// e a Mana enche. Devolve os níveis ganhos.
		/// </summary>
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
				Milestones.Grant(player, Milestones.ForAccountLevel(player.AccountLevel));
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
