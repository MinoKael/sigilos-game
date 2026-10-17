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

		/// <summary>O que não tem volta: vermelho (Soltar, Vender, Parar).</summary>
		Danger,

		/// <summary>Sem madeira nem moldura: parece texto solto (o "Esqueci a senha" do login).</summary>
		Text,
	}

	/// <summary>
	/// O botão de texto do jogo: diz o que faz, com um símbolo opcional na frente e, numa segunda linha,
	/// o custo (símbolo e número: "⚡ 5"). Alto o bastante para o dedo (<see cref="GameTheme.Touch"/>),
	/// afunda ao apertar e apaga quando desligado. Toda ação que possa gerar dúvida usa este botão; o de
	/// só ícone (<see cref="SigilButton"/>) fica para fechar, voltar, pausar.
	///
	/// O conteúdo (<c>Content/Row</c>: <c>Icon</c>, <c>Text</c> com <c>Label</c> e <c>Cost</c>) é desenhado
	/// por cima do botão; foco, afundar, apagar desligado e medir o conteúdo vêm de <see cref="TouchButton"/>.
	/// As cores de cada peso vêm de <see cref="Tones"/>.
	/// </summary>
	public partial class GameButton : TouchButton
	{
		/// <summary>A margem de cada lado do conteúdo.</summary>
		private const int Padding = 18;

		/// <summary>A altura de um botão <see cref="ButtonKind.Text"/>: sem madeira, não precisa da altura de toque inteira.</summary>
		public const float TextHeight = 36;

		private readonly Label _label = new() { Name = "Label", MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
		private readonly HBoxContainer _cost = new() { Name = "Cost", MouseFilter = MouseFilterEnum.Ignore, Visible = false, Alignment = BoxContainer.AlignmentMode.Center };
		private readonly VBoxContainer _lines = new()
		{
			Name = "Text",
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};

		private readonly HBoxContainer _row = new() { Name = "Row", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		private readonly float _height;
		private Doodle? _icon;
		private string? _iconName;
		private ButtonKind _kind;
		private float _width;
		private bool _fixed;
		private string? _blockTitle;
		private string? _blockReason;

		public GameButton(string text, ButtonKind kind = ButtonKind.Secondary, string? icon = null, float height = GameTheme.Touch)
		{
			_height = height;

			Name = "Button";
			ClipContents = true;
			SizeFlagsHorizontal = SizeFlags.ExpandFill;
			CustomMinimumSize = new Vector2(0, height);

			Pad(Padding, 4, 6);
			_row.AddThemeConstantOverride("separation", Space.Regular);
			Content.AddChild(_row);

			_lines.AddThemeConstantOverride("separation", -2); // Negativo: o texto e o custo colados, uma peça só.

			_label.Text = text;
			_label.HorizontalAlignment = HorizontalAlignment.Center;
			_label.ClipText = false;
			_label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			_label.AddThemeFontOverride("font", GameTheme.Serif);
			_lines.AddChild(_label);

			_cost.AddThemeConstantOverride("separation", Space.Tight);
			_lines.AddChild(_cost);
			_row.AddChild(_lines);

			ShowLines();
			Kind = kind;
			IconName = icon;
		}

		/// <summary>Um botão vazio de peso secundário, para o inicializador de objeto (<c>new GameButton { Text = ..., Kind = ... }</c>).</summary>
		public GameButton() : this("")
		{
		}

		/// <summary>O texto do botão (o <c>Button.Text</c> fica vazio: quem desenha é o rótulo de dentro).</summary>
		public new string Text
		{
			get => _label.Text;
			set
			{
				_label.Text = value;
				ShowLines();
				Fit();
			}
		}

		/// <summary>O peso do botão: troca as caixas, a tinta dos símbolos e o contorno das letras.</summary>
		public ButtonKind Kind
		{
			get => _kind;
			set
			{
				_kind = value;
				Restyle();
				Fit();
			}
		}

		/// <summary>O símbolo na frente do texto, pelo nome em Assets/Icons (<c>"grindstone"</c>); nulo tira. (O <c>Button.Icon</c> da Godot fica sem uso.)</summary>
		public string? IconName
		{
			get => _iconName;
			set
			{
				_iconName = value;
				if (_icon != null)
					Layout.Discard(_icon);
				_icon = null;
				if (value != null)
				{
					_icon = Doodle.Icon(Art.Icon(value), (int)(_height * 0.5f), Tones.Ink(_kind));
					_icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
					_row.AddChild(_icon);
					_row.MoveChild(_icon, 0);
				}

				Fit();
			}
		}

		/// <summary>Botão pronto: texto, ação, peso e símbolo. O nó leva o nome dado pelo chamador (<c>Named</c>).</summary>
		public static GameButton Of(string text, Action onPressed, ButtonKind kind = ButtonKind.Secondary, string? icon = null, float height = GameTheme.Touch)
		{
			var button = new GameButton(text, kind, icon, height);
			button.Pressed += () =>
			{
				if (button._blockReason != null)
					Dialog.Info(button, button._blockTitle ?? button.Text, button._blockReason, 420);
				else
					onPressed();
			};
			return button;
		}

		/// <summary>O botão está apagado por um motivo (e o toque diz qual).</summary>
		public bool Blocked => _blockReason != null;

		/// <summary>
		/// Apagado com explicação: o botão fica no lugar, com o visual de desligado, e o toque abre uma janela
		/// com o motivo em vez de agir. Só vale para o botão de <see cref="Of"/>. Motivo nulo religa.
		/// </summary>
		public GameButton Block(string? reason, string? title = null)
		{
			if (_blockReason == reason && _blockTitle == title)
				return this;

			_blockReason = reason;
			_blockTitle = title;
			Restyle();
			QueueRedraw();
			return this;
		}

		public override void _Draw()
		{
			base._Draw();
			if (_blockReason != null)
				Content.Modulate = new Color(1, 1, 1, Fade.Disabled);
		}

		/// <summary>A segunda linha: o custo em símbolo e número ("⚡ 5"). Vazio esconde.</summary>
		public GameButton WithCost(string? icon, string value)
		{
			Layout.Clear(_cost);
			_cost.Visible = value.Length > 0;
			if (value.Length > 0)
			{
				if (icon != null) _cost.AddChild(Doodle.Icon(Art.Icon(icon), 18, Tones.Ink(_kind)).Named("Icon"));
				var amount = new Label { Name = "Value", Text = value, MouseFilter = MouseFilterEnum.Ignore };
				amount.AddThemeFontOverride("font", GameTheme.Serif);
				amount.AddThemeFontSizeOverride("font_size", FontSize.Body);
				amount.AddThemeColorOverride("font_color", Palette.Text);
				amount.AddThemeColorOverride("font_outline_color", Tones.Outline(_kind));
				amount.AddThemeConstantOverride("outline_size", 4);
				_cost.AddChild(amount);
			}

			ShowLines();
			Fit();
			return this;
		}

		/// <summary>Sem texto nem custo (só o símbolo) a coluna de texto some: o símbolo fica no meio do botão, não ao lado de um vão.</summary>
		private void ShowLines() => _lines.Visible = _label.Text.Length > 0 || _cost.Visible;

		/// <summary>
		/// Altura exata, a do construtor: o texto não a faz crescer (o que não cabe é cortado). Para botões lado a lado
		/// que precisam ficar todos da mesma altura, com nome em uma ou duas linhas e custo ou não.
		/// </summary>
		public GameButton FixedHeight()
		{
			_fixed = true;
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

		/// <summary>
		/// O tamanho mínimo: a altura de toque (ou a do conteúdo) e, na largura, a pedida em <see cref="Wide"/> ou
		/// pelo menos as margens, o símbolo e a palavra mais longa. O rótulo quebra linha e não tem largura mínima
		/// própria: sem esse piso, numa fileira sem largura (HBox, HFlow) o botão encolhia a zero e o texto saía
		/// uma letra por linha. Numa célula de grade mais larga, o botão continua preenchendo a célula.
		/// </summary>
		protected override Vector2? MinimumFor(Vector2 content) => new Vector2(Mathf.Max(_width, MinimumWidth()), _fixed ? _height : Mathf.Max(content.Y, _height));

		/// <summary>
		/// As margens, o símbolo e a palavra mais longa do texto (ou o custo, se for maior) numa linha. O de peso
		/// <see cref="ButtonKind.Text"/> não quebra: conta o texto inteiro (solto no meio de uma coluna, encolhido
		/// ao centro, a palavra mais longa o partia em duas linhas).
		/// </summary>
		private float MinimumWidth()
		{
			var font = _label.GetThemeFont("font");
			var size = _label.GetThemeFontSize("font_size");
			var word = 0f;
			var pieces = _kind == ButtonKind.Text ? new[] { _label.Text } : _label.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			foreach (var piece in pieces)
				word = Mathf.Max(word, font.GetStringSize(piece, HorizontalAlignment.Left, -1, size).X);
			var cost = _cost.Visible ? _cost.GetCombinedMinimumSize().X : 0;
			var icon = _icon != null ? _icon.CustomMinimumSize.X + 10 : 0;
			return Padding * 2 + icon + Mathf.Max(word, cost) + 6;
		}

		/// <summary>As caixas de cada estado, a tinta dos símbolos e o contorno e a cor das letras do peso atual (o de texto é de ouro).</summary>
		private void Restyle()
		{
			var (fill, border) = Tones.Of(_kind);
			var text = _kind == ButtonKind.Text;
			if (_blockReason != null && !text)
			{
				var off = Box(Palette.Disabled, Palette.Disabled.Lightened(0.12f), false);
				foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
					AddThemeStyleboxOverride(state, off);
				AddThemeStyleboxOverride("disabled", off);
				return;
			}

			AddThemeStyleboxOverride("normal", text ? new StyleBoxEmpty() : Box(fill, border, false));
			AddThemeStyleboxOverride("hover", text ? new StyleBoxEmpty() : Box(fill.Lightened(0.12f), Palette.Gold, true));
			AddThemeStyleboxOverride("pressed", text ? new StyleBoxEmpty() : Box(fill.Darkened(0.18f), border, false, pressed: true));
			AddThemeStyleboxOverride("hover_pressed", text ? new StyleBoxEmpty() : Box(fill.Darkened(0.18f), border, false, pressed: true));
			AddThemeStyleboxOverride("disabled", text ? new StyleBoxEmpty() : Box(Palette.Disabled, Palette.Disabled.Lightened(0.12f), false));
			_label.AddThemeConstantOverride("outline_size", text ? 0 : 5);
			_label.AutowrapMode = text ? TextServer.AutowrapMode.Off : TextServer.AutowrapMode.WordSmart;
			_label.AddThemeFontSizeOverride("font_size", text || _height >= GameTheme.Touch ? FontSize.Strong : FontSize.Compact);
			_label.AddThemeColorOverride("font_color", text ? Tones.Ink(_kind) : Palette.Text);
			_label.AddThemeColorOverride("font_outline_color", Tones.Outline(_kind));

			var ink = Tones.Ink(_kind);
			_icon?.SetInk(ink);
			foreach (var child in _cost.GetChildren())
			{
				if (child is Doodle icon)
					icon.SetInk(ink);
				else if (child is Label amount)
					amount.AddThemeColorOverride("font_outline_color", Tones.Outline(_kind));
			}
		}

		/// <summary>A madeira: cantos redondos, borda de baixo mais grossa (o degrau) que some ao apertar.</summary>
		private static StyleBoxFlat Box(Color fill, Color border, bool glow, bool pressed = false)
		{
			// O botão corta o que passa da borda (ClipContents): sombra para fora vira um brilho de cantos retos.
			// Por isso o brilho do mouse é a moldura mais grossa e mais clara, por dentro, com o raio certo.
			var box = GameTheme.Box(fill, border, glow ? 3 : 2, Radius.Button, 0);
			box.BorderWidthBottom = pressed ? 2 : 5;
			box.ExpandMarginTop = pressed ? -2 : 0;
			if (!glow)
			{
				box.ShadowColor = new Color(0, 0, 0, 0.35f);
				box.ShadowSize = 3;
				box.ShadowOffset = new Vector2(0, 2);
			}

			return box;
		}
	}
}
