using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// O tema da interface: pergaminho discreto, fonte serifada nos títulos e fonte limpa nos números
	/// (GDD, seção 5). As fontes são do sistema — nenhum arquivo de fonte no projeto.
	///
	/// As telas pedem os papéis pelo nome de variação (<see cref="Title"/>, <see cref="OnStone"/>...),
	/// nunca por cor solta.
	/// </summary>
	public static class GameTheme
	{
		/// <summary>Título grande, serifado, sobre pedra.</summary>
		public const string Title = "TitleLabel";

		/// <summary>Cabeçalho serifado sobre pergaminho.</summary>
		public const string Heading = "HeadingLabel";

		/// <summary>Texto claro sobre pedra.</summary>
		public const string OnStone = "StoneLabel";

		/// <summary>Texto pequeno e apagado sobre pergaminho.</summary>
		public const string Faded = "FadedLabel";

		/// <summary>Painel escuro sobre pedra (faixas de moeda, barra de turnos).</summary>
		public const string DarkPanel = "DarkPanel";

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

			theme.SetColor("font_color", "Label", Palette.Ink);

			Variation(theme, Title, "Label");
			theme.SetFont("font", Title, Serif);
			theme.SetFontSize("font_size", Title, 40);
			theme.SetColor("font_color", Title, Palette.Bone);

			Variation(theme, Heading, "Label");
			theme.SetFont("font", Heading, Serif);
			theme.SetFontSize("font_size", Heading, 22);
			theme.SetColor("font_color", Heading, Palette.Ink);

			Variation(theme, OnStone, "Label");
			theme.SetColor("font_color", OnStone, Palette.Bone);

			Variation(theme, Faded, "Label");
			theme.SetFontSize("font_size", Faded, 13);
			theme.SetColor("font_color", Faded, Palette.InkFaded);

			theme.SetStylebox("panel", "PanelContainer", Box(Palette.Parchment, Palette.Ink, 2, 6, 12));
			theme.SetStylebox("panel", "Panel", Box(Palette.Parchment, Palette.Ink, 2, 6, 0));
			Variation(theme, DarkPanel, "PanelContainer");
			theme.SetStylebox("panel", DarkPanel, Box(Palette.StoneLight, Palette.Ink, 1, 6, 8));

			theme.SetStylebox("normal", "Button", Box(Palette.ParchmentDark, Palette.Ink, 2, 5, 8));
			theme.SetStylebox("hover", "Button", Box(Palette.Parchment, Palette.Ink, 2, 5, 8));
			theme.SetStylebox("pressed", "Button", Box(Palette.Gold, Palette.Ink, 2, 5, 8));
			theme.SetStylebox("disabled", "Button", Box(Palette.ParchmentDark.Darkened(0.35f), Palette.InkFaded, 1, 5, 8));
			theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
			theme.SetColor("font_color", "Button", Palette.Ink);
			theme.SetColor("font_hover_color", "Button", Palette.Ink);
			theme.SetColor("font_pressed_color", "Button", Palette.Ink);
			theme.SetColor("font_focus_color", "Button", Palette.Ink);
			theme.SetColor("font_disabled_color", "Button", Palette.InkFaded);
			theme.SetFont("font", "Button", Serif);
			theme.SetFontSize("font_size", "Button", 17);

			// CheckBox herda de Button: sem estas caixas vazias, cada caixinha viraria um botão de pergaminho.
			foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
				theme.SetStylebox(state, "CheckBox", new StyleBoxEmpty());
			theme.SetColor("font_color", "CheckBox", Palette.Ink);
			theme.SetColor("font_hover_color", "CheckBox", Palette.Ink);
			theme.SetColor("font_pressed_color", "CheckBox", Palette.Ink);

			theme.SetStylebox("background", "ProgressBar", Box(Palette.Ink.Lightened(0.1f), Palette.Ink, 1, 3, 0));
			theme.SetStylebox("fill", "ProgressBar", Box(Palette.Health, Palette.Health, 0, 3, 0));

			theme.SetStylebox("panel", "TooltipPanel", Box(Palette.Parchment, Palette.Ink, 1, 4, 8));
			theme.SetColor("font_color", "TooltipLabel", Palette.Ink);
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

		private static void Variation(Theme theme, string name, string baseType) => theme.SetTypeVariation(name, baseType);
	}
}
