namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Nível compartilhado (GDD, seção 10): todas as invocações lutam no mesmo nível, então testar
	/// qualquer time custa zero.
	///
	/// Simplificação do MVP: o GDD sobe as 5 invocações de maior nível e nivela as outras pela quinta.
	/// Com um nível único para a conta o efeito é o mesmo depois das 5 primeiras, sem a tela de escolher
	/// quem sobe.
	/// </summary>
	public static class SharedLevel
	{
		/// <summary>Teto da região 1. As regiões 2 e 3 sobem para 40 e 60.</summary>
		public const int RegionOneCap = 20;

		public const int EssencePerLevel = 60;

		/// <summary>Essência para ir de <paramref name="level"/> a <paramref name="level"/> + 1.</summary>
		public static int CostToRaise(int level) => EssencePerLevel * level;

		public static bool CanRaise(int level, int essence) => level < RegionOneCap && essence >= CostToRaise(level);
	}
}
