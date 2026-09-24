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
			var background = new ColorRect { Color = Palette.Stone, MouseFilter = Control.MouseFilterEnum.Ignore };
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

		/// <summary>Painel de pergaminho com cabeçalho. O conteúdo vai na coluna devolvida.</summary>
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
