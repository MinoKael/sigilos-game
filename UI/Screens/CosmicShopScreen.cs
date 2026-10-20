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
	/// A Loja Cósmica: as ofertas de Data/arena.json em bancas, pagas em Astrarium. Cada banca diz quantas
	/// vezes ainda se pode comprar no dia ou na semana. Comprar pede confirmação; o GameRoot aplica a regra
	/// (<see cref="Arena.Buy"/>) e chama <see cref="Refresh"/>. A Loja do Véu é a mesma banca com as ofertas
	/// <c>veilShop</c>, paga em Ecos do Véu (<see cref="Veil.Buy"/>).
	/// </summary>
	public partial class CosmicShopScreen : Control
	{
		private readonly GameDatabase _database;
		private readonly PlayerState _player;
		private readonly DateTime _now;

		private readonly CurrencyBar _currencies = new();
		private ShopShelf _shelf = null!;
		private List<int> _categories = new();
		private readonly Label _message = new() { Name = "Message", HorizontalAlignment = HorizontalAlignment.Center };

		private readonly bool _veil;

		public CosmicShopScreen(GameDatabase database, PlayerState player, DateTime now, bool veil = false)
		{
			_database = database;
			_player = player;
			_now = now;
			_veil = veil;
		}

		private IReadOnlyList<ArenaOffer> Offers => _veil ? _database.Arena.VeilShop : _database.Arena.Shop;

		private int Balance => _veil ? _player.Veil.Ecos : _player.Arena.Astrarium;

		private string CurrencyIcon => _veil ? "polar_star" : "astrarium";

		private string CurrencyName => T(_veil ? "veil.ecos" : "arena.astrarium");

		private bool CanBuy(ArenaOffer offer) => _veil ? Veil.CanBuy(_player, _database.Arena, offer, _now) : Arena.CanBuy(_player, _database.Arena, offer, _now);

		private bool IsSoldOut(ArenaOffer offer) => _veil ? Veil.IsSoldOut(_player, _database.Arena, offer, _now) : Arena.IsSoldOut(_player, _database.Arena, offer, _now);

		private int Bought(ArenaOffer offer) => _veil ? Veil.BoughtOf(_player, _database.Arena, offer, _now) : Arena.BoughtOf(_player, _database.Arena, offer, _now);

		public event Action<ArenaOffer>? BuyRequested;
		public event Action? BackRequested;

		public override void _Ready()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(Layout.Background());
			var page = Layout.Page(this);
			page.AddChild(Layout.Header(T(_veil ? "veil.shop" : "arena.cosmic_shop"), _currencies, () => BackRequested?.Invoke(), this, T(_veil ? "veil.shop_intro" : "arena.shop_intro")).Header);

			var balance = Layout.Labeled(CurrencyIcon, Texts.Number(Balance), CurrencyName, Palette.Spirit).Named("Balance");
			page.AddChild(balance);

			// Só as categorias que têm oferta: cada uma na ordem de sempre (Transmutação, Mana, Especial).
			var names = new[] { (T("shop.category.scrolls"), "scroll"), (T("shop.category.mana"), "mana"), (T("shop.category.special"), "gem") };
			_categories = Enumerable.Range(0, names.Length).Where(c => Offers.Any(o => Category(o) == c)).ToList();
			_shelf = new ShopShelf(_categories.Select(c => names[c]).ToList(), Cards);
			page.AddChild(_shelf);

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
					value.Text = Texts.Number(Balance);
			}

			_shelf.Refresh();
		}

		private static int Category(ArenaOffer offer) => offer.Item switch
		{
			ArenaItem.Scrolls or ArenaItem.LightDarkScrolls => 0,
			ArenaItem.Mana => 1,
			_ => 2,
		};

		private IEnumerable<Control> Cards(int category)
		{
			var arena = _database.Arena;
			foreach (var offer in Offers)
			{
				if (Category(offer) == _categories[category])
					yield return Card(offer, arena);
			}
		}

		private static string Icon(ArenaItem item) => item switch
		{
			ArenaItem.Mana => "mana",
			ArenaItem.ReappraisalGems => "gem",
			ArenaItem.InfusionCores => "monster",
			_ => "scroll",
		};

		private Control Card(ArenaOffer offer, ArenaDefinition arena)
		{
			var canBuy = CanBuy(offer);
			var buy = GameButton.Of(T("shop.buy_button"), () =>
			{
				Dialog.Confirm(this,
					T("shop.confirm_title"),
					T(_veil ? "veil.shop_confirm" : "arena.shop_confirm", offer.Name, offer.Amount, offer.Price),
					T("shop.buy_button"),
					() => BuyRequested?.Invoke(offer));
			}, ButtonKind.Primary, null, 56).WithCost(CurrencyIcon, Texts.Number(offer.Price)).Named("Buy");
			if (!canBuy)
			{
				buy.Block(IsSoldOut(offer)
					? T(offer.Period == ArenaPeriod.Day ? "arena.sold_out_day" : "arena.sold_out_week")
					: T(_veil ? "veil.no_ecos" : "arena.no_astrarium", Texts.Number(offer.Price - Balance)), offer.Name);
			}

			var detail = "";
			if (offer.Limit > 0)
			{
				var left = Math.Max(0, offer.Limit - Bought(offer));
				detail = T(offer.Period == ArenaPeriod.Day ? "arena.limit_day" : "arena.limit_week", left, offer.Limit);
			}

			return ShopCard.Of(offer.Amount > 1 ? $"{Texts.Number(offer.Amount)} × {offer.Name}" : offer.Name, detail, Icon(offer.Item), Palette.Gold, buy);
		}
	}
}
