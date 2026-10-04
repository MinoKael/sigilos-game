using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O correio do Santuário: cartas que o servidor manda com recompensas. Coletar soma as recompensas e
	/// anota a carta no save (<see cref="PlayerState.ClaimedMail"/>) antes de o servidor saber: se a conexão
	/// cair no meio, a recompensa não se perde, e a mesma carta nunca é coletada duas vezes. A Mana pode
	/// passar do máximo, como a da Loja. Os presentes (<see cref="Mail.Gifts"/>) entram junto: monstros novos
	/// na coleção (ou no Baú), runas no inventário (mesmo cheio, como as da vitória) e retratos liberados
	/// (<see cref="Account.UnlockAvatar"/>). Monstro que o jogo não conhece não entra.
	/// </summary>
	public static class Mailbox
	{
		/// <summary>As cartas que este save ainda não coletou.</summary>
		public static IReadOnlyList<Mail> Unclaimed(PlayerState player, IEnumerable<Mail> mail) =>
			mail.Where(m => !player.ClaimedMail.Contains(m.Id)).ToList();

		/// <summary>
		/// Soma as recompensas e anota a carta. Falso se ela já tinha sido coletada: nada muda. Sem
		/// <paramref name="database"/>, os presentes não entram (só as moedas).
		/// </summary>
		public static bool Claim(PlayerState player, Mail mail, GameDatabase? database = null, Random? random = null)
		{
			if (player.ClaimedMail.Contains(mail.Id))
				return false;

			foreach (var (item, amount) in mail.Rewards)
				Give(player, item, amount);
			if (database != null)
			{
				random ??= new Random();
				foreach (var gift in mail.Gifts)
					Give(random, database, player, gift);
			}

			player.ClaimedMail.Add(mail.Id);
			return true;
		}

		private static void Give(Random random, GameDatabase database, PlayerState player, MailGift gift)
		{
			switch (gift.Kind)
			{
				case MailGiftKind.Monster when database.HasSummon(gift.Id):
					for (var i = 0; i < gift.Count; i++)
						Roster.Add(player, database.Summon(gift.Id));
					break;
				case MailGiftKind.Rune:
					for (var i = 0; i < gift.Count; i++)
						RuneInventory.Create(random, player, gift.Grade, gift.Set is { } set ? new[] { set } : null, gift.Rarity);
					break;
				case MailGiftKind.Avatar when database.HasSummon(gift.Id):
					Account.UnlockAvatar(player, gift.Id, gift.Awakened);
					break;
			}
		}

		private static void Give(PlayerState player, MailItem item, int amount)
		{
			switch (item)
			{
				case MailItem.Gold:
					player.Gold += amount;
					break;
				case MailItem.Mana:
					player.Mana += amount;
					break;
				case MailItem.Scrolls:
					player.Scrolls += amount;
					break;
				case MailItem.Essence:
					player.Essence += amount;
					break;
				case MailItem.Fragments:
					player.Fragments += amount;
					break;
				case MailItem.ReappraisalGems:
					player.ReappraisalGems += amount;
					break;
				case MailItem.LightDarkScrolls:
					player.LightDarkScrolls += amount;
					break;
				case MailItem.LegendaryScrolls:
					player.LegendaryScrolls += amount;
					break;
				case MailItem.InfusionCores:
					Milestones.Grant(player, new Prize(InfusionCores: amount));
					break;
			}
		}
	}
}
