using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A Loja (GDD, seção 12): troca Ouro por Mana ou Pergaminhos, pelas ofertas de Data/shop.json. O
	/// Ouro só vem de jogar: canalização, subida de nível da conta e primeira vitória em andar de
	/// Masmorra. A Mana comprada pode passar do máximo.
	/// </summary>
	public static class Shop
	{
		public static bool CanBuy(PlayerState player, ShopOffer offer) => player.Gold >= offer.Price;

		public static bool Buy(PlayerState player, ShopOffer offer)
		{
			if (!CanBuy(player, offer))
				return false;

			player.Gold -= offer.Price;
			switch (offer.Item)
			{
				case ShopItem.Mana:
					player.Mana += offer.Amount;
					break;
				case ShopItem.Scrolls:
					player.Scrolls += offer.Amount;
					break;
			}

			return true;
		}
	}
}
