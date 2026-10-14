using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Nível de cada monstro, até o máximo das estrelas dele (<see cref="Growth.MaxLevel"/>). A
	/// experiência de cada nível segue a tabela de referência (GDD): cada estrela tem a sua coluna. Como
	/// evoluir mantém o nível, de cada estrela acima da natural só se sobem os 5 últimos níveis: um 3★
	/// natural chega ao 6★ nível 40 com 858.795 de experiência. Vem de duas fontes: toda vitória dá
	/// experiência a quem lutou, e a Essência pode ser infundida, <see cref="ExperiencePerEssence"/> de
	/// experiência por Essência. No nível máximo a experiência para: o próximo passo é evoluir
	/// (<see cref="Evolution"/>).
	/// </summary>
	public static class Leveling
	{
		public const int ExperiencePerEssence = 10;

        /// <summary>Experiência para sair de cada nível e chegar ao próximo, uma linha por estrela.</summary>
        private static readonly int[][] Table =
        {
			new[] { 1380, 1548, 1737, 1950, 2184, 2454, 2754, 3087, 3465, 3888, 4365, 4893, 5493, 6162 },
			new[] { 1656, 1857, 2085, 2337, 2625, 2943, 3306, 3705, 4158, 4665, 5235, 5874, 6591, 7395, 8295, 9309, 10443, 11718, 13269 },
			new[] { 1986, 2229, 2502, 2808, 3147, 3534, 3963, 4449, 4989, 5598, 6282, 7050, 7908, 8871, 9957, 11169, 12534, 14061, 15921, 18027, 20406, 23109, 26160, 29886 },
			new[] { 2388, 2676, 3006, 3372, 3783, 4245, 4761, 5343, 5994, 6729, 7545, 8469, 9501, 10659, 11961, 13419, 15057, 16893, 19128, 21657, 24516, 27762, 31428, 35907, 41019, 46857, 53532, 61158, 70485 },
			new[] { 2856, 3204, 3597, 4032, 4527, 5079, 5697, 6393, 7176, 8046, 9030, 10134, 11367, 12756, 14310, 16056, 18018, 20214, 22884, 25914, 29337, 33216, 37605, 42963, 49080, 56070, 64050, 73176, 84339, 97212, 112044, 129144, 148851, 171564 },
			new[] { 3450, 3870, 4341, 4872, 5469, 6132, 6882, 7722, 8664, 9720, 10905, 12237, 13728, 15405, 17286, 19392, 21756, 24414, 27642, 31293, 35433, 40113, 45420, 51888, 59274, 67716, 77358, 88374, 101862, 117402, 135321, 155970, 179772, 207204, 228255, 251448, 276996, 305136, 336138 }
		};

        public static int MaxLevel(OwnedSummon monster) => Growth.MaxLevel(monster.Stars);

		public static bool IsMaxLevel(OwnedSummon monster) => monster.Level >= MaxLevel(monster);

		/// <summary>Experiência para sair de <paramref name="level"/> e chegar ao próximo, nestas estrelas. 0 no máximo.</summary>
		public static int ExperienceToNext(int stars, int level)
		{
			var row = Table[Math.Clamp(stars, 1, Growth.MaxStars) - 1];
			return level >= 1 && level <= row.Length ? row[level - 1] : 0;
		}

		public static int ExperienceToNext(OwnedSummon monster) => ExperienceToNext(monster.Stars, monster.Level);

		/// <summary>Experiência que falta para o próximo nível. 0 no nível máximo.</summary>
		public static int Missing(OwnedSummon monster) =>
			IsMaxLevel(monster) ? 0 : ExperienceToNext(monster) - monster.Experience;

		/// <summary>Experiência que falta até o nível máximo das estrelas de agora.</summary>
		public static int MissingToMax(OwnedSummon monster)
		{
			var total = Missing(monster);
			for (var level = monster.Level + 1; level < MaxLevel(monster); level++)
				total += ExperienceToNext(monster.Stars, level);
			return total;
		}

		/// <summary>Essência para infundir <paramref name="experience"/> de experiência.</summary>
		public static int EssenceFor(int experience) => (experience + ExperiencePerEssence - 1) / ExperiencePerEssence;

		/// <summary>Soma experiência e sobe quantos níveis ela pagar. Devolve os níveis ganhos.</summary>
		public static int AddExperience(OwnedSummon monster, int amount)
		{
			var gained = 0;
			monster.Experience += Math.Max(0, amount);
			while (!IsMaxLevel(monster) && monster.Experience >= ExperienceToNext(monster))
			{
				monster.Experience -= ExperienceToNext(monster);
				monster.Level++;
				gained++;
			}

			if (IsMaxLevel(monster))
				monster.Experience = 0;
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
		/// Infunde Essência como experiência, até <paramref name="essence"/>, nunca além do nível máximo
		/// das estrelas nem da Essência que o jogador tem. Devolve a Essência gasta.
		/// </summary>
		public static int Infuse(PlayerState player, OwnedSummon monster, int essence)
		{
			if (monster.IsInfusionCore)
				return 0;
			var spent = Math.Min(Math.Min(essence, player.Essence), EssenceFor(MissingToMax(monster)));
			if (spent <= 0)
				return 0;

			player.Essence -= spent;
			AddExperience(monster, spent * ExperiencePerEssence);
			return spent;
		}

		/// <summary>O nível a que <paramref name="essence"/> de Essência levaria o monstro, sem mudar nada (para a confirmação).</summary>
		public static int LevelAfter(OwnedSummon monster, int essence)
		{
			var level = monster.Level;
			var experience = monster.Experience + Math.Max(0, essence) * ExperiencePerEssence;
			while (level < MaxLevel(monster) && experience >= ExperienceToNext(monster.Stars, level))
			{
				experience -= ExperienceToNext(monster.Stars, level);
				level++;
			}

			return level;
		}

		/// <summary>Quanto de Essência leva até o próximo nível e até o máximo (a conta arredonda para cima).</summary>
		public static (int Next, int Max) InfuseCosts(OwnedSummon monster) => (EssenceFor(Missing(monster)), EssenceFor(MissingToMax(monster)));

		internal static IEnumerable<int> Row(int stars) => Table[Math.Clamp(stars, 1, Growth.MaxStars) - 1];

		/// <summary>Experiência total do nível 1 ao máximo destas estrelas.</summary>
		public static int TotalFor(int stars) => Row(stars).Sum();
	}
}
