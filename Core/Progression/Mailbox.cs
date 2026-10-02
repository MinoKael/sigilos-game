using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O correio do Santuário: cartas que o servidor manda com recompensas. Coletar soma as recompensas e
	/// anota a carta no save (<see cref="PlayerState.ClaimedMail"/>) antes de o servidor saber: se a conexão
	/// cair no meio, a recompensa não se perde, e a mesma carta nunca é coletada duas vezes. A Mana pode
	/// passar do máximo, como a da Loja.
	/// </summary>
	public static class Mailbox
	{
		/// <summary>As cartas que este save ainda não coletou.</summary>
		public static IReadOnlyList<Mail> Unclaimed(PlayerState player, IEnumerable<Mail> mail) =>
			mail.Where(m => !player.ClaimedMail.Contains(m.Id)).ToList();

		/// <summary>Soma as recompensas e anota a carta. Falso se ela já tinha sido coletada: nada muda.</summary>
		public static bool Claim(PlayerState player, Mail mail)
		{
			if (player.ClaimedMail.Contains(mail.Id))
				return false;

			foreach (var (item, amount) in mail.Rewards)
				Give(player, item, amount);
			player.ClaimedMail.Add(mail.Id);
			return true;
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
			}
		}
	}
}
