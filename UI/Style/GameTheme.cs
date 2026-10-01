using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// O tema da interface: painéis de couro com moldura de ouro (<see cref="Ornament.Panel"/>), pedra
	/// entalhada nos fundos rebaixados, barra de rolagem que é só uma gema deslizando, caixinhas de marcar
	/// que são sigilos acesos e barras de energia entalhadas.
	///
	/// Tudo é pensado para o dedo primeiro (celular deitado, 1280×720 de base): texto grande, botões com
	/// pelo menos <see cref="Touch"/> px de altura e nada que dependa de passar o mouse — não existe dica
	/// (tooltip); o que precisa de explicação abre um <see cref="Components.Dialog"/>.
	///
	/// A fonte do jogo inteiro vem de Assets/Fonts, com uma fonte do sistema de reserva para o que ela
	/// não desenha (★, ×, ⟳...). As runas usam a Kehdrai (<see cref="Runes"/>), que troca letra por runa:
	/// os Glifos são texto, nítidos em qualquer tamanho. As telas pedem os papéis pelo nome de variação
	/// (<see cref="Title"/>, <see cref="Heading"/>...), nunca por cor solta. Os botões de texto são os
	/// <see cref="Components.GameButton"/>; os de ícone (<see cref="Components.SigilButton"/>) ficam para o
	/// que todo mundo reconhece (fechar, voltar, pausa).
	/// </summary>
	public static class GameTheme
	{
		/// <summary>Altura mínima de tudo o que se toca: botão, aba, linha de lista.</summary>
		public const float Touch = 56;

		/// <summary>Tamanho do texto corrido.</summary>
		public const int BodySize = 18;

		/// <summary>Tamanho do texto pequeno (legendas, valores secundários).</summary>
		public const int SmallSize = 15;

		/// <summary>Título grande de tela.</summary>
		public const string Title = "TitleLabel";

		/// <summary>Cabeçalho de painel.</summary>
		public const string Heading = "HeadingLabel";

		/// <summary>Texto pequeno e apagado: valores secundários.</summary>
		public const string Faded = "FadedLabel";

		/// <summary>Número em destaque (moedas, custos, níveis).</summary>
		public const string Number = "NumberLabel";

		/// <summary>Pedra entalhada dentro de um painel (listas, barras, cápsulas).</summary>
		public const string InsetPanel = "InsetPanel";

		/// <summary>A fonte do jogo: títulos, números e texto.</summary>
		public static readonly Font Serif = Game();

		/// <summary>A mesma fonte do jogo (o nome fica para quem pede "a fonte do texto").</summary>
		public static readonly Font Sans = Serif;

		/// <summary>A fonte das runas: cada Glifo é uma letra (<see cref="Texts.Rune"/>).</summary>
		public static readonly Font Runes = GD.Load<Font>("res://Assets/Fonts/Kehdrai.ttf");

		/// <summary>A Wezards com a reserva do sistema para os símbolos que ela não tem.</summary>
		private static Font Game()
		{
			var font = GD.Load<FontFile>("res://Assets/Fonts/Chewy.ttf");
			var reserve = new SystemFont { FontNames = new[] { "Georgia", "Segoe UI Symbol", "Segoe UI", "Arial" } };
			font.Fallbacks = new Godot.Collections.Array<Font> { reserve };
			return font;
		}

		public static Theme Build()
		{
			var theme = new Theme { DefaultFont = Sans, DefaultFontSize = BodySize };

			Labels(theme);
			Panels(theme);
			Buttons(theme);
			Toggles(theme);
			Fields(theme);
			Scrollbars(theme);
			Bars(theme);
			Popups(theme);
			return theme;
		}

		private static void Labels(Theme theme)
		{
			theme.SetColor("font_color", "Label", Palette.Text);

			theme.SetTypeVariation(Title, "Label");
			theme.SetFont("font", Title, Serif);
			theme.SetFontSize("font_size", Title, 32);
			theme.SetColor("font_color", Title, Palette.Gold);
			theme.SetColor("font_shadow_color", Title, new Color(0, 0, 0, 0.6f));
			theme.SetConstant("shadow_offset_y", Title, 2);

			theme.SetTypeVariation(Heading, "Label");
			theme.SetFont("font", Heading, Serif);
			theme.SetFontSize("font_size", Heading, 23);
			theme.SetColor("font_color", Heading, Palette.Gold);

			theme.SetTypeVariation(Faded, "Label");
			theme.SetFontSize("font_size", Faded, SmallSize);
			theme.SetColor("font_color", Faded, Palette.TextFaded);

			theme.SetTypeVariation(Number, "Label");
			theme.SetFont("font", Number, Serif);
			theme.SetFontSize("font_size", Number, 19);
			theme.SetColor("font_color", Number, Palette.Text);
			theme.SetColor("font_outline_color", Number, Palette.Background);
			theme.SetConstant("outline_size", Number, 4);

			theme.SetColor("default_color", "RichTextLabel", Palette.Text);

			var line = new StyleBoxLine { Color = Palette.GoldDark, Thickness = 1, GrowBegin = -6, GrowEnd = -6 };
			theme.SetStylebox("separator", "HSeparator", line);
			theme.SetConstant("separation", "HSeparator", 10);
		}

		private static void Panels(Theme theme)
		{
			theme.SetStylebox("panel", "PanelContainer", Ornament.Panel(Palette.Panel, Palette.GoldDark));
			theme.SetStylebox("panel", "Panel", Ornament.Panel(Palette.Panel, Palette.GoldDark, 0));
			theme.SetTypeVariation(InsetPanel, "PanelContainer");
			theme.SetStylebox("panel", InsetPanel, Carved(Palette.Inset, 8));
		}

		private static void Buttons(Theme theme)
		{
			theme.SetStylebox("normal", "Button", Wood(Palette.Button, Palette.GoldDark, false));
			theme.SetStylebox("hover", "Button", Wood(Palette.ButtonHover, Palette.Gold, true));
			theme.SetStylebox("pressed", "Button", Wood(Palette.Inset, Palette.Arcane, true));
			theme.SetStylebox("hover_pressed", "Button", Wood(Palette.Inset, Palette.Arcane, true));
			theme.SetStylebox("disabled", "Button", Wood(Palette.Disabled, Palette.Disabled.Lightened(0.1f), false));
			theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
			theme.SetColor("font_color", "Button", Palette.Text);
			theme.SetColor("font_hover_color", "Button", Palette.Text);
			theme.SetColor("font_pressed_color", "Button", Palette.Arcane);
			theme.SetColor("font_hover_pressed_color", "Button", Palette.Arcane);
			theme.SetColor("font_focus_color", "Button", Palette.Text);
			theme.SetColor("font_disabled_color", "Button", Palette.TextFaded.Darkened(0.2f));
			theme.SetFont("font", "Button", Serif);
			theme.SetFontSize("font_size", "Button", 19);

			// MenuButton e OptionButton herdam do Button, mas precisam das caixas próprias.
			foreach (var type in new[] { "MenuButton", "OptionButton" })
			{
				theme.SetStylebox("normal", type, Wood(Palette.Button, Palette.GoldDark, false));
				theme.SetStylebox("hover", type, Wood(Palette.ButtonHover, Palette.Gold, true));
				theme.SetStylebox("pressed", type, Wood(Palette.Inset, Palette.Arcane, true));
				theme.SetStylebox("disabled", type, Wood(Palette.Disabled, Palette.Disabled, false));
				theme.SetStylebox("focus", type, new StyleBoxEmpty());
			}
		}

		/// <summary>CheckBox e CheckButton viram sigilos que acendem: nada de caixinha ou chave de liga-desliga.</summary>
		private static void Toggles(Theme theme)
		{
			foreach (var type in new[] { "CheckBox", "CheckButton" })
			{
				foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
					theme.SetStylebox(state, type, new StyleBoxEmpty());
				theme.SetIcon("checked", type, Ornament.Sigil(true, Palette.GoldDark, Palette.Arcane));
				theme.SetIcon("unchecked", type, Ornament.Sigil(false, Palette.GoldDark, Palette.Arcane));
				theme.SetIcon("checked_disabled", type, Ornament.Sigil(true, Palette.Disabled, Palette.TextFaded));
				theme.SetIcon("unchecked_disabled", type, Ornament.Sigil(false, Palette.Disabled, Palette.TextFaded));
				theme.SetColor("font_color", type, Palette.Text);
				theme.SetColor("font_hover_color", type, Palette.Arcane);
				theme.SetColor("font_pressed_color", type, Palette.Arcane);
				theme.SetColor("font_hover_pressed_color", type, Palette.Arcane);
			}
		}

		/// <summary>Campo de texto (e-mail, senha): pedra entalhada, que ganha a moldura de ouro ao receber o foco.</summary>
		private static void Fields(Theme theme)
		{
			var normal = Carved(Palette.Inset, 10);
			normal.ContentMarginLeft = normal.ContentMarginRight = 14;
			theme.SetStylebox("normal", "LineEdit", normal);
			var focus = Box(new Color(0, 0, 0, 0), Palette.Gold, 2, 8, 0);
			focus.DrawCenter = false;
			theme.SetStylebox("focus", "LineEdit", focus);
			var disabled = Carved(Palette.Disabled, 10);
			disabled.ContentMarginLeft = disabled.ContentMarginRight = 14;
			theme.SetStylebox("read_only", "LineEdit", disabled);
			theme.SetFont("font", "LineEdit", Serif);
			theme.SetFontSize("font_size", "LineEdit", 20);
			theme.SetColor("font_color", "LineEdit", Palette.Text);
			theme.SetColor("font_uneditable_color", "LineEdit", Palette.TextFaded);
			theme.SetColor("font_placeholder_color", "LineEdit", new Color(Palette.TextFaded, 0.6f));
			theme.SetColor("caret_color", "LineEdit", Palette.Gold);
			theme.SetColor("selection_color", "LineEdit", new Color(Palette.Arcane, 0.35f));
			theme.SetColor("clear_button_color", "LineEdit", Palette.TextFaded);
		}

		/// <summary>Sem trilho: só a gema, meio apagada, que acende sob o mouse.</summary>
		private static void Scrollbars(Theme theme)
		{
			foreach (var type in new[] { "VScrollBar", "HScrollBar" })
			{
				var track = new StyleBoxEmpty();
				track.SetContentMarginAll(2);
				theme.SetStylebox("scroll", type, track);
				theme.SetStylebox("scroll_focus", type, track);
				theme.SetStylebox("grabber", type, Ornament.Gem(Palette.GoldDark));
				theme.SetStylebox("grabber_highlight", type, Ornament.Gem(Palette.Gold));
				theme.SetStylebox("grabber_pressed", type, Ornament.Gem(Palette.Arcane));
				foreach (var icon in new[] { "increment", "increment_highlight", "increment_pressed", "decrement", "decrement_highlight", "decrement_pressed" })
					theme.SetIcon(icon, type, new PlaceholderTexture2D { Size = Vector2.Zero });
			}

			theme.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty());
		}

		/// <summary>Barras de energia entalhadas: sulco escuro na pedra e o brilho por dentro.</summary>
		private static void Bars(Theme theme)
		{
			var groove = Carved(Palette.Inset, 0);
			groove.SetCornerRadiusAll(4);
			theme.SetStylebox("background", "ProgressBar", groove);
			theme.SetStylebox("fill", "ProgressBar", Energy(Palette.Health));
			theme.SetColor("font_color", "ProgressBar", Palette.Text);

			// O controle deslizante é a mesma barra entalhada, com a energia até a gema.
			var track = Carved(Palette.Inset, 0);
			track.SetCornerRadiusAll(6);
			track.ContentMarginTop = track.ContentMarginBottom = 6;
			var energy = Energy(Palette.Arcane);
			energy.SetCornerRadiusAll(6);
			energy.ContentMarginTop = energy.ContentMarginBottom = 6;
			theme.SetStylebox("slider", "HSlider", track);
			theme.SetStylebox("grabber_area", "HSlider", energy);
			theme.SetStylebox("grabber_area_highlight", "HSlider", Energy(Palette.Arcane.Lightened(0.2f)));
			theme.SetIcon("grabber", "HSlider", Ornament.GemIcon(Palette.Gold));
			theme.SetIcon("grabber_highlight", "HSlider", Ornament.GemIcon(Palette.Arcane));
			theme.SetIcon("grabber_disabled", "HSlider", Ornament.GemIcon(Palette.Disabled));
		}

		private static void Popups(Theme theme)
		{
			theme.SetStylebox("panel", "TabContainer", Ornament.Panel(Palette.Panel, Palette.GoldDark, 12));
			theme.SetStylebox("tab_selected", "TabContainer", Wood(Palette.Panel, Palette.Gold, true));
			theme.SetStylebox("tab_unselected", "TabContainer", Wood(Palette.Inset, Palette.GoldDark, false));
			theme.SetStylebox("tab_hovered", "TabContainer", Wood(Palette.PanelLight, Palette.Gold, true));
			theme.SetStylebox("tabbar_background", "TabContainer", new StyleBoxEmpty());
			theme.SetColor("font_selected_color", "TabContainer", Palette.Gold);
			theme.SetColor("font_unselected_color", "TabContainer", Palette.TextFaded);
			theme.SetColor("font_hovered_color", "TabContainer", Palette.Arcane);
			theme.SetFont("font", "TabContainer", Serif);
			theme.SetFontSize("font_size", "TabContainer", 17);

			theme.SetStylebox("panel", "PopupMenu", Ornament.Panel(Palette.Panel, Palette.Gold, 8));
			theme.SetStylebox("hover", "PopupMenu", Box(Palette.PanelLight, Palette.Arcane, 1, 4, 4));
			theme.SetColor("font_color", "PopupMenu", Palette.Text);
			theme.SetColor("font_hover_color", "PopupMenu", Palette.Arcane);
		}

		/// <summary>Caixa simples, para destaques pontuais.</summary>
		public static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius, int margin)
		{
			var box = new StyleBoxFlat { BgColor = background, BorderColor = border, AntiAliasing = true };
			box.SetBorderWidthAll(borderWidth);
			box.SetCornerRadiusAll(radius);
			box.SetContentMarginAll(margin);
			return box;
		}

		/// <summary>Pedra entalhada: fundo escuro com sombra no alto (o sulco) e um filete de luz embaixo.</summary>
		public static StyleBoxFlat Carved(Color background, int margin, Color? border = null)
		{
			var box = Box(background, border ?? background.Darkened(0.5f), 1, 8, margin);
			box.BorderWidthTop = 2;
			box.BorderColor = border ?? new Color(0, 0, 0, 0.55f);
			box.ShadowColor = new Color(Palette.Gold, 0.06f);
			box.ShadowSize = 1;
			box.ShadowOffset = new Vector2(0, 1);
			return box;
		}

		/// <summary>Madeira com borda de ouro; <paramref name="glow"/> acende a aura arcana (hover, apertado).</summary>
		public static StyleBoxFlat Wood(Color background, Color border, bool glow)
		{
			var box = Box(background, border, 2, 7, 8);
			box.BorderWidthBottom = 3;
			box.ContentMarginLeft = box.ContentMarginRight = 14;
			if (glow)
			{
				box.ShadowColor = new Color(Palette.Arcane, 0.28f);
				box.ShadowSize = 6;
			}

			return box;
		}

		/// <summary>O brilho de dentro de uma barra de energia, na cor dada.</summary>
		public static StyleBoxFlat Energy(Color color)
		{
			var box = Box(color, color.Lightened(0.35f), 0, 4, 0);
			box.BorderWidthTop = 1;
			box.ShadowColor = new Color(color, 0.35f);
			box.ShadowSize = 3;
			return box;
		}
	}
}
