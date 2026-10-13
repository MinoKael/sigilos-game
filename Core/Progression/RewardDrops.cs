using System;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// As três categorias de drop que não podem vir juntas na mesma resolução de recompensa: o Núcleo de
	/// Infusão, o Pergaminho de Transmutação Alquímica (o Místico) e o Núcleo de Fragmento. Uma resolução entrega
	/// no máximo uma delas, ou nenhuma. Os outros drops (runas, pedras, Ouro, Essência) não entram aqui.
	/// </summary>
	public enum DropCategory
	{
		None,
		InfusionCore,
		Scroll,
		FragmentCore,
	}

	/// <summary>
	/// Onde a exclusividade das categorias de <see cref="DropCategory"/> mora: um sorteio só, com faixas em
	/// sequência (Núcleo, Pergaminho, Fragmento) sobre os 100%. Cada categoria mantém a chance individual que
	/// os dados dão a ela; o que sobra é "nenhum". Quem resolve a recompensa (Progression/Dungeons) chama
	/// isto uma vez por vitória, e nenhuma tela decide drop.
	/// </summary>
	public static class RewardDrops
	{
		/// <summary>Quantos Fragmentos vale um Núcleo de Fragmento.</summary>
		public const int FragmentCoreValue = 30;

		/// <summary>
		/// Sorteia a categoria da resolução. As chances são em % (0 a 100). Se a soma passa de 100, as faixas
		/// são reduzidas na mesma proporção, para o limite valer sem perder a razão entre elas.
		/// Vale para os drops sorteados: os marcos fixos (primeira vitória, nível da conta) pagam o que está nos
		/// dados, sem passar por aqui.
		/// </summary>
		public static DropCategory Roll(Random random, double coreChance, double scrollChance, double fragmentChance)
		{
			var roll = random.NextDouble() * 100;

			var total = coreChance + scrollChance + fragmentChance;
			var scale = total > 100 ? 100 / total : 1;

			var edge = coreChance * scale;
			if (roll < edge)
				return DropCategory.InfusionCore;

			edge += scrollChance * scale;
			if (roll < edge)
				return DropCategory.Scroll;

			edge += fragmentChance * scale;
			return roll < edge ? DropCategory.FragmentCore : DropCategory.None;
		}
	}
}
