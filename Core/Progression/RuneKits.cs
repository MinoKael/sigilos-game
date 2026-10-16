using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O kit de sigilos (prêmio da fase 20): um sigilo Lendário 6★ para cada um dos 6 espaços, do conjunto que o
	/// jogador escolhe ao abrir. Saem como os de drop (+0, principal e subatributos sorteados).
	/// </summary>
	public static class RuneKits
	{
		/// <summary>As estrelas dos sigilos do kit.</summary>
		public const int Grade = RuneRules.MaxGrade;

		public static bool HasPending(PlayerState player) => player.PendingRuneKits > 0;

		/// <summary>Abre um kit no conjunto escolhido. Devolve os 6 sigilos; vazio se não há kit ou o conjunto não está em jogo.</summary>
		public static IReadOnlyList<Rune> Open(Random random, PlayerState player, RuneSet set)
		{
			if (player.PendingRuneKits <= 0 || !RuneSets.IsActive(set))
				return Array.Empty<Rune>();

			player.PendingRuneKits--;
			return Enumerable.Range(1, RuneRules.Slots)
				.Select(slot => RuneInventory.Create(random, player, Grade, new[] { set }, RuneRarity.Legendary, slot))
				.ToList();
		}
	}
}
