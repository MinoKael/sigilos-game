using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Nível de cada monstro, até o máximo das estrelas dele (<see cref="Growth.MaxLevel"/>). A
	/// experiência de cada nível segue a tabela de Summoners War: cada estrela tem a sua coluna, e um
	/// 6★ do 1 ao 40 pede 1.005.420. Vem de duas fontes: toda vitória dá experiência a quem lutou, e a
	/// Essência pode ser infundida, <see cref="ExperiencePerEssence"/> de experiência por Essência.
	/// No nível máximo a experiência para: o próximo passo é evoluir (<see cref="Evolution"/>).
	/// </summary>
	public static class Leveling
	{
		public const int ExperiencePerEssence = 10;

		/// <summary>Experiência para sair de cada nível e chegar ao próximo, uma linha por estrela.</summary>
		private static readonly int[][] Table =
		{
			new[] { 460, 516, 579, 650, 728, 818, 918, 1029, 1155, 1296, 1455, 1631, 1831, 2054 },
			new[] { 552, 619, 695, 779, 875, 981, 1102, 1235, 1386, 1555, 1745, 1958, 2197, 2465, 2765, 3103, 3481, 3906, 4423 },
			new[] { 662, 743, 834, 936, 1049, 1178, 1321, 1483, 1663, 1866, 2094, 2350, 2636, 2957, 3319, 3723, 4178, 4687, 5307, 6009, 6802, 7703, 8720, 9962 },
			new[] { 796, 892, 1002, 1124, 1261, 1415, 1587, 1781, 1998, 2243, 2515, 2823, 3167, 3553, 3987, 4473, 5019, 5631, 6376, 7219, 8172, 9254, 10476, 11969, 13673, 15619, 17844, 20386, 23495 },
			new[] { 952, 1068, 1199, 1344, 1509, 1693, 1899, 2131, 2392, 2682, 3010, 3378, 3789, 4252, 4770, 5352, 6006, 6738, 7628, 8638, 9779, 11072, 12535, 14321, 16360, 18690, 21350, 24392, 28113, 32404, 37348, 43048, 49617, 57188 },
			new[] { 1150, 1290, 1447, 1624, 1823, 2044, 2294, 2574, 2888, 3240, 3635, 4079, 4576, 5135, 5762, 6464, 7252, 8138, 9214, 10431, 11811, 13371, 15140, 17296, 19758, 22572, 25786, 29458, 33954, 39134, 45107, 51990, 59924, 69068, 76085, 83816, 92332, 101712, 112046 },
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
			var spent = Math.Min(Math.Min(essence, player.Essence), EssenceFor(MissingToMax(monster)));
			if (spent <= 0)
				return 0;

			player.Essence -= spent;
			AddExperience(monster, spent * ExperiencePerEssence);
			return spent;
		}

		/// <summary>Quanto de Essência leva até o próximo nível e até o máximo (a conta arredonda para cima).</summary>
		public static (int Next, int Max) InfuseCosts(OwnedSummon monster) => (EssenceFor(Missing(monster)), EssenceFor(MissingToMax(monster)));

		internal static IEnumerable<int> Row(int stars) => Table[Math.Clamp(stars, 1, Growth.MaxStars) - 1];

		/// <summary>Experiência total do nível 1 ao máximo destas estrelas.</summary>
		public static int TotalFor(int stars) => Row(stars).Sum();
	}
}
