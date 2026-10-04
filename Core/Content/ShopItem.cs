namespace Sigilos.Core.Content
{
	/// <summary>O que a Loja vende.</summary>
	public enum ShopItem
	{
		Mana,
		Scrolls,

		/// <summary>Gema de Reavaliação: devolve uma runa ao estado em que caiu (Core/Runes/RuneReappraisal).</summary>
		ReappraisalGems,
		RenameAccount,

		/// <summary>Expansão de Coleção: vagas a mais na coleção, até <see cref="Progression.Account.MaxCollectionCapacity"/>.</summary>
		CollectionExpander,
	}
}
