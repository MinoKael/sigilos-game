using System;
using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O jeito de toda loja: as categorias numa coluna à esquerda (Transmutação, Mana, Especial...) e, à direita, as
	/// ofertas da categoria aberta numa grade de 4 colunas que rola para baixo. Cada loja diz as categorias e,
	/// para a aberta, as bancas (<see cref="ShopCard"/>); <see cref="Refresh"/> refaz as bancas sem sair da categoria
	/// nem da posição da rolagem.
	/// </summary>
	public partial class ShopShelf : HBoxContainer
	{
		public const int Columns = 4;
		private const float CategoryWidth = 210;

		private readonly TextTabs _tabs = new(vertical: true, 56) { Name = "Categories" };
		private readonly GridContainer _grid = new() { Name = "Offers", Columns = Columns };
		private readonly ScrollContainer _scroll;
		private readonly Func<int, IEnumerable<Control>> _cards;

		/// <param name="categories">Nome e símbolo de cada categoria, na ordem da coluna.</param>
		/// <param name="cards">As bancas da categoria de índice dado.</param>
		public ShopShelf(IReadOnlyList<(string Name, string Icon)> categories, Func<int, IEnumerable<Control>> cards)
		{
			Name = "Shelf";
			SizeFlagsVertical = SizeFlags.ExpandFill;
			AddThemeConstantOverride("separation", Space.Spacious);
			_cards = cards;

			var side = new VBoxContainer { Name = "Side", CustomMinimumSize = new Vector2(CategoryWidth, 0) };
			foreach (var (name, icon) in categories)
				_tabs.Add(name, "", icon);
			_tabs.Changed += _ =>
			{
				_scroll.ScrollVertical = 0;
				Refresh();
			};
			side.AddChild(_tabs);
			AddChild(side);

			_grid.AddThemeConstantOverride("h_separation", Space.Regular);
			_grid.AddThemeConstantOverride("v_separation", Space.Regular);
			_scroll = Layout.Scroll(_grid);
			AddChild(_scroll);
		}

		/// <summary>Refaz as bancas da categoria aberta, mantendo a rolagem.</summary>
		public void Refresh()
		{
			var keep = _scroll.ScrollVertical;
			Layout.Clear(_grid);
			var index = 0;
			foreach (var card in _cards(_tabs.Selected))
			{
				card.Name = $"Offer{++index}";
				_grid.AddChild(card);
			}

			// A grade tem sempre as 4 colunas (com 1 ou 2 bancas só, ela as esticaria até a borda): vãos completam a fileira.
			for (var gap = 0; _grid.GetChildCount() == 0 || _grid.GetChildCount() % Columns != 0; gap++)
				_grid.AddChild(new Control { Name = $"Gap{gap + 1}", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

			Layout.KeepScroll(_scroll, keep);
		}
	}

	/// <summary>Uma banca da loja, sempre do mesmo tamanho: nome, símbolo, uma linha de detalhe e o botão Comprar no pé.</summary>
	public static class ShopCard
	{
		private const float Height = 270;

		/// <param name="detail">A linha de baixo do símbolo (o que é, quantas ainda se pode comprar); o espaço dela é fixo.</param>
		/// <param name="buy">O botão Comprar, já com preço e, se não dá, o motivo (<see cref="GameButton.Block"/>).</param>
		/// <param name="explain">Tocar no símbolo explica a oferta (recebe o símbolo, onde a janela se cola); nulo deixa o símbolo mudo.</param>
		public static PanelContainer Of(string name, string detail, string icon, Color tint, GameButton buy, Action<Control>? explain = null)
		{
			var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, Height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			var content = new VBoxContainer { Name = "Content" };
			content.AddThemeConstantOverride("separation", Space.Regular);
			panel.AddChild(content);

			// Nome e detalhe quebram dentro de uma altura reservada: texto longo não mexe na banca.
			content.AddChild(Text("Name", name, GameTheme.Heading, 52));
			var symbol = Doodle.Icon(Art.Icon(icon), 72, tint).Named("Symbol");
			symbol.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			symbol.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			if (explain != null)
			{
				symbol.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
				Press.On(symbol, () => explain(symbol), () => explain(symbol));
			}

			content.AddChild(symbol);
			content.AddChild(Text("Detail", detail, GameTheme.Faded, 40));
			buy.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			content.AddChild(buy);
			return panel;
		}

		private static Label Text(string name, string text, string variation, float height) => new()
		{
			Name = name,
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			ThemeTypeVariation = variation,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(10, height),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
	}
}
