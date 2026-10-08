using System;
using System.Collections.Generic;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Regras da Exploração Estelar (GDD, seção 11): o percurso pelas 88 constelações abre depois de uma
	/// fase da Campanha, e cada constelação depois da anterior. O percurso recomeça no dia 1 de cada mês
	/// (pelo relógio do aparelho), e a cada mês uma das três Explorações traz os desafios, em rodízio. A
	/// primeira vitória de cada constelação no mês paga a recompensa dela, sempre a mesma; vencer de novo
	/// no mesmo mês não paga nada. A luta não custa Mana: o que limita é o percurso, que só se faz uma
	/// vez por mês.
	/// </summary>
	public static class Exploration
	{
		/// <summary>Quantas Explorações se revezam.</summary>
		public const int Rotation = 3;

		/// <summary>O mês do calendário ("2026-10"): a chave do progresso.</summary>
		public static string MonthKey(DateTime now) => $"{now.Year:D4}-{now.Month:D2}";

		/// <summary>
		/// A Exploração do mês (de 0 a 2): o rodízio conta os meses desde o ano 0, então todo aparelho e
		/// todo save chegam à mesma, e cada mês traz a seguinte.
		/// </summary>
		public static int VariationOf(DateTime now) => (now.Year * 12 + now.Month - 1) % Rotation;

		/// <summary>Quando o percurso recomeça e a próxima Exploração entra: meia-noite do dia 1 do mês seguinte.</summary>
		public static DateTime NextRotation(DateTime now) => new DateTime(now.Year, now.Month, 1).AddMonths(1);

		/// <summary>Aberta depois da fase dela, ou se a conta já venceu alguma constelação (nunca fecha de novo).</summary>
		public static bool IsOpen(PlayerState player, ExplorationDefinition exploration) =>
			exploration.Constellations.Count > 0 && (player.HighestStage >= exploration.UnlockStage || player.ExplorationBest > 0);

		/// <summary>Constelações vencidas neste mês. Num mês novo é 0, mesmo antes de o save ser renovado.</summary>
		public static int Cleared(PlayerState player, DateTime now) =>
			player.ExplorationMonth == MonthKey(now) ? player.ExplorationCleared : 0;

		/// <summary>Num mês novo, o percurso recomeça. Devolve verdadeiro quando renovou.</summary>
		public static bool Renew(PlayerState player, DateTime now)
		{
			var month = MonthKey(now);
			if (player.ExplorationMonth == month)
				return false;

			player.ExplorationMonth = month;
			player.ExplorationCleared = 0;
			return true;
		}

		/// <summary>A constelação já foi vencida neste mês.</summary>
		public static bool IsCleared(PlayerState player, int number, DateTime now) => number <= Cleared(player, now);

		/// <summary>Aberta: a Exploração está aberta e a constelação é uma das vencidas no mês ou a seguinte.</summary>
		public static bool IsUnlocked(PlayerState player, ExplorationDefinition exploration, int number, DateTime now) =>
			IsOpen(player, exploration) && number >= 1 && number <= Math.Min(exploration.Constellations.Count, Cleared(player, now) + 1);

		/// <summary>Se a luta pode começar: só pede a constelação aberta (não custa Mana nem solta runa).</summary>
		public static EntryProblem Check(PlayerState player, ExplorationDefinition exploration, int number, DateTime now) =>
			IsUnlocked(player, exploration, number, now) ? EntryProblem.None : EntryProblem.Locked;

		/// <summary>O que a primeira vitória do mês na constelação entrega de marco.</summary>
		public static Prize PrizeOf(ConstellationReward reward) => new(reward.Legendary, reward.LightDark, reward.Cores);

		/// <summary>
		/// A vitória na constelação <paramref name="number"/>. A primeira do mês (a constelação seguinte às
		/// vencidas) paga a recompensa inteira; repetir uma já vencida não paga nada. Uma vitória que
		/// termina num mês novo, numa constelação que o mês novo ainda não abriu, também não paga: o
		/// percurso já recomeçou.
		/// </summary>
		public static VictoryReward ApplyVictory(PlayerState player, ExplorationDefinition exploration, int number, DateTime now)
		{
			Renew(player, now);
			var firstClear = number == player.ExplorationCleared + 1 && number <= exploration.Constellations.Count;
			if (!firstClear)
				return new VictoryReward(0, 0, 0, 0, 0, false, null, null, Array.Empty<RuneTool>(), Array.Empty<int>(), 0);

			var reward = exploration.Constellation(number).Reward;
			player.ExplorationCleared = number;
			player.ExplorationBest = Math.Max(player.ExplorationBest, number);
			player.Gold += reward.Gold;
			player.Essence += reward.Essence;
			player.Scrolls += reward.Scrolls;

			var levelUps = Leveling.GiveExperience(player, Teams.Of(player, Teams.Exploration), reward.Experience);
			var level = player.AccountLevel;
			var account = Account.GiveExperience(player, reward.Experience);
			var prize = PrizeOf(reward);
			Milestones.Grant(player, prize);
			prize += Milestones.ForAccountLevels(level, player.AccountLevel);

			return new VictoryReward(0, reward.Scrolls, reward.Gold, reward.Essence + account.Essence, reward.Experience, true, null, null, Array.Empty<RuneTool>(), levelUps, account.Levels, prize);
		}

		/// <summary>O total que o percurso inteiro paga num mês: a soma das recompensas das 88 constelações.</summary>
		public static ConstellationReward MonthlyTotal(ExplorationDefinition exploration)
		{
			var total = new ConstellationReward();
			foreach (var constellation in exploration.Constellations)
			{
				var r = constellation.Reward;
				total = total with
				{
					Essence = total.Essence + r.Essence,
					Gold = total.Gold + r.Gold,
					Scrolls = total.Scrolls + r.Scrolls,
					Experience = total.Experience + r.Experience,
					Legendary = total.Legendary + r.Legendary,
					LightDark = total.LightDark + r.LightDark,
					Cores = total.Cores + r.Cores,
				};
			}

			return total;
		}

		/// <summary>As constelações de uma faixa do céu, com o andar de cada uma.</summary>
		public static IEnumerable<(int Number, ConstellationDefinition Constellation)> Of(ExplorationDefinition exploration, Hemisphere hemisphere)
		{
			for (var i = 0; i < exploration.Constellations.Count; i++)
			{
				if (exploration.Constellations[i].Hemisphere == hemisphere)
					yield return (i + 1, exploration.Constellations[i]);
			}
		}
	}
}
