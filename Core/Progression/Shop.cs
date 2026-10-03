using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// A Loja (GDD, seção 12): troca Ouro por Mana, Pergaminhos, Gemas de Reavaliação ou a troca do nome da
	/// conta, pelas ofertas de Data/shop.json. O nome é do servidor: quem chama <see cref="Buy"/> com a troca
	/// de nome é o GameRoot, só depois que o servidor aceitou o nome novo (aqui sai só o Ouro). O
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
				case ShopItem.ReappraisalGems:
					player.ReappraisalGems += offer.Amount;
					break;
				case ShopItem.RenameAccount:
					break;
			}

			return true;
		}
	}
}
