using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>As peças de layout que toda tela repete: fundo de pedra, margem, painel com título e limpeza.</summary>
	public static class Layout
	{
		public const int ScreenMargin = 24;

		public static ColorRect Background()
		{
			var background = new ColorRect { Color = Palette.Background, MouseFilter = Control.MouseFilterEnum.Ignore };
			background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			return background;
		}

		/// <summary>Põe a margem da tela em <paramref name="screen"/> e devolve a coluna principal.</summary>
		public static VBoxContainer Page(Control screen)
		{
			var margin = new MarginContainer();
			margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			foreach (var side in new[] { "left", "right", "top", "bottom" })
				margin.AddThemeConstantOverride($"margin_{side}", ScreenMargin);
			screen.AddChild(margin);

			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 12);
			margin.AddChild(column);
			return column;
		}

		/// <summary>Tira todos os filhos: as telas se remontam do zero a cada mudança.</summary>
		public static void Clear(Node node)
		{
			foreach (var child in node.GetChildren())
				child.QueueFree();
		}

		/// <summary>
		/// Botão com ícone de Assets/ à esquerda. O ícone é desenhado pelo <see cref="Doodle"/> na cor do
		/// texto: os SVG são pretos, e o ícone comum do botão só consegue escurecer, nunca clarear.
		/// </summary>
		public static Button IconButton(string text, Texture2D? icon, int iconSize = 36, Color? ink = null)
		{
			// O ícone do botão é transparente e só reserva o lugar: quem desenha é o Doodle por cima.
			var button = new Button { Text = text, Icon = Blank(iconSize), IconAlignment = HorizontalAlignment.Left };
			button.AddThemeConstantOverride("h_separation", 8);
			var doodle = Doodle.Icon(icon, iconSize, ink ?? Palette.Gold);
			doodle.AnchorTop = doodle.AnchorBottom = 0.5f;
			doodle.OffsetLeft = 8;
			doodle.OffsetRight = 8 + iconSize;
			doodle.OffsetTop = -iconSize / 2f;
			doodle.OffsetBottom = iconSize / 2f;
			button.AddChild(doodle);
			return button;
		}

		private static readonly System.Collections.Generic.Dictionary<int, Texture2D> Blanks = new();

		/// <summary>Textura transparente de um tamanho: reserva lugar sem desenhar nada.</summary>
		private static Texture2D Blank(int size)
		{
			if (!Blanks.TryGetValue(size, out var texture))
			{
				texture = ImageTexture.CreateFromImage(Image.CreateEmpty(size, size, false, Image.Format.Rgba8));
				Blanks[size] = texture;
			}

			return texture;
		}

		/// <summary>Cabeçalho de tela: título, moedas (se houver) e o botão de voltar.</summary>
		public static HBoxContainer Header(string title, CurrencyBar? currencies, string back, System.Action onBack)
		{
			var header = new HBoxContainer();
			header.AddThemeConstantOverride("separation", 12);
			header.AddChild(new Label { Text = title, ThemeTypeVariation = GameTheme.Title, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
			if (currencies != null)
				header.AddChild(currencies);
			var button = new Button { Text = back };
			button.Pressed += onBack;
			header.AddChild(button);
			return header;
		}

		/// <summary>Uma aba com rolagem vertical; o conteúdo vai na coluna devolvida.</summary>
		public static VBoxContainer Tab(TabContainer tabs, string title)
		{
			var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
			var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 10);
			scroll.AddChild(column);
			tabs.AddChild(scroll);
			tabs.SetTabTitle(tabs.GetTabCount() - 1, title);
			return column;
		}

		/// <summary>Texto simples que quebra linha.</summary>
		public static Label Text(string text, string? variation = null, float width = 0)
		{
			var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(width, 0) };
			if (variation != null)
				label.ThemeTypeVariation = variation;
			return label;
		}

		/// <summary>Painel com cabeçalho. O conteúdo vai na coluna devolvida.</summary>
		public static (PanelContainer Panel, VBoxContainer Content) Section(string title)
		{
			var panel = new PanelContainer();
			var content = new VBoxContainer();
			content.AddThemeConstantOverride("separation", 8);
			panel.AddChild(content);
			content.AddChild(new Label { Text = title, ThemeTypeVariation = GameTheme.Heading });
			return (panel, content);
		}
	}
}
