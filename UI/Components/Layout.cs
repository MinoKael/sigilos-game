using System;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>As peças de layout que toda tela repete: fundo, margem, cabeçalho, painel com título, cápsula e limpeza.</summary>
	public static class Layout
	{
		public const int ScreenMargin = 24;

		/// <summary>
		/// O fundo de pedra quente com a luz de vela e o anel de sigilo, centrado na tela ou no centro de
		/// <paramref name="focus"/> (o que a tela tem no meio: a constelação, o portal, o círculo).
		/// </summary>
		public static Backdrop Background(Control? focus = null) => new(focus);

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

		/// <summary>O controle mais alto acima de <paramref name="control"/>: onde abrem as camadas por cima da tela (herdam o tema).</summary>
		public static Control Host(Control control)
		{
			var host = control;
			while (host.GetParent() is Control parent)
				host = parent;
			return host;
		}

		/// <summary>Tira todos os filhos: as telas se remontam do zero a cada mudança.</summary>
		public static void Clear(Node node)
		{
			foreach (var child in node.GetChildren())
				child.QueueFree();
		}

		/// <summary>
		/// Cabeçalho de tela: o título (com o símbolo da tela, se houver) à esquerda, o cabeçalho de
		/// recursos e o sigilo de voltar à direita. A caixa do meio (<c>Extra</c>) recebe o que a tela
		/// quiser pôr ao lado do título (contagem, abas).
		/// </summary>
		public static (HBoxContainer Header, HBoxContainer Extra) Header(string title, string? icon, CurrencyBar? currencies, Action onBack)
		{
			var header = new HBoxContainer();
			header.AddThemeConstantOverride("separation", 12);
			if (icon != null)
				header.AddChild(Doodle.Icon(Art.Icon(icon), 40, Palette.Gold));
			header.AddChild(new Label { Text = title, ThemeTypeVariation = GameTheme.Title, VerticalAlignment = VerticalAlignment.Center });
			var extra = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Begin };
			extra.AddThemeConstantOverride("separation", 10);
			header.AddChild(extra);
			if (currencies != null)
			{
				currencies.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
				header.AddChild(currencies);
			}

			header.AddChild(SigilButton.Of("back", T("common.back"), onBack, 48, SigilShape.Square));
			return (header, extra);
		}

		/// <summary>Cápsula de símbolo e número: recompensa, custo, contagem. O nome vai na dica.</summary>
		public static PanelContainer Chip(string icon, string value, string tooltip, Color? ink = null) => Chip(Art.Icon(icon), value, tooltip, ink);

		/// <summary>Cápsula com um Glifo, escrito na fonte das runas.</summary>
		public static PanelContainer Chip(Core.Content.Glyph glyph, string value, string tooltip, Color? ink = null)
		{
			var chip = Chip((Texture2D?)null, value, tooltip, ink);
			var row = (HBoxContainer)chip.GetChild(0);
			row.GetChild(0).QueueFree();
			var rune = new RuneGlyph(glyph, 20, ink ?? Palette.Gold);
			row.AddChild(rune);
			row.MoveChild(rune, 0);
			return chip;
		}

		/// <summary>Cápsula com qualquer desenho (efeito, criatura).</summary>
		public static PanelContainer Chip(Texture2D? icon, string value, string tooltip, Color? ink = null)
		{
			var capsule = new PanelContainer { TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Stop };
			var box = GameTheme.Carved(Palette.Inset, 3);
			box.SetCornerRadiusAll(14);
			box.ContentMarginLeft = 4;
			box.ContentMarginRight = 10;
			capsule.AddThemeStyleboxOverride("panel", box);
			var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 4);
			row.AddChild(Medal(icon, ink ?? Palette.Gold, 22));
			var label = new Label { Text = value, ThemeTypeVariation = GameTheme.Number, MouseFilter = Control.MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", 15);
			row.AddChild(label);
			capsule.AddChild(row);
			return capsule;
		}

		/// <summary>Um retrato recortado em círculo, de tamanho fixo.</summary>
		public static Control Medal(Texture2D? art, Color ink, float size)
		{
			var holder = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
			holder.AddChild(Doodle.Masked(art, ink, MaskShape.Circle, boil: false));
			return holder;
		}

		/// <summary>Fileira de sigilos, centralizada ou não.</summary>
		public static HBoxContainer Row(int separation = 10, bool centered = false)
		{
			var row = new HBoxContainer { Alignment = centered ? BoxContainer.AlignmentMode.Center : BoxContainer.AlignmentMode.Begin };
			row.AddThemeConstantOverride("separation", separation);
			return row;
		}

		/// <summary>Fileira que quebra linha.</summary>
		public static HFlowContainer Flow(int separation = 8)
		{
			var flow = new HFlowContainer();
			flow.AddThemeConstantOverride("h_separation", separation);
			flow.AddThemeConstantOverride("v_separation", separation);
			return flow;
		}

		/// <summary>Rolagem vertical: a gema do tema, sem trilho.</summary>
		public static ScrollContainer Scroll(Control content)
		{
			var scroll = new ScrollContainer
			{
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			};
			content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scroll.AddChild(content);
			return scroll;
		}

		/// <summary>Uma aba com rolagem vertical; o conteúdo vai na coluna devolvida.</summary>
		public static VBoxContainer Tab(TabContainer tabs, string title)
		{
			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 10);
			tabs.AddChild(Scroll(column));
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
			if (title.Length > 0)
				content.AddChild(new Label { Text = title, ThemeTypeVariation = GameTheme.Heading });
			return (panel, content);
		}

		/// <summary>Barra de energia entalhada, na cor dada.</summary>
		public static ProgressBar Energy(Color color, float height = 10)
		{
			var bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Stop };
			bar.AddThemeStyleboxOverride("fill", GameTheme.Energy(color));
			return bar;
		}
	}
}
