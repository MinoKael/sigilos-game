using System;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O que um save enviado pelo jogo não pode ter mudado por conta própria: o que só as ações do servidor
	/// (<see cref="ServerActions"/>) fazem. O servidor confere cada envio contra o save que já guarda.
	/// </summary>
	public static class SaveAudit
	{
		/// <summary>
		/// Nulo se o envio vale; senão, o motivo. <paramref name="previous"/> é o save guardado (nulo na primeira
		/// vez, que vale como veio). <paramref name="replacement"/>: o envio troca a conta por uma nova (outra versão ou outra data de
		/// começo) e só vale se parece uma conta que nunca invocou nem melhorou runas.
		/// </summary>
		public static string? Check(PlayerState next, PlayerState? previous, bool replacement, AuditContext? context = null)
		{
			if (replacement)
			{
				if (next.TotalPulls != 0 || next.PullsSinceFiveStar != 0)
					return "pulls_forged";

				if (!next.Runes.All(rune => RuneAudit.IsFresh(rune)))
					return "rune_forged";

				// Uma conta nova pode ter jogado um pouco antes do primeiro envio: o crescimento cabe no que o tempo permite.
				return context == null ? null : MonsterAudit.Structure(next, null, context.Database) ?? MonsterAudit.Spending(next, null, context with { Since = TimeSpan.FromSeconds(10) });
			}

			// A primeira vez da conta na nuvem leva o save que já existe no aparelho: não há o que comparar.
			if (previous == null)
				return null;

			if (next.TotalPulls != previous.TotalPulls || next.PullsSinceFiveStar != previous.PullsSinceFiveStar)
				return "pulls_changed";

			var before = previous.Runes.ToDictionary(rune => rune.Id);
			foreach (var rune in next.Runes)
			{
				if (before.TryGetValue(rune.Id, out var old))
				{
					if (Frozen(rune) != Frozen(old))
						return "rune_changed";
				}
				else if (rune.Id < previous.NextRuneId || !RuneAudit.IsFresh(rune))
					return "rune_forged";
			}

			if (context == null)
				return null;

			return MonsterAudit.Structure(next, previous, context.Database)
				?? MonsterAudit.Spending(next, previous, context)
				?? MonsterAudit.Points(next, previous, context.Database);
		}

		/// <summary>A runa sem o que o jogador mexe sozinho (cadeado e quem a usa).</summary>
		private static string Frozen(Rune rune)
		{
			var copy = rune.Clone();
			copy.Locked = false;
			copy.EquippedOn = null;
			return JsonSerializer.Serialize(copy, GameDatabase.JsonOptions);
		}
	}

	/// <summary>Se uma runa tem a cara de uma que acabou de cair: +0, sorteios dentro das faixas, nada de pedra nem melhora.</summary>
	public static class RuneAudit
	{
		private const double Slack = 1e-6;

		public static bool IsFresh(Rune rune)
		{
			if (rune.Level != 0 || rune.Grade is < 1 or > RuneRules.MaxGrade || rune.Slot is < 1 or > RuneRules.Slots)
				return false;

			if (!RuneRules.MainOptions(rune.Slot).Contains(rune.Main) || rune.Substats.Count > RuneRules.MaxSubstats || rune.EquippedOn is < 0)
				return false;

			var seen = new System.Collections.Generic.HashSet<RuneStat>();
			if (rune.Innate != null)
			{
				if (!Legal(rune, rune.Innate) || !seen.Add(rune.Innate.Stat))
					return false;
			}

			foreach (var substat in rune.Substats)
			{
				if (!Legal(rune, substat) || !seen.Add(substat.Stat))
					return false;
			}

			return true;
		}

		private static bool Legal(Rune rune, RuneSubstat substat)
		{
			if (!RuneRules.CanBeSubstat(rune.Slot, rune.Main, substat.Stat) || substat.Rolls.Count != 1 || substat.Grind != 0 || substat.Enchanted || substat.Original != null)
				return false;

			var roll = substat.Rolls[0];
			var (min, max) = RuneRules.SubstatRange(substat.Stat, rune.Grade);
			return roll.Level == 0 && roll.Amount >= min - Slack && roll.Amount <= max + Slack;
		}
	}
}
