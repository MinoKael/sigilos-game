using System;
using System.Collections.Generic;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Nível de cada invocação, de 1 a 40. A experiência vem de duas fontes: toda vitória dá
	/// experiência ao time que lutou, e a Essência da ociosidade pode ser
	/// infundida, 1 Essência por ponto de experiência.
	/// </summary>
	public static class Leveling
	{
		public const int MaxLevel = Growth.MaxLevel;

		/// <summary>Experiência para sair de <paramref name="level"/> e chegar ao próximo.</summary>
		public static int ExperienceToNext(int level) => 20 * level + 30;

		/// <summary>Experiência que falta para o próximo nível. 0 no nível máximo.</summary>
		public static int Missing(OwnedSummon summon) =>
			summon.Level >= MaxLevel ? 0 : ExperienceToNext(summon.Level) - summon.Experience;

		/// <summary>Experiência que falta até o nível 40.</summary>
		public static int MissingToMax(OwnedSummon summon)
		{
			var total = Missing(summon);
			for (var level = summon.Level + 1; level < MaxLevel; level++)
				total += ExperienceToNext(level);
			return total;
		}

		/// <summary>Soma experiência e sobe quantos níveis ela pagar. Devolve os níveis ganhos.</summary>
		public static int AddExperience(OwnedSummon summon, int amount)
		{
			var gained = 0;
			summon.Experience += Math.Max(0, amount);
			while (summon.Level < MaxLevel && summon.Experience >= ExperienceToNext(summon.Level))
			{
				summon.Experience -= ExperienceToNext(summon.Level);
				summon.Level++;
				gained++;
			}

			if (summon.Level >= MaxLevel)
				summon.Experience = 0;
			return gained;
		}

		/// <summary>Experiência de vitória para cada monstro da equipe. Devolve os ids de quem subiu de nível.</summary>
		public static IReadOnlyList<int> GiveExperience(PlayerState player, IReadOnlyList<int> team, int amount)
		{
			var levelUps = new List<int>();
			foreach (var id in team)
			{
				if (player.Monster(id) is { Stored: false } monster && AddExperience(monster, amount) > 0)
					levelUps.Add(id);
			}

			return levelUps;
		}

		/// <summary>
		/// Infunde Essência como experiência, até <paramref name="essence"/> e nunca além do nível 40
		/// nem da Essência que o jogador tem. Devolve a Essência gasta.
		/// </summary>
		public static int Infuse(PlayerState player, OwnedSummon summon, int essence)
		{
			var spent = Math.Min(Math.Min(essence, player.Essence), MissingToMax(summon));
			if (spent <= 0)
				return 0;

			player.Essence -= spent;
			AddExperience(summon, spent);
			return spent;
		}
	}
}
