using System;
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
	/// A Loja: as ofertas de Data/shop.json em bancas de couro — o símbolo grande, a quantidade e o
	/// sigilo de comprar com o preço em Ouro na plaquinha. Comprar pede confirmação; o GameRoot aplica a
	/// regra (Core/Progression/Shop) e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class ShopScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly HFlowContainer _offers = Layout.Flow(24).Named("Offers");
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center };

		public ShopScreen(GameDatabase database, PlayerState player)
		{
			_database = database;
			_player = player;
		}

		public event Action<ShopOffer>? BuyRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("destination.Shop"), "shop", _currencies, () => BackRequested?.Invoke()).Header);

			_offers.Alignment = FlowContainer.AlignmentMode.Center;
			var scroll = Layout.Scroll(_offers);
			page.AddChild(scroll);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);
			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			Layout.Clear(_offers);
			for (var i = 0; i < _database.Shop.Count; i++)
				_offers.AddChild(Card(_database.Shop[i]).Named($"Offer{i + 1}"));
		}

		public void ShowMessage(string text) => _message.Text = text;

		private Control Card(ShopOffer offer)
		{
			var panel = new PanelContainer { CustomMinimumSize = new Vector2(220, 0), TooltipText = offer.Name };
			var content = new VBoxContainer { Name = "Content", Alignment = BoxContainer.AlignmentMode.Center };
			content.AddThemeConstantOverride("separation", 12);
			panel.AddChild(content);

			var icon = Doodle.Icon(Art.Icon(offer.Item == ShopItem.Mana ? "mana" : "scroll"), 88, Palette.Gold);
			icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			content.AddChild(icon);

			var amount = new Label { Name = "Amount", Text = $"×{offer.Amount}", HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Number };
			amount.AddThemeFontSizeOverride("font_size", 28);
			amount.AddThemeColorOverride("font_color", Palette.Gold);
			content.AddChild(amount);

			var buy = SigilButton.Of("gold", T("shop.buy", Texts.Amount(offer.Item, offer.Amount), offer.Price), () => SigilDialog.Ask(this,
				T("shop.confirm", offer.Name, Texts.Amount(offer.Item, offer.Amount), offer.Price),
				() => BuyRequested?.Invoke(offer)), 72).Named("Buy");
			buy.Badge = offer.Price.ToString();
			buy.Disabled = !Shop.CanBuy(_player, offer);
			if (buy.Disabled)
				buy.TooltipText = T("shop.no_gold", offer.Price - _player.Gold);
			var row = Layout.Row(0, true).Named("Actions");
			row.AddChild(buy);
			content.AddChild(row);
			return panel;
		}
	}
}
