namespace Sigilos.Core.Player
{
	/// <summary>Como a grade de Monstros se ordena. Em qualquer ordem, os favoritos vêm antes.</summary>
	public enum MonsterSort
	{
		/// <summary>Mais estrelas primeiro.</summary>
		Stars,

		/// <summary>Maior nível primeiro.</summary>
		Level,

		/// <summary>Mais estrelas naturais primeiro.</summary>
		Rarity,

		/// <summary>Por elemento: Fogo, Água, Vento, Luz, Trevas.</summary>
		Element,

		/// <summary>Pelo nome, de A a Z.</summary>
		Name,

		/// <summary>O que chegou por último primeiro.</summary>
		Newest,

		/// <summary>Pelo atributo de <see cref="MonsterFilter.SortStat"/>, com as runas: o maior primeiro.</summary>
		Stat,
	}
}
