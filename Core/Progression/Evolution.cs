using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Evoluir (GDD, seção 10): as estrelas naturais são só o começo, e todo monstro chega a 6★. No
	/// nível máximo das estrelas de agora, paga Essência e Fragmentos e ganha uma estrela. O nível
	/// fica: o monstro continua de onde estava, agora na faixa de atributos da estrela nova (os
	/// atributos sobem na hora, <see cref="Growth.Fraction"/>) e com 5 níveis a mais pela frente
	/// (<see cref="Growth.MaxLevel"/>).
	/// </summary>
	public static class Evolution
	{
		/// <summary>O preço de sair de <paramref name="stars"/> para a próxima estrela.</summary>
		public static int Cost(int stars) => stars switch
		{
			1 => 10,
			2 => 20,
			3 => 30,
			4 => 60,
			_ => 120,
		};

		/// <summary>Está no nível máximo e ainda não é 6★ (sem olhar o preço).</summary>
		public static bool IsReady(OwnedSummon monster) => monster.Stars < Growth.MaxStars && Leveling.IsMaxLevel(monster);

		public static bool CanEvolve(PlayerState player, OwnedSummon monster)
		{
			var fragments = Cost(monster.Stars);
			return IsReady(monster) && player.Fragments >= fragments;
		}

		public static bool Evolve(PlayerState player, OwnedSummon monster)
		{
			if (!CanEvolve(player, monster))
				return false;

			var fragments = Cost(monster.Stars);
			player.Fragments -= fragments;
			monster.Stars++;
			return true;
		}
	}
}
