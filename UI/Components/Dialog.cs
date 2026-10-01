using System;
using System.Linq;
using Godot;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A janela por cima da tela, no lugar de toda dica e de toda pergunta: um painel de couro com o
	/// título, o ✕ de fechar, o conteúdo (<see cref="Body"/>, com rolagem quando não cabe) e os botões de
	/// ação embaixo (<see cref="AddAction"/>).
	///
	/// - Com <c>anchor</c>, é contextual: abre colada no elemento que o jogador tocou (embaixo dele, ou em
	///   cima quando não cabe), com uma seta apontando para ele, e a tela escurece pouco.
	/// - Sem, abre no centro, e a tela escurece mais.
	///
	/// Tocar fora, o ✕, Esc e o botão Voltar do celular fecham (<see cref="Dismissable"/> falso deixa só
	/// os botões de ação). Fechar não mexe em nada do jogo: quem precisa saber assina <see cref="Closed"/>.
	/// </summary>
	public partial class Dialog : ColorRect
	{
		public const float DefaultWidth = 560;

		/// <summary>Espaço mínimo entre a janela e a borda da tela.</summary>
		private const float Edge = 16;

		/// <summary>O tamanho da seta que aponta para o elemento de origem.</summary>
		private const float Arrow = 12;

		private readonly PanelContainer _panel = new() { Name = "Panel" };
		private readonly ScrollContainer _scroll = new() { Name = "Scroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		private readonly HBoxContainer _actions = new() { Name = "Actions", Alignment = BoxContainer.AlignmentMode.End, Visible = false };
		private readonly SigilButton _close;

		/// <summary>
		/// O texto do meio do cabeçalho (<see cref="SetCaption"/>). Com ele, o título, ele e o lado do ✕
		/// dividem a linha em três partes iguais, e o texto fica no centro da janela.
		/// </summary>
		private readonly Label _caption = new()
		{
			Name = "Caption",
			ThemeTypeVariation = GameTheme.Heading,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Visible = false,
		};

		private readonly HBoxContainer _right = new() { Name = "Right", Alignment = BoxContainer.AlignmentMode.End };
		private readonly Control? _anchor;
		private readonly float _width;
		private bool _closed;
		private bool _dismissable = true;

		/// <summary>Onde a seta encosta no painel e para onde ela aponta (coordenadas locais); nulo sem âncora.</summary>
		private (Vector2 Base, Vector2 Tip)? _arrow;

		private Dialog(string title, float width, Control? anchor, string name)
		{
			_anchor = anchor;
			_width = width;
			Name = name;
			Color = new Color(0, 0, 0, anchor == null ? 0.6f : 0.35f);
			MouseFilter = MouseFilterEnum.Stop;
			ZIndex = 80;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			_panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 18));
			_panel.CustomMinimumSize = new Vector2(width, 0);
			AddChild(_panel);

			var column = new VBoxContainer { Name = "Column" };
			column.AddThemeConstantOverride("separation", 12);
			_panel.AddChild(column);

			var header = new HBoxContainer { Name = "Header" };
			header.AddThemeConstantOverride("separation", 10);
			var heading = new Label
			{
				Name = "Title",
				Text = title,
				ThemeTypeVariation = GameTheme.Heading,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			header.AddChild(heading);
			header.AddChild(_caption);
			_close = SigilButton.Of("cancel", Close, 48).Named("Close");
			_close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			_right.AddChild(_close);
			header.AddChild(_right);
			column.AddChild(header);

			Body.AddThemeConstantOverride("separation", 12);
			Body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_scroll.AddChild(Body);
			column.AddChild(_scroll);

			_actions.AddThemeConstantOverride("separation", 14);
			column.AddChild(_actions);

			Body.MinimumSizeChanged += QueueFit;
			Resized += QueueFit;
		}

		/// <summary>O conteúdo: tudo o que a janela mostra entre o título e os botões.</summary>
		public VBoxContainer Body { get; } = new() { Name = "Body" };

		/// <summary>Um nome no meio do cabeçalho, na mesma linha do título e do ✕ (o conjunto da runa na ficha dela).</summary>
		public void SetCaption(string text, Color color)
		{
			_caption.Text = text;
			_caption.AddThemeColorOverride("font_color", color);
			_caption.Visible = text.Length > 0;
			_right.SizeFlagsHorizontal = _caption.Visible ? SizeFlags.ExpandFill : SizeFlags.Fill;
		}

		/// <summary>Tocar fora, o ✕, Esc e Voltar fecham; falso esconde o ✕ e deixa só os botões de ação (escolha obrigatória).</summary>
		public bool Dismissable
		{
			get => _dismissable;
			set
			{
				_dismissable = value;
				_close.Visible = value;
			}
		}

		/// <summary>A janela fechou, por qualquer caminho.</summary>
		public event Action? Closed;

		/// <summary>
		/// Abre por cima da tela de <paramref name="from"/>. Com <paramref name="anchor"/>, abre colada nele
		/// (a janela contextual); sem, no centro. <paramref name="name"/> é o nome do nó (<c>RuneDialog</c>).
		/// </summary>
		public static Dialog Open(Control from, string title, float width = DefaultWidth, Control? anchor = null, string name = "Dialog")
		{
			var dialog = new Dialog(title, width, anchor, name);
			Layout.Host(from).AddChild(dialog);
			return dialog;
		}

		/// <summary>Janela contextual só de texto (o que antes seria uma dica), colada em <paramref name="anchor"/>.</summary>
		public static Dialog Info(Control anchor, string title, string text, float width = 460)
		{
			var dialog = Open(anchor, title, width, anchor, "InfoDialog");
			dialog.Body.AddChild(RichText.Label(text, width - 40).Named("Text"));
			return dialog;
		}

		/// <summary>A pergunta de confirmação: o texto, Cancelar e o botão que confirma (com o peso dele).</summary>
		public static Dialog Confirm(Control from, string title, string text, string confirm, Action onConfirmed, ButtonKind kind = ButtonKind.Primary)
		{
			var dialog = Open(from, title, 520, null, "ConfirmDialog");
			dialog.Body.AddChild(RichText.Label(text, 480).Named("Text"));
			dialog.AddAction(T("common.cancel"), null).Named("Cancel");
			dialog.AddAction(confirm, onConfirmed, kind).Named("Confirm");
			return dialog;
		}

		/// <summary>Fecha a janela de cima de <paramref name="root"/> que aceita fechar. Falso se não havia nenhuma.</summary>
		public static bool CloseTop(Node root)
		{
			var top = root.GetChildren().OfType<Dialog>().LastOrDefault(d => !d._closed && d.Dismissable);
			top?.Close();
			return top != null;
		}

		/// <summary>Um botão na fileira de baixo. Com <paramref name="closes"/>, a janela fecha antes de agir.</summary>
		public GameButton AddAction(string text, Action? onPressed, ButtonKind kind = ButtonKind.Secondary, bool closes = true, string? icon = null)
		{
			var button = new GameButton(text, kind, icon) { Name = $"Action{_actions.GetChildCount() + 1}" };
			button.Pressed += () =>
			{
				if (closes)
					Close();
				onPressed?.Invoke();
			};
			_actions.Visible = true;
			_actions.AddChild(button);
			return button;
		}

		/// <summary>Tira os botões de ação (para quem remonta a janela).</summary>
		public void ClearActions()
		{
			Layout.Clear(_actions);
			_actions.Visible = false;
		}

		public void Close()
		{
			if (_closed)
				return;
			_closed = true;
			QueueFree();
			Closed?.Invoke();
		}

		public override void _Ready() => QueueFit();

		public override void _GuiInput(InputEvent @event)
		{
			if (Dismissable && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right })
			{
				AcceptEvent();
				Close();
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (!@event.IsActionPressed("ui_cancel"))
				return;
			GetViewport().SetInputAsHandled();
			if (Dismissable)
				Close();
		}

		public override void _Draw()
		{
			if (_arrow is not { } arrow)
				return;

			// A seta: um triângulo de couro com a borda de ouro, do painel até perto do elemento.
			var direction = (arrow.Tip - arrow.Base).Normalized();
			var side = new Vector2(-direction.Y, direction.X) * Arrow;
			var points = new[] { arrow.Base + side, arrow.Tip, arrow.Base - side };
			DrawColoredPolygon(points, Palette.Panel);
			DrawPolyline(points, Palette.Gold, 2, true);
		}

		private void QueueFit() => Callable.From(Fit).CallDeferred();

		/// <summary>
		/// Mede e põe a janela no lugar: a rolagem cresce até o conteúdo caber ou até a tela acabar, e o
		/// painel fica colado na âncora (ou no centro), sempre dentro da tela.
		/// </summary>
		private void Fit()
		{
			if (!IsInsideTree() || _closed)
				return;

			var screen = Size;
			var chrome = _panel.GetCombinedMinimumSize().Y - _scroll.CustomMinimumSize.Y;
			var room = screen.Y - 2 * Edge - chrome - (_anchor != null ? Arrow * 2 : 0);
			var content = Body.GetCombinedMinimumSize().Y;
			_scroll.CustomMinimumSize = new Vector2(0, Mathf.Clamp(content, 0, Mathf.Max(60, room)));

			var width = Mathf.Min(_width, screen.X - 2 * Edge);
			_panel.CustomMinimumSize = new Vector2(width, 0);
			var size = new Vector2(width, _panel.GetCombinedMinimumSize().Y);
			_panel.Size = size;

			if (_anchor == null || !IsInstanceValid(_anchor) || !_anchor.IsInsideTree())
			{
				_arrow = null;
				_panel.Position = ((screen - size) / 2).Round();
				QueueRedraw();
				return;
			}

			var target = _anchor.GetGlobalRect();
			target.Position -= GetGlobalRect().Position;
			var x = Mathf.Clamp(target.GetCenter().X - size.X / 2, Edge, screen.X - size.X - Edge);
			var below = target.End.Y + Arrow + 4;
			var above = target.Position.Y - Arrow - 4 - size.Y;
			float y;
			Vector2 arrowBase;
			Vector2 tip;
			var pointX = Mathf.Clamp(target.GetCenter().X, x + 28, x + size.X - 28);
			if (below + size.Y <= screen.Y - Edge)
			{
				y = below;
				arrowBase = new Vector2(pointX, y + 2);
				tip = new Vector2(pointX, target.End.Y + 3);
			}
			else if (above >= Edge)
			{
				y = above;
				arrowBase = new Vector2(pointX, y + size.Y - 2);
				tip = new Vector2(pointX, target.Position.Y - 3);
			}
			else
			{
				// Não cabe em cima nem embaixo: fica ao lado, na altura que der.
				y = Mathf.Clamp(target.GetCenter().Y - size.Y / 2, Edge, screen.Y - size.Y - Edge);
				var right = target.End.X + Arrow + 4 + size.X <= screen.X - Edge;
				x = right ? target.End.X + Arrow + 4 : Mathf.Max(Edge, target.Position.X - Arrow - 4 - size.X);
				var pointY = Mathf.Clamp(target.GetCenter().Y, y + 28, y + size.Y - 28);
				arrowBase = new Vector2(right ? x + 2 : x + size.X - 2, pointY);
				tip = new Vector2(right ? target.End.X + 3 : target.Position.X - 3, pointY);
			}

			_panel.Position = new Vector2(x, y).Round();
			_arrow = arrowBase.DistanceTo(tip) > 4 ? (arrowBase, tip) : null;
			QueueRedraw();
		}
	}
}
