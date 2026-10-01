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
	/// A Loja: as ofertas de Data/shop.json em bancas de couro — o que é, escrito ("30 Mana"), o símbolo
	/// grande e o botão Comprar com o preço em Ouro (sem Ouro, diz quanto falta). Comprar pede
	/// confirmação; o GameRoot aplica a regra (Core/Progression/Shop) e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class ShopScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _offers = Layout.Row(24).Named("Offers");
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
			page.AddChild(Layout.Header(T("destination.Shop"), _currencies, () => BackRequested?.Invoke()).Header);
			page.AddChild(Layout.Text(T("shop.intro"), GameTheme.Faded).Named("Intro"));

			_offers.Alignment = BoxContainer.AlignmentMode.Center;
			var scroll = Layout.Scroll(_offers);
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
			scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
			_offers.SizeFlagsVertical = SizeFlags.Expand | SizeFlags.ShrinkCenter;
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

		private PanelContainer Card(ShopOffer offer)
		{
			var panel = new PanelContainer { CustomMinimumSize = new Vector2(260, 340) };
			var content = new VBoxContainer { Name = "Content", Alignment = BoxContainer.AlignmentMode.Center };
			content.AddThemeConstantOverride("separation", 14);
			panel.AddChild(content);

			var name = new Label { Name = "Name", Text = Texts.Amount(offer.Item, offer.Amount), HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Heading };
			content.AddChild(name);
			var icon = Doodle.Icon(Art.Icon(offer.Item == ShopItem.Mana ? "mana" : "scroll"), 100, Palette.Gold);
			icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			content.AddChild(icon);
			var what = Layout.Text(offer.Name, GameTheme.Faded, 220).Named("What");
			what.HorizontalAlignment = HorizontalAlignment.Center;
			content.AddChild(what);

			var canBuy = Shop.CanBuy(_player, offer);
			var buy = GameButton.Of(T("shop.buy_button"), () => Dialog.Confirm(this,
				T("shop.confirm_title"),
				T("shop.confirm", offer.Name, Texts.Amount(offer.Item, offer.Amount), offer.Price),
				T("shop.buy_button"),
				() => BuyRequested?.Invoke(offer)), ButtonKind.Primary).WithCost("gold", offer.Price.ToString()).Named("Buy");
			buy.Disabled = !canBuy;
			content.AddChild(buy);
			if (!canBuy)
			{
				var missing = new Label { Name = "Short", Text = T("shop.no_gold", offer.Price - _player.Gold), HorizontalAlignment = HorizontalAlignment.Center };
				missing.AddThemeColorOverride("font_color", Palette.Negative);
				missing.AddThemeFontSizeOverride("font_size", GameTheme.SmallSize);
				content.AddChild(missing);
			}

			return panel;
		}
	}
}
