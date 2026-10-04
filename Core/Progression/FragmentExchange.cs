using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Summoning;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A troca de Fragmentos (GDD, seção 12): o caminho certo, sem sorte, para a 4★ que falta na equipe. Paga
	/// <see cref="Cost"/> Fragmentos e recebe uma cópia nova da variante escolhida, entre as 4★ de Fogo, Água
	/// e Vento (as de Luz e Trevas e as 5★ não se trocam: são da sorte e dos marcos).
	/// </summary>
	public static class FragmentExchange
	{
		public const int Cost = 300;

		/// <summary>As variantes que se trocam, na ordem das famílias e dos elementos.</summary>
		public static IReadOnlyList<SummonDefinition> Options(GameDatabase database) =>
			database.Summons.Where(s => s.Rarity == 4 && SummonRates.Allows(ScrollKind.Mystic, s.Element)).ToList();

		public static bool CanExchange(PlayerState player, GameDatabase database, string summonId) =>
			player.Fragments >= Cost && Options(database).Any(s => s.Id == summonId);

		/// <summary>Paga e entrega a cópia (na coleção, ou no Baú se ela está cheia). Nulo se não pode.</summary>
		public static SummonResult? Exchange(PlayerState player, GameDatabase database, string summonId)
		{
			if (!CanExchange(player, database, summonId))
				return null;

			player.Fragments -= Cost;
			return SummonRitual.Receive(player, database.Summon(summonId));
		}
	}
}
