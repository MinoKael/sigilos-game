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
	/// grande e o botão Comprar com o preço em Ouro (sem Ouro, diz quanto falta). Tocar no símbolo abre
	/// para que a oferta serve. As bancas rolam de lado (arrastando, com o mouse também), com a barra à
	/// vista embaixo delas. Comprar pede confirmação; o GameRoot aplica a regra (Core/Progression/Shop) e
	/// chama <see cref="Refresh"/>.
	/// </summary>
	public partial class ShopScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _offers = Layout.Row(24).Named("Offers");
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
			page.AddChild(Layout.Header(T("destination.Shop"), _currencies, () => BackRequested?.Invoke()).Header);
			page.AddChild(Layout.Text(T("shop.intro"), GameTheme.Faded).Named("Intro"));
			page.AddChild(Layout.Text(T("shop.info_hint"), GameTheme.Faded).Named("InfoHint"));

			// A fileira de bancas no meio da altura, com a barra logo embaixo dela, sempre à vista.
			_offers.Alignment = BoxContainer.AlignmentMode.Center;
			var shelf = new MarginContainer { Name = "Shelf" };
			shelf.AddThemeConstantOverride("margin_bottom", 14);
			shelf.AddChild(_offers);
			var scroll = Layout.Scroll(shelf);
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowAlways;
			scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
			scroll.SizeFlagsVertical = SizeFlags.Expand | SizeFlags.ShrinkCenter;
			GameTheme.Grooved(scroll.GetHScrollBar());
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
			var icon = offer.Item switch
			{
				ShopItem.Mana => Doodle.Icon(Art.Icon("mana"), 100, Palette.Gold),
				ShopItem.ReappraisalGems => Doodle.Icon(Art.Icon("gem"), 100, Palette.Arcane),
				ShopItem.RenameAccount => Doodle.Icon(Art.Icon("avatar"), 100, Palette.Gold),
				ShopItem.CollectionExpander => Doodle.Icon(Art.Icon("monster"), 100, Palette.Gold),
				_ => Doodle.Icon(Art.Icon("scroll"), 100, Palette.Gold),
			};
			icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			icon.MouseDefaultCursorShape = CursorShape.PointingHand;
			// O símbolo explica a oferta (a Gema diz quantas o jogador já tem, a Expansão as vagas de agora e o
			// máximo: nada disso está em alguma barra).
			void Explain() => Dialog.Info(icon, offer.Name, T($"shop.info.{offer.Item}", _player.ReappraisalGems, _player.CollectionCapacity, Account.MaxCollectionCapacity));
			Press.On(icon, Explain, Explain);
			content.AddChild(icon);
			var what = Layout.Text(offer.Name, GameTheme.Faded, 220).Named("What");
			what.HorizontalAlignment = HorizontalAlignment.Center;
			content.AddChild(what);

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
			}, ButtonKind.Primary).WithCost("gold", Texts.Number(offer.Price)).Named("Buy");
			buy.Disabled = !canBuy;
			content.AddChild(buy);
			if (!canBuy)
			{
				var message = rename && !_canRename ? T("shop.rename_no_account")
					: Shop.IsSoldOut(_player, offer) ? T("shop.sold_out", Texts.Number(Account.MaxCollectionCapacity))
					: T("shop.no_gold", Texts.Number(offer.Price - _player.Gold));
				var missing = new Label { Name = "Short", Text = message, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(220, 0) };
				missing.AddThemeColorOverride("font_color", Palette.Negative);
				missing.AddThemeFontSizeOverride("font_size", GameTheme.SmallSize);
				content.AddChild(missing);
			}

			return panel;
		}
	}
}
