using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Player;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// O que a Gema de Reavaliação desfaz numa runa: a melhora (volta a +0), os subatributos que vieram
	/// nas melhoras, os sorteios a mais dos que nasceram com ela, os bônus de Pedra de Afiar e o
	/// encantamento (volta o subatributo de antes). <see cref="EnchantKept"/>: a runa foi encantada antes
	/// de o jogo guardar o subatributo trocado, e o encantado fica, só com o sorteio da gema.
	/// </summary>
	public sealed record ReappraisalChanges(
		int Level,
		IReadOnlyList<RuneStat> RemovedSubstats,
		int ExtraRolls,
		int Grinds,
		(RuneStat From, RuneStat To)? Enchant,
		bool EnchantKept)
	{
		/// <summary>Há algo a desfazer: sem isso, a gema não tem o que fazer na runa.</summary>
		public bool Any => Level > 0 || RemovedSubstats.Count > 0 || ExtraRolls > 0 || Grinds > 0 || Enchant != null;
	}

	/// <summary>
	/// A Gema de Reavaliação (comprada na Loja): devolve a runa ao estado em que caiu. Gasta uma gema; a
	/// Essência das melhoras não volta. O nativo nunca muda, então fica como está.
	/// </summary>
	public static class RuneReappraisal
	{
		public static ReappraisalChanges Preview(Rune rune)
		{
			var removed = new List<RuneStat>();
			var extra = 0;
			var grinds = 0;
			(RuneStat, RuneStat)? enchant = null;
			var kept = false;
			foreach (var substat in rune.Substats)
			{
				if (substat.Grind > 0)
					grinds++;

				var origin = Origin(substat);
				if (substat.Enchanted)
				{
					if (substat.Original == null)
						kept = true;
					else
						enchant = (substat.Stat, substat.Original.Stat);
				}

				if (origin.Rolls.Count == 0 || origin.Rolls[0].Level > 0)
				{
					if (!(substat.Enchanted && substat.Original == null))
						removed.Add(origin.Stat);
					continue;
				}

				extra += origin.Rolls.Count(r => r.Level > 0);
			}

			return new ReappraisalChanges(rune.Level, removed, extra, grinds, enchant, kept);
		}

		public static bool CanReappraise(PlayerState player, Rune rune) => player.ReappraisalGems > 0 && Preview(rune).Any;

		/// <summary>Gasta uma gema e devolve a runa ao estado em que caiu. Falso se não havia gema ou nada a desfazer.</summary>
		public static bool Reappraise(PlayerState player, Rune rune)
		{
			if (!CanReappraise(player, rune))
				return false;

			player.ReappraisalGems--;
			var substats = new List<RuneSubstat>();
			foreach (var substat in rune.Substats)
			{
				if (substat.Enchanted && substat.Original == null)
				{
					// Encantada antes de o jogo guardar o original: o encantado fica, sem bônus de pedra.
					substats.Add(new RuneSubstat { Stat = substat.Stat, Rolls = { substat.Rolls[0] }, Enchanted = true });
					continue;
				}

				var origin = Origin(substat);
				if (origin.Rolls.Count == 0 || origin.Rolls[0].Level > 0)
					continue;
				substats.Add(new RuneSubstat { Stat = origin.Stat, Rolls = { origin.Rolls[0] } });
			}

			rune.Substats = substats;
			rune.Level = 0;
			return true;
		}

		/// <summary>O subatributo antes do encantamento (o próprio, se não foi encantado).</summary>
		private static RuneSubstat Origin(RuneSubstat substat) => substat.Enchanted && substat.Original != null ? substat.Original : substat;
	}
}
