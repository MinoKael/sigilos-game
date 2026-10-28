using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O marco de chegar a 6★ (GDD, seção 12), pago por carta (<see cref="Letters"/>) na hora da evolução. O primeiro
	/// homúnculo de cada elemento a chegar lá paga <see cref="Milestones.FirstSixStar"/>; os outros, <see cref="Milestones.NextSixStar"/>.
	/// "Primeiro do elemento" vale uma vez por conta, mesmo que o 6★ seja solto depois (<see cref="PlayerState.SixStarElements"/>),
	/// e não vale para o elemento em que a conta já tinha um 6★ quando o marco chegou (ninguém é pago de trás para frente).
	/// </summary>
	public static class SixStar
	{
		/// <summary>
		/// <paramref name="monster"/> acabou de evoluir: se chegou a 6★, manda a carta do marco e a devolve. Nulo, sem mudar
		/// nada, se não chegou, se o monstro não está nos dados ou se não há o que pagar.
		/// </summary>
		public static Letter? Reached(PlayerState player, GameDatabase database, OwnedSummon monster, DateTime now)
		{
			if (monster.Stars < Growth.MaxStars || !database.Knows(monster.SummonId))
				return null;

			var element = database.Summon(monster.SummonId).Element;
			var name = element.ToString();
			var first = !player.SixStarElements.Contains(name) && !player.Monsters.Any(other => other != monster && other.Stars >= Growth.MaxStars && !other.IsInfusionCore
				&& database.Knows(other.SummonId) && database.Summon(other.SummonId).Element == element);
			if (!player.SixStarElements.Contains(name))
				player.SixStarElements.Add(name);

			return first
				? Letters.Send(player, Letters.SixStarFirst, now, Milestones.FirstSixStar, monster.SummonId, name)
				: Letters.Send(player, Letters.SixStar, now, Milestones.NextSixStar, monster.SummonId);
		}
	}
}
