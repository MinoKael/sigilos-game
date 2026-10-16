using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Components;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// A Loja: as ofertas de Data/shop.json em <see cref="ShopShelf"/> (categorias à esquerda, grade de 4 colunas),
	/// cada uma uma banca com o que é, escrito ("30 Mana"), o símbolo e o botão Comprar com o preço em Ouro (sem
	/// Ouro, o botão fica apagado e o toque diz quanto falta). Tocar no símbolo abre para que a oferta serve. Comprar pede confirmação; o GameRoot aplica a regra (Core/Progression/Shop) e
	/// chama <see cref="Refresh"/>.
	/// </summary>
	public partial class ShopScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private ShopShelf _shelf = null!;
		private List<int> _categories = new();
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center };

		/// <summary>A troca de nome só vale para quem joga numa conta que já tem nome.</summary>
		private readonly bool _canRename;

		/// <param name="canRename">Jogando numa conta com nome: a oferta de troca de nome fica liberada.</param>
		public ShopScreen(GameDatabase database, PlayerState player, bool canRename = false)
		{
			_database = database;
			_player = player;
			_canRename = canRename;
		}

		public event Action<ShopOffer>? BuyRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Shop"), _currencies, () => BackRequested?.Invoke(), this, T("shop.intro"), T("shop.info_hint")).Header);

			// Categorias à esquerda, as ofertas da aberta numa grade de 4 colunas à direita.
			// Só as categorias que têm oferta: cada uma na ordem de sempre (Transmutação, Mana, Especial).
			var names = new[] { (T("shop.category.scrolls"), "scroll"), (T("shop.category.mana"), "mana"), (T("shop.category.special"), "gem") };
			_categories = Enumerable.Range(0, names.Length).Where(c => _database.Shop.Any(o => Category(o) == c)).ToList();
			_shelf = new ShopShelf(_categories.Select(c => names[c]).ToList(), Cards);
			page.AddChild(_shelf);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);
			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			_shelf.Refresh();
		}

		private static int Category(ShopOffer offer) => offer.Item switch
		{
			ShopItem.Scrolls => 0,
			ShopItem.Mana => 1,
			_ => 2,
		};

		private IEnumerable<Control> Cards(int category)
		{
			foreach (var offer in _database.Shop)
			{
				if (Category(offer) == _categories[category])
					yield return Card(offer);
			}
		}

		public void ShowMessage(string text) => _message.Text = text;

		private Control Card(ShopOffer offer)
		{
			var (icon, tint) = offer.Item switch
			{
				ShopItem.Mana => ("mana", Palette.Gold),
				ShopItem.ReappraisalGems => ("gem", Palette.Arcane),
				ShopItem.RenameAccount => ("avatar", Palette.Gold),
				ShopItem.CollectionExpander => ("monster", Palette.Gold),
				_ => ("scroll", Palette.Gold),
			};

			// A troca de nome pede uma conta com nome; a janela do nome já é a confirmação (ela diz o preço).
			var rename = offer.Item == ShopItem.RenameAccount;
			var canBuy = Shop.CanBuy(_player, offer) && (!rename || _canRename);
			var buy = GameButton.Of(T("shop.buy_button"), () =>
			{
				if (rename)
					BuyRequested?.Invoke(offer);
				else
					Dialog.Confirm(this,
						T("shop.confirm_title"),
						T("shop.confirm", offer.Name, Texts.Amount(offer.Item, offer.Amount), offer.Price),
						T("shop.buy_button"),
						() => BuyRequested?.Invoke(offer));
			}, ButtonKind.Primary, null, 56).WithCost("gold", Texts.Number(offer.Price)).Named("Buy");
			if (!canBuy)
			{
				buy.Block(rename && !_canRename ? T("shop.rename_no_account")
					: Shop.IsSoldOut(_player, offer) ? T("shop.sold_out", Texts.Number(Account.MaxCollectionCapacity))
					: T("shop.no_gold", Texts.Number(offer.Price - _player.Gold)), offer.Name);
			}

			// O símbolo explica a oferta (a Gema diz quantas o jogador já tem, a Expansão as vagas de agora e o
			// máximo: nada disso está em alguma barra).
			return ShopCard.Of(Texts.Amount(offer.Item, offer.Amount), offer.Name, icon, tint, buy,
				symbol => Dialog.Info(symbol, offer.Name, T($"shop.info.{offer.Item}", _player.ReappraisalGems, _player.CollectionCapacity, Account.MaxCollectionCapacity)));
		}
	}
}
