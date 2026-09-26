using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Evoluir (GDD, seção 10): as estrelas naturais são só o começo, e todo monstro chega a 6★. No
	/// nível máximo das estrelas de agora, paga Essência e Fragmentos, ganha uma estrela e volta ao
	/// nível 1, com nível máximo 5 acima (<see cref="Growth.MaxLevel"/>).
	/// </summary>
	public static class Evolution
	{
		/// <summary>O preço de sair de <paramref name="stars"/> para a próxima estrela.</summary>
		public static (int Essence, int Fragments) Cost(int stars) => stars switch
		{
			1 => (2_000, 5),
			2 => (5_000, 10),
			3 => (15_000, 20),
			4 => (40_000, 40),
			_ => (100_000, 80),
		};

		/// <summary>Está no nível máximo e ainda não é 6★ (sem olhar o preço).</summary>
		public static bool IsReady(OwnedSummon monster) => monster.Stars < Growth.MaxStars && Leveling.IsMaxLevel(monster);

		public static bool CanEvolve(PlayerState player, OwnedSummon monster)
		{
			var (essence, fragments) = Cost(monster.Stars);
			return IsReady(monster) && player.Essence >= essence && player.Fragments >= fragments;
		}

		public static bool Evolve(PlayerState player, OwnedSummon monster)
		{
			if (!CanEvolve(player, monster))
				return false;

			var (essence, fragments) = Cost(monster.Stars);
			player.Essence -= essence;
			player.Fragments -= fragments;
			monster.Stars++;
			monster.Level = 1;
			monster.Experience = 0;
			return true;
		}
	}
}
