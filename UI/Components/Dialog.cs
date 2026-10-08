using System;
using System.Linq;
using Godot;
using Sigilos.UI.Audio;
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
	/// - Em tela cheia (<see cref="Full"/>), cobre a tela toda: o fundo preto meio transparente e, por cima
	///   dele, o cabeçalho, o conteúdo ocupando o que sobra (num chão escuro, para o jogo atrás não se
	///   misturar com o texto) e a fileira de baixo.
	///
	/// Tocar fora, o ✕, Esc e o botão Voltar do celular fecham (<see cref="Dismissable"/> falso deixa só
	/// os botões de ação). Fechar não mexe em nada do jogo: quem precisa saber assina <see cref="Closed"/>.
	/// Abrir e fechar soam como reserva (<see cref="Sfx.Fallback"/>), acima do clique que abriu.
	/// </summary>
	public partial class Dialog : ColorRect
	{
		public const float DefaultWidth = 560;

		/// <summary>Espaço mínimo entre a janela e a borda da tela.</summary>
		private const float Edge = 16;

		/// <summary>O tamanho da seta que aponta para o elemento de origem.</summary>
		private const float Arrow = 12;

		/// <summary>A camada da primeira janela, por cima das telas e dos avisos.</summary>
		private const int Bottom = 80;

		/// <summary>
		/// Cada janela fica esta camada acima das abertas antes dela. O conteúdo de uma janela pode subir
		/// dentro dela (a luta pequena da Batalha automática, com a moldura, sobe até 30), e nada disso passa
		/// por cima da pergunta que abriu depois.
		/// </summary>
		private const int Layer = 40;

		private readonly PanelContainer _panel = new() { Name = "Panel" };
		/// <summary>
		/// A rolagem do conteúdo guarda sempre o lugar da barra (<c>Reserve</c>): o conteúdo tem a mesma largura
		/// com a barra ou sem ela. Sem isso, um texto no limite quebrava em mais linhas quando a barra
		/// aparecia, em menos quando sumia, e a janela trocava de tamanho sem parar até o jogo cair (a
		/// fusão de alguns monstros).
		/// </summary>
		private readonly ScrollContainer _scroll = new() { Name = "Scroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Reserve, ScrollVerticalCustomStep = Layout.WheelStep };
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
		private readonly bool _full;
		private bool _closed;
		private bool _dismissable = true;

		/// <summary>Onde a seta encosta no painel e para onde ela aponta (coordenadas locais); nulo sem âncora.</summary>
		private (Vector2 Base, Vector2 Tip)? _arrow;

		private Dialog(string title, float width, Control? anchor, string name, bool full = false)
		{
			_anchor = anchor;
			_width = width;
			_full = full;
			Name = name;
			Color = new Color(0, 0, 0, full ? 0.5f : anchor == null ? 0.6f : 0.35f);
			MouseFilter = MouseFilterEnum.Stop;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			if (full)
			{
				// Sem o couro: o próprio fundo escurecido é a janela, com a mesma margem das telas.
				var box = new StyleBoxEmpty();
				box.SetContentMarginAll(Layout.ScreenMargin);
				_panel.AddThemeStyleboxOverride("panel", box);
				_panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				_scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			}
			else
			{
				_panel.AddThemeStyleboxOverride("panel", Ornament.Panel(Palette.Panel, Palette.Gold, 18));
				_panel.CustomMinimumSize = new Vector2(width, 0);
			}

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
			DragScroll.Enable(_scroll);
			if (full)
			{
				var sheet = new PanelContainer { Name = "Sheet", SizeFlagsVertical = SizeFlags.ExpandFill };
				sheet.AddThemeStyleboxOverride("panel", GameTheme.Box(new Color(Palette.Inset, 0.88f), Palette.GoldDark, 1, 12, 12));
				sheet.AddChild(_scroll);
				column.AddChild(sheet);
			}
			else
			{
				column.AddChild(_scroll);
			}

			_actions.AddThemeConstantOverride("separation", 14);
			column.AddChild(_actions);

			Body.MinimumSizeChanged += QueueFit;
			// Os botões também: o texto deles quebra linha e, antes de ganhar largura, mede uma letra por
			// linha; sem refazer a conta quando eles encolhem, a janela ficava do tamanho da tela.
			_actions.MinimumSizeChanged += QueueFit;
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

		/// <summary>
		/// Troca o ✕ pela seta de voltar: para a janela que é um lugar para onde se volta depois (a da Batalha
		/// automática, que fechar não para), e não uma pergunta que se cancela.
		/// </summary>
		public void UseBackButton() => _close.SetIcon(Art.Icon("back"));

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
			return Show(from, new Dialog(title, width, anchor, name));
		}

		/// <summary>
		/// Abre cobrindo a tela toda por cima da tela de <paramref name="from"/> (o Chat global): o conteúdo rola
		/// no espaço que sobra entre o cabeçalho e a fileira de baixo.
		/// </summary>
		public static Dialog Full(Control from, string title, string name)
		{
			return Show(from, new Dialog(title, 0, null, name, true));
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

		/// <summary>Põe <paramref name="dialog"/> por cima da tela de <paramref name="from"/>, uma camada acima das janelas abertas (<see cref="Layer"/>).</summary>
		private static Dialog Show(Control from, Dialog dialog)
		{
			var host = Layout.Host(from);
			dialog.ZIndex = host.GetChildren().OfType<Dialog>().Where(d => !d._closed).Select(d => d.ZIndex + Layer).DefaultIfEmpty(Bottom).Max();
			host.AddChild(dialog);
			Sfx.Fallback("ui.dialog_open", 2);
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

		/// <summary>Um controle na fileira de baixo, na ordem em que chega (o campo de texto do chat antes do Enviar).</summary>
		public void AddFooter(Control control)
		{
			_actions.Visible = true;
			_actions.AddChild(control);
		}

		/// <summary>Rola o conteúdo até <paramref name="control"/> aparecer, depois de ele se arrumar (a linha que acabou de chegar).</summary>
		public void Reveal(Control control) => Layout.Reveal(_scroll, control);

		/// <summary>O conteúdo está rolado até o fim (ou cabe sem rolar).</summary>
		public bool AtEnd
		{
			get
			{
				var bar = _scroll.GetVScrollBar();
				return _scroll.ScrollVertical >= bar.MaxValue - bar.Page - 4;
			}
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
			Sfx.Fallback("ui.dialog_close", 2);
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
			if (!IsInsideTree() || _closed || _full)
				return;

			var screen = Size;
			var chrome = _panel.GetCombinedMinimumSize().Y - _scroll.CustomMinimumSize.Y;
			var room = screen.Y - 2 * Edge - chrome - (_anchor != null ? Arrow * 2 : 0);
			var content = Body.GetCombinedMinimumSize().Y;
			_scroll.CustomMinimumSize = new Vector2(0, Mathf.Clamp(content, 0, Mathf.Max(60, room)));

			// A largura pedida é o mínimo: botões de ação demais alargam o painel, e a conta usa o tamanho de
			// verdade para não deixar a janela sair da tela.
			var width = Mathf.Min(_width, screen.X - 2 * Edge);
			_panel.CustomMinimumSize = new Vector2(width, 0);
			var size = _panel.GetCombinedMinimumSize();
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
