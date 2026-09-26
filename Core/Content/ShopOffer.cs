namespace Sigilos.Core.Content
{
	/// <summary>Uma oferta da Loja (Data/shop.json): <see cref="Amount"/> de <see cref="Item"/> por <see cref="Price"/> de Ouro.</summary>
	public sealed record ShopOffer
	{
		public string Id { get; init; } = "";
		public string Name { get; init; } = "";
		public ShopItem Item { get; init; }
		public int Amount { get; init; }

		/// <summary>Em Ouro.</summary>
		public int Price { get; init; }
	}
}
