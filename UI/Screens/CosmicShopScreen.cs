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
	/// A Loja Cósmica: as ofertas de Data/arena.json em bancas, pagas em Astrarium. Cada banca diz quantas
	/// vezes ainda se pode comprar no dia ou na semana. Comprar pede confirmação; o GameRoot aplica a regra
	/// (<see cref="Arena.Buy"/>) e chama <see cref="Refresh"/>.
	/// </summary>
	public partial class CosmicShopScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly DateTime _now;

		private readonly CurrencyBar _currencies = new();
		private readonly HBoxContainer _offers = Layout.Row(Space.Spacious).Named("Offers");
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center };

		public CosmicShopScreen(GameDatabase database, PlayerState player, DateTime now)
		{
			_database = database;
			_player = player;
			_now = now;
		}

		public event Action<ArenaOffer>? BuyRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T("arena.cosmic_shop"), _currencies, () => BackRequested?.Invoke()).Header);
			page.AddChild(Layout.Text(T("arena.shop_intro"), GameTheme.Faded).Named("Intro"));

			var balance = Layout.Labeled("astrarium", Texts.Number(_player.Arena.Astrarium), T("arena.astrarium"), Palette.Spirit).Named("Balance");
			page.AddChild(balance);

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

		public void ShowMessage(string text) => _message.Text = text;

		public void Refresh()
		{
			_currencies.Refresh(_player);
			if (FindChild("Balance", true, false) is { } balance)
			{
				var value = balance.FindChild("Value", true, false) as Label;
				if (value != null)
					value.Text = Texts.Number(_player.Arena.Astrarium);
			}

			Layout.Clear(_offers);
			var arena = _database.Arena;
			for (var i = 0; i < arena.Shop.Count; i++)
				_offers.AddChild(Card(arena.Shop[i], arena).Named($"Offer{i + 1}"));
		}

		private static string Icon(ArenaItem item) => item switch
		{
			ArenaItem.Mana => "mana",
			ArenaItem.ReappraisalGems => "gem",
			ArenaItem.InfusionCores => "monster",
			_ => "scroll",
		};

		private PanelContainer Card(ArenaOffer offer, ArenaDefinition arena)
		{
			var panel = new PanelContainer { CustomMinimumSize = new Vector2(260, 340) };
			var content = new VBoxContainer { Name = "Content", Alignment = BoxContainer.AlignmentMode.Center };
			content.AddThemeConstantOverride("separation", Space.Wide);
			panel.AddChild(content);

			content.AddChild(new Label { Name = "Name", Text = offer.Amount > 1 ? $"{Texts.Number(offer.Amount)} × {offer.Name}" : offer.Name, HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = GameTheme.Heading, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(220, 0) });
			var icon = Doodle.Icon(Art.Icon(Icon(offer.Item)), 100, Palette.Gold);
			icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			content.AddChild(icon);

			if (offer.Limit > 0)
			{
				var left = Math.Max(0, offer.Limit - Arena.BoughtOf(_player, arena, offer, _now));
				var key = offer.Period == ArenaPeriod.Day ? "arena.limit_day" : "arena.limit_week";
				var limit = Layout.Text(T(key, left, offer.Limit), GameTheme.Faded, 220).Named("Limit");
				limit.HorizontalAlignment = HorizontalAlignment.Center;
				content.AddChild(limit);
			}

			var canBuy = Arena.CanBuy(_player, arena, offer, _now);
			var buy = GameButton.Of(T("shop.buy_button"), () =>
			{
				Dialog.Confirm(this,
					T("shop.confirm_title"),
					T("arena.shop_confirm", offer.Name, offer.Amount, offer.Price),
					T("shop.buy_button"),
					() => BuyRequested?.Invoke(offer));
			}, ButtonKind.Primary).WithCost("astrarium", Texts.Number(offer.Price)).Named("Buy");
			buy.Disabled = !canBuy;
			content.AddChild(buy);
			if (!canBuy)
			{
				var message = Arena.IsSoldOut(_player, arena, offer, _now)
					? T(offer.Period == ArenaPeriod.Day ? "arena.sold_out_day" : "arena.sold_out_week")
					: T("arena.no_astrarium", Texts.Number(offer.Price - _player.Arena.Astrarium));
				var missing = new Label { Name = "Short", Text = message, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(220, 0) };
				missing.AddThemeColorOverride("font_color", Palette.Negative);
				missing.AddThemeFontSizeOverride("font_size", GameTheme.SmallSize);
				content.AddChild(missing);
			}

			return panel;
		}
	}
}
