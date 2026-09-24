using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// O tema da interface, na linha de Summoners War: painéis escuros com moldura dourada, botões de
	/// bronze, fonte serifada nos títulos e fonte limpa nos números. As fontes são do sistema — nenhum
	/// arquivo de fonte no projeto.
	///
	/// As telas pedem os papéis pelo nome de variação (<see cref="Title"/>, <see cref="Heading"/>...),
	/// nunca por cor solta.
	/// </summary>
	public static class GameTheme
	{
		/// <summary>Título grande de tela.</summary>
		public const string Title = "TitleLabel";

		/// <summary>Cabeçalho de painel.</summary>
		public const string Heading = "HeadingLabel";

		/// <summary>Texto pequeno e apagado: dicas, descrições.</summary>
		public const string Faded = "FadedLabel";

		/// <summary>Painel rebaixado dentro de outro painel (listas, barras).</summary>
		public const string InsetPanel = "InsetPanel";

		public static readonly Font Serif = new SystemFont
		{
			FontNames = new[] { "Georgia", "Palatino Linotype", "Book Antiqua", "Times New Roman", "serif" },
		};

		public static readonly Font Sans = new SystemFont
		{
			FontNames = new[] { "Segoe UI", "Noto Sans", "Helvetica", "Arial", "sans-serif" },
		};

		public static Theme Build()
		{
			var theme = new Theme { DefaultFont = Sans, DefaultFontSize = 16 };

			theme.SetColor("font_color", "Label", Palette.Text);

			theme.SetTypeVariation(Title, "Label");
			theme.SetFont("font", Title, Serif);
			theme.SetFontSize("font_size", Title, 38);
			theme.SetColor("font_color", Title, Palette.Gold);

			theme.SetTypeVariation(Heading, "Label");
			theme.SetFont("font", Heading, Serif);
			theme.SetFontSize("font_size", Heading, 21);
			theme.SetColor("font_color", Heading, Palette.Gold);

			theme.SetTypeVariation(Faded, "Label");
			theme.SetFontSize("font_size", Faded, 13);
			theme.SetColor("font_color", Faded, Palette.TextFaded);

			theme.SetStylebox("panel", "PanelContainer", Box(Palette.Panel, Palette.GoldDark, 2, 6, 12));
			theme.SetStylebox("panel", "Panel", Box(Palette.Panel, Palette.GoldDark, 2, 6, 0));
			theme.SetTypeVariation(InsetPanel, "PanelContainer");
			theme.SetStylebox("panel", InsetPanel, Box(Palette.Inset, Palette.Inset, 0, 6, 8));

			theme.SetStylebox("normal", "Button", Box(Palette.Button, Palette.Gold, 1, 5, 8));
			theme.SetStylebox("hover", "Button", Box(Palette.ButtonHover, Palette.Gold, 2, 5, 8));
			theme.SetStylebox("pressed", "Button", Box(Palette.Gold, Palette.Gold, 2, 5, 8));
			theme.SetStylebox("hover_pressed", "Button", Box(Palette.Gold, Palette.Text, 2, 5, 8));
			theme.SetStylebox("disabled", "Button", Box(Palette.Disabled, Palette.Disabled, 1, 5, 8));
			theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
			theme.SetColor("font_color", "Button", Palette.Text);
			theme.SetColor("font_hover_color", "Button", Palette.Text);
			theme.SetColor("font_pressed_color", "Button", Palette.Background);
			theme.SetColor("font_hover_pressed_color", "Button", Palette.Background);
			theme.SetColor("font_focus_color", "Button", Palette.Text);
			theme.SetColor("font_disabled_color", "Button", Palette.TextFaded);
			theme.SetColor("icon_normal_color", "Button", Palette.Text);
			theme.SetColor("icon_hover_color", "Button", Palette.Text);
			theme.SetColor("icon_pressed_color", "Button", Palette.Background);
			theme.SetColor("icon_disabled_color", "Button", Palette.TextFaded);
			theme.SetFont("font", "Button", Serif);
			theme.SetFontSize("font_size", "Button", 17);

			// CheckBox herda de Button: sem estas caixas vazias, cada caixinha viraria um botão.
			foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
				theme.SetStylebox(state, "CheckBox", new StyleBoxEmpty());
			theme.SetColor("font_color", "CheckBox", Palette.Text);
			theme.SetColor("font_hover_color", "CheckBox", Palette.Text);
			theme.SetColor("font_pressed_color", "CheckBox", Palette.Gold);
			theme.SetColor("font_hover_pressed_color", "CheckBox", Palette.Gold);

			theme.SetStylebox("background", "ProgressBar", Box(Palette.Inset, Palette.Background, 1, 3, 0));
			theme.SetStylebox("fill", "ProgressBar", Box(Palette.Health, Palette.Health, 0, 3, 0));

			theme.SetStylebox("panel", "TabContainer", Box(Palette.Panel, Palette.GoldDark, 2, 6, 12));
			theme.SetStylebox("tab_selected", "TabContainer", Box(Palette.Panel, Palette.Gold, 2, 5, 10));
			theme.SetStylebox("tab_unselected", "TabContainer", Box(Palette.Inset, Palette.GoldDark, 1, 5, 10));
			theme.SetStylebox("tab_hovered", "TabContainer", Box(Palette.PanelLight, Palette.Gold, 1, 5, 10));
			theme.SetColor("font_selected_color", "TabContainer", Palette.Gold);
			theme.SetColor("font_unselected_color", "TabContainer", Palette.TextFaded);
			theme.SetColor("font_hovered_color", "TabContainer", Palette.Text);
			theme.SetFont("font", "TabContainer", Serif);
			theme.SetFontSize("font_size", "TabContainer", 17);

			theme.SetStylebox("normal", "OptionButton", Box(Palette.Button, Palette.Gold, 1, 5, 8));
			theme.SetStylebox("panel", "PopupMenu", Box(Palette.Panel, Palette.Gold, 1, 4, 6));

			theme.SetStylebox("panel", "TooltipPanel", Box(Palette.Inset, Palette.Gold, 1, 4, 8));
			theme.SetColor("font_color", "TooltipLabel", Palette.Text);
			return theme;
		}

		public static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius, int margin)
		{
			var box = new StyleBoxFlat { BgColor = background, BorderColor = border };
			box.SetBorderWidthAll(borderWidth);
			box.SetCornerRadiusAll(radius);
			box.SetContentMarginAll(margin);
			return box;
		}
	}
}
