namespace Sigilos.Core.Runes
{
	/// <summary>Como o inventário de runas se ordena.</summary>
	public enum RuneSort
	{
		/// <summary>Mais estrelas primeiro.</summary>
		Grade,

		/// <summary>Mais melhorada primeiro.</summary>
		Level,

		/// <summary>Mais subatributos primeiro.</summary>
		Rarity,

		/// <summary>Por conjunto.</summary>
		Set,

		/// <summary>Por espaço, de 1 a 6.</summary>
		Slot,

		/// <summary>A que chegou por último primeiro.</summary>
		Newest,
	}
}
