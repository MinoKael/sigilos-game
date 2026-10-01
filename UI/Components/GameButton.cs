using System;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>O peso visual de um <see cref="GameButton"/>.</summary>
	public enum ButtonKind
	{
		/// <summary>A ação principal da tela: âmbar aceso (Lutar, Invocar, Comprar, Iniciar).</summary>
		Primary,

		/// <summary>As outras ações: madeira com moldura de ouro.</summary>
		Secondary,

		/// <summary>O que não tem volta: vermelho (Liberar, Vender, Parar).</summary>
		Danger,
	}

	/// <summary>
	/// O botão de texto do jogo: diz o que faz, com um símbolo opcional na frente e, numa segunda linha,
	/// o custo (símbolo e número: "⚡ 5"). Alto o bastante para o dedo (<see cref="GameTheme.Touch"/>),
	/// afunda ao apertar e apaga quando desligado. Toda ação que possa gerar dúvida usa este botão; o de
	/// só ícone (<see cref="SigilButton"/>) fica para fechar, voltar, pausar.
	///
	/// O conteúdo (<c>Content/Row</c>: <c>Icon</c>, <c>Text</c> com <c>Label</c> e <c>Cost</c>) é desenhado
	/// por cima do botão e não recebe toque. O <c>Button</c> não mede filhos (e ignora
	/// <c>_GetMinimumSize</c>), então o tamanho mínimo é refeito a cada mudança do conteúdo.
	/// </summary>
	public partial class GameButton : Button
	{
		private readonly MarginContainer _content = new() { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
		private readonly Label _label = new() { Name = "Label", MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
		private readonly HBoxContainer _cost = new() { Name = "Cost", MouseFilter = MouseFilterEnum.Ignore, Visible = false, Alignment = BoxContainer.AlignmentMode.Center };
		private readonly Doodle? _icon;
		private readonly float _height;
		private readonly ButtonKind _kind;
		private float _width;

		public GameButton(string text, ButtonKind kind = ButtonKind.Secondary, string? icon = null, float height = GameTheme.Touch)
		{
			_kind = kind;
			_height = height;
			Name = "Button";
			FocusMode = FocusModeEnum.None;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			ClipContents = false;

			var (fill, border) = Tones(kind);
			AddThemeStyleboxOverride("normal", Box(fill, border, false));
			AddThemeStyleboxOverride("hover", Box(fill.Lightened(0.12f), border.Lightened(0.2f), true));
			AddThemeStyleboxOverride("pressed", Box(fill.Darkened(0.18f), border, false, pressed: true));
			AddThemeStyleboxOverride("hover_pressed", Box(fill.Darkened(0.18f), border, false, pressed: true));
			AddThemeStyleboxOverride("disabled", Box(Palette.Disabled, Palette.Disabled.Lightened(0.12f), false));
			AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			_content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_content.AddThemeConstantOverride("margin_left", 18);
			_content.AddThemeConstantOverride("margin_right", 18);
			_content.AddThemeConstantOverride("margin_top", 4);
			_content.AddThemeConstantOverride("margin_bottom", 6);
			AddChild(_content);

			var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			row.AddThemeConstantOverride("separation", 10);
			_content.AddChild(row);

			var size = height >= GameTheme.Touch ? 20 : 17;
			if (icon != null)
			{
				_icon = Doodle.Icon(Art.Icon(icon), (int)(height * 0.5f), Ink);
				_icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
				row.AddChild(_icon);
			}

			var lines = new VBoxContainer { Name = "Text", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			lines.AddThemeConstantOverride("separation", -2);
			_label.Text = text;
			_label.HorizontalAlignment = HorizontalAlignment.Center;
			_label.AddThemeFontOverride("font", GameTheme.Serif);
			_label.AddThemeFontSizeOverride("font_size", size);
			_label.AddThemeColorOverride("font_color", Palette.Text);
			_label.AddThemeColorOverride("font_outline_color", Outline);
			_label.AddThemeConstantOverride("outline_size", 5);
			lines.AddChild(_label);
			_cost.AddThemeConstantOverride("separation", 4);
			lines.AddChild(_cost);
			row.AddChild(lines);

			_content.MinimumSizeChanged += Fit;
			Juice.Attach(this, 0.95f);
		}

		/// <summary>O texto do botão (o <c>Button.Text</c> fica vazio: quem desenha é o rótulo de dentro).</summary>
		public new string Text
		{
			get => _label.Text;
			set
			{
				_label.Text = value;
				Fit();
			}
		}

		/// <summary>Botão pronto: texto, ação, peso e símbolo. O nó leva o nome dado pelo chamador (<c>Named</c>).</summary>
		public static GameButton Of(string text, Action onPressed, ButtonKind kind = ButtonKind.Secondary, string? icon = null, float height = GameTheme.Touch)
		{
			var button = new GameButton(text, kind, icon, height);
			button.Pressed += onPressed;
			return button;
		}

		/// <summary>A segunda linha: o custo em símbolo e número ("⚡ 5"). Vazio esconde.</summary>
		public GameButton WithCost(string icon, string value)
		{
			Layout.Clear(_cost);
			_cost.Visible = value.Length > 0;
			if (value.Length > 0)
			{
				_cost.AddChild(Doodle.Icon(Art.Icon(icon), 18, Ink).Named("Icon"));
				var amount = new Label { Name = "Value", Text = value, MouseFilter = MouseFilterEnum.Ignore };
				amount.AddThemeFontOverride("font", GameTheme.Serif);
				amount.AddThemeFontSizeOverride("font_size", 16);
				amount.AddThemeColorOverride("font_color", Palette.Text);
				amount.AddThemeColorOverride("font_outline_color", Outline);
				amount.AddThemeConstantOverride("outline_size", 4);
				_cost.AddChild(amount);
			}

			Fit();
			return this;
		}

		/// <summary>Largura mínima, para botões lado a lado ficarem iguais.</summary>
		public GameButton Wide(float width)
		{
			_width = width;
			Fit();
			return this;
		}

		public override void _Ready() => Fit();

		/// <summary>O tamanho mínimo: o do conteúdo, com a altura de toque e a largura pedida.</summary>
		private void Fit()
		{
			var content = _content.GetCombinedMinimumSize();
			CustomMinimumSize = new Vector2(Mathf.Max(content.X, _width), Mathf.Max(content.Y, _height));
		}

		public override void _Draw()
		{
			// Desligado, o conteúdo apaga junto com a madeira.
			_content.Modulate = Disabled ? new Color(1, 1, 1, 0.45f) : Colors.White;
		}

		/// <summary>A cor do símbolo: creme no âmbar e no vermelho, ouro na madeira.</summary>
		private Color Ink => _kind == ButtonKind.Secondary ? Palette.Gold : Palette.Text;

		private Color Outline => _kind switch
		{
			ButtonKind.Primary => Palette.PrimaryDark.Darkened(0.35f),
			ButtonKind.Danger => Palette.DangerDark.Darkened(0.3f),
			_ => new Color(0.08f, 0.05f, 0.03f),
		};

		private static (Color Fill, Color Border) Tones(ButtonKind kind) => kind switch
		{
			ButtonKind.Primary => (Palette.Primary, Palette.PrimaryDark),
			ButtonKind.Danger => (Palette.Danger, Palette.DangerDark),
			_ => (Palette.Button, Palette.GoldDark),
		};

		/// <summary>A madeira: cantos redondos, borda de baixo mais grossa (o degrau) que some ao apertar.</summary>
		private static StyleBoxFlat Box(Color fill, Color border, bool glow, bool pressed = false)
		{
			var box = GameTheme.Box(fill, border, 2, 10, 0);
			box.BorderWidthBottom = pressed ? 2 : 5;
			box.ExpandMarginTop = pressed ? -2 : 0;
			box.ShadowColor = glow ? new Color(Palette.Gold, 0.25f) : new Color(0, 0, 0, 0.35f);
			box.ShadowSize = glow ? 6 : 3;
			box.ShadowOffset = glow ? Vector2.Zero : new Vector2(0, 2);
			return box;
		}
	}
}
