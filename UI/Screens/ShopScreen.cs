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
	/// A Loja: as ofertas de Data/shop.json em cartões, cada uma com o preço em Ouro. Comprar pede
	/// confirmação; o GameRoot aplica a regra (Core/Progression/Shop) e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class ShopScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;

		private readonly CurrencyBar _currencies = new();
		private readonly HFlowContainer _offers = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly Label _message = new() { HorizontalAlignment = HorizontalAlignment.Center };

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
			page.AddChild(Layout.Header(T("loja.titulo"), _currencies, T("geral.voltar"), () => BackRequested?.Invoke()));
			page.AddChild(Layout.Text(T("loja.subtitulo", Account.LevelUpGold), GameTheme.Faded));

			_offers.AddThemeConstantOverride("h_separation", 16);
			_offers.AddThemeConstantOverride("v_separation", 16);
			var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			scroll.AddChild(_offers);
			page.AddChild(scroll);

			_message.AddThemeColorOverride("font_color", Palette.Gold);
			page.AddChild(_message);
			Refresh();
		}

		public void Refresh()
		{
			_currencies.Refresh(_player);
			Layout.Clear(_offers);
			foreach (var offer in _database.Shop)
				_offers.AddChild(Card(offer));
		}

		public void ShowMessage(string text) => _message.Text = text;

		private Control Card(ShopOffer offer)
		{
			var (panel, content) = Layout.Section(offer.Name);
			panel.CustomMinimumSize = new Vector2(260, 0);

			var icon = Doodle.Icon(Art.Icon(offer.Item == ShopItem.Mana ? "mana" : "scroll"), 72, Palette.Gold);
			icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			content.AddChild(icon);

			var amount = new Label { Text = Texts.Amount(offer.Item, offer.Amount), HorizontalAlignment = HorizontalAlignment.Center };
			amount.AddThemeFontOverride("font", GameTheme.Serif);
			amount.AddThemeFontSizeOverride("font_size", 22);
			content.AddChild(amount);
			content.AddChild(Layout.Text(T(offer.Item == ShopItem.Mana ? "loja.explica_mana" : "loja.explica_pergaminhos"), GameTheme.Faded, 236));

			var buy = Layout.IconButton(T("loja.preco", offer.Price), Art.Icon("gold"), 26);
			buy.CustomMinimumSize = new Vector2(0, 48);
			buy.Disabled = !Shop.CanBuy(_player, offer);
			buy.TooltipText = buy.Disabled ? T("loja.sem_ouro", offer.Price - _player.Gold) : "";
			buy.Pressed += () => Confirm(offer);
			content.AddChild(buy);
			return panel;
		}

		private void Confirm(ShopOffer offer)
		{
			var dialog = new ConfirmationDialog
			{
				DialogText = T("loja.confirma", offer.Name, Texts.Amount(offer.Item, offer.Amount), offer.Price),
				Title = T("geral.confirmar"),
				OkButtonText = T("geral.sim"),
				CancelButtonText = T("geral.nao"),
			};
			dialog.Confirmed += () => BuyRequested?.Invoke(offer);
			dialog.Confirmed += dialog.QueueFree;
			dialog.Canceled += dialog.QueueFree;
			AddChild(dialog);
			dialog.PopupCentered();
		}
	}
}
