using Godot;
using Sigilos.Core.Social;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O balão do Chat global, por cima de qualquer tela, no lugar que a tela marca para ele (<see cref="Follow"/>):
	/// - nas telas com cabeçalho, logo depois do título, num espaço guardado para ele (<see cref="Slot"/>, que
	///   <see cref="Layout.Header"/> já põe);
	/// - no Santuário, no canto da constelação, e na luta, ao lado da rodada (<see cref="Dock"/> e <see cref="Slot"/>);
	/// - numa tela que não marca nada, no canto de cima à esquerda da janela.
	/// Só o símbolo do chat, redondo e meio transparente como o aviso da Batalha automática
	/// (<see cref="AutoBattleBadge"/>). Não conta as linhas que chegam. A borda diz a conexão: verde ao vivo,
	/// apagada sem ela. Tocar abre a janela do chat (<see cref="Pressed"/>). Só aparece jogando na conta.
	///
	/// Ao lado, numa faixa, a última linha que chegou de outra conta ("Nome: texto", ou o feito), por
	/// <see cref="LastSeconds"/> segundos; a seguinte toma o lugar dela e conta de novo. Tocar na faixa também
	/// abre o chat. Com a janela do chat aberta (<see cref="ChatFeed.Reading"/>), a faixa não aparece.
	/// </summary>
	public partial class ChatBubble : Button
	{
		/// <summary>Pequeno: cabe ao lado do título das telas.</summary>
		public const float Side = 36;

		private const float Corner = 2;

		/// <summary>O grupo dos lugares do balão (<see cref="Slot"/>, <see cref="Dock"/>).</summary>
		private const string DockGroup = "ChatDock";

		/// <summary>A tela de agora: o balão fica no primeiro lugar marcado dentro dela.</summary>
		private Control? _screen;

		/// <summary>O lugar onde o balão está; nulo: o canto da janela.</summary>
		private Control? _dock;

		/// <summary>Já há um <see cref="Place"/> marcado para o fim do quadro (só um por vez).</summary>
		private bool _placing;

		/// <summary>Meio transparente: o balão fica por cima de qualquer tela.</summary>
		private const float Opacity = 0.8f;

		private const int IconSize = 22;

		/// <summary>Quanto a última linha fica na faixa, se não chegar outra.</summary>
		private const float LastSeconds = 10;

		private const float LastHeight = 28;

		/// <summary>A faixa cresce com a linha até aqui; o resto do texto vira reticências.</summary>
		private const float LastMaxWidth = 380;

		private const float LastGap = 6;

		private const int LastFontSize = 15;

		private readonly ChatFeed _feed;
		private readonly Doodle _icon = Doodle.Icon(Art.Icon("chat"), IconSize, Palette.Gold);
		private readonly StyleBoxFlat _box;

		private readonly PanelContainer _last = new() { Name = "Last", Visible = false, MouseDefaultCursorShape = CursorShape.PointingHand };
		private readonly Label _lastFrom = new() { Name = "From", VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		private readonly Label _lastText = new() { Name = "Text", VerticalAlignment = VerticalAlignment.Center, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
		private readonly Timer _lastTimer = new() { Name = "LastTimer", OneShot = true, WaitTime = LastSeconds };

		public ChatBubble(ChatFeed feed)
		{
			_feed = feed;
			Name = "ChatBubble";
			FocusMode = FocusModeEnum.None;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			Visible = false;
			ZIndex = 70;
			Modulate = new Color(1, 1, 1, Opacity);

			_box = GameTheme.Box(new Color(Palette.Inset, 0.94f), Palette.GoldDark, 2, (int)(Side / 2), 0);
			_box.ShadowColor = new Color(0, 0, 0, 0.5f);
			_box.ShadowSize = 6;
			foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
				AddThemeStyleboxOverride(state, _box);
			AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			_icon.Name = "Icon";
			_icon.MouseFilter = MouseFilterEnum.Ignore;
			_icon.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
			_icon.GrowHorizontal = _icon.GrowVertical = GrowDirection.Both;
			AddChild(_icon);

			SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
			Place();
			BuildLast();

			feed.Changed += Refresh;
			feed.Added += ShowLast;
			Pressed += HideLast;
			Refresh();
		}

		public override void _EnterTree() => GetViewport().SizeChanged += PlaceLater;

		public override void _ExitTree()
		{
			_feed.Changed -= Refresh;
			_feed.Added -= ShowLast;
			GetViewport().SizeChanged -= PlaceLater;
		}

		/// <summary>
		/// O espaço do balão numa fileira (depois do título da tela, ao lado da rodada na luta): guarda o
		/// lugar dele, para ele não cobrir nada, e o marca como o lugar do balão.
		/// </summary>
		public static Control Slot() => Dock(new Control
		{
			Name = "ChatSlot",
			CustomMinimumSize = new Vector2(Side, Side),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		});

		/// <summary>Marca <paramref name="corner"/> como o lugar do balão: ele fica no canto de cima à esquerda dele.</summary>
		public static T Dock<T>(T corner) where T : Control
		{
			corner.AddToGroup(DockGroup);
			return corner;
		}

		/// <summary>A tela que entrou: o balão vai para o lugar que ela marcou (sem lugar, o canto da janela).</summary>
		public void Follow(Control? screen)
		{
			_screen = screen;
			PlaceLater();
		}

		/// <summary>Depois do arranjo: quando o lugar muda, os pais dele já estão no lugar.</summary>
		private void PlaceLater()
		{
			if (_placing)
				return;
			_placing = true;
			Callable.From(Place).CallDeferred();
		}

		/// <summary>
		/// Acha o lugar na tela de agora (o de antes pode ter saído, numa tela que se refaz), passa a
		/// acompanhá-lo e põe o balão no canto dele.
		/// </summary>
		private void Place()
		{
			_placing = false;
			var dock = FindDock();
			if (dock != _dock)
			{
				if (_dock != null && IsInstanceValid(_dock))
				{
					_dock.ItemRectChanged -= PlaceLater;
					_dock.TreeExiting -= PlaceLater;
				}

				_dock = dock;
				if (dock != null)
				{
					dock.ItemRectChanged += PlaceLater;
					dock.TreeExiting += PlaceLater;
				}
			}

			var at = dock == null || !IsInsideTree()
				? new Vector2(Corner, Corner)
				: dock.GlobalPosition - (GetParentControl()?.GlobalPosition ?? Vector2.Zero);
			OffsetLeft = at.X;
			OffsetTop = at.Y;
			OffsetRight = at.X + Side;
			OffsetBottom = at.Y + Side;
		}

		private Control? FindDock()
		{
			if (_screen == null || !IsInstanceValid(_screen) || !_screen.IsInsideTree())
				return null;

			foreach (var node in _screen.GetTree().GetNodesInGroup(DockGroup))
			{
				if (node is Control dock && !dock.IsQueuedForDeletion() && _screen.IsAncestorOf(dock))
					return dock;
			}

			return null;
		}

		private void Refresh()
		{
			_box.BorderColor = _feed.Live ? Palette.Spirit : Palette.GoldDark;
			if (_feed.Lines.Count == 0)
				HideLast();
		}

		/// <summary>A faixa da última linha, filha do balão: anda com ele para onde ele for.</summary>
		private void BuildLast()
		{
			var box = GameTheme.Box(new Color(Palette.Inset, 0.94f), Palette.GoldDark, 1, (int)(LastHeight / 2), 0);
			box.ContentMarginLeft = box.ContentMarginRight = 12;
			_last.AddThemeStyleboxOverride("panel", box);
			_last.CustomMinimumSize = new Vector2(0, LastHeight);
			_last.Position = new Vector2(Side + LastGap, (Side - LastHeight) / 2);

			var row = Layout.Row(0).Named("Row");
			row.MouseFilter = MouseFilterEnum.Ignore;
			foreach (var label in new[] { _lastFrom, _lastText })
			{
				label.AddThemeFontSizeOverride("font_size", LastFontSize);
				row.AddChild(label);
			}

			_lastFrom.AddThemeColorOverride("font_color", Palette.Gold);
			_last.AddChild(row);
			AddChild(_last);
			Press.On(_last, Open, Open);

			_lastTimer.Timeout += HideLast;
			AddChild(_lastTimer);
		}

		/// <summary>Tocar na faixa é tocar no balão: abre o chat.</summary>
		private void Open() => EmitSignal(BaseButton.SignalName.Pressed);

		/// <summary>A linha que chegou vai para a faixa (as desta conta não: quem escreveu já viu).</summary>
		private void ShowLast(ChatLine line)
		{
			if (_feed.Reading || line.From == _feed.Me)
				return;

			var feat = line.Feat;
			_lastFrom.Visible = feat == null;
			_lastFrom.Text = $"{line.From}: ";
			_lastText.Text = feat != null ? Texts.Describe(feat, line.From) : line.Text ?? "";
			_lastText.AddThemeColorOverride("font_color", feat != null ? Palette.Gold : Palette.Text);

			// Sem as reticências, o texto pede a largura dele; com elas, cabe no que sobrar até o máximo.
			var room = LastMaxWidth - (_lastFrom.Visible ? _lastFrom.GetMinimumSize().X : 0);
			var width = _lastText.GetThemeFont("font").GetStringSize(_lastText.Text, HorizontalAlignment.Left, -1, LastFontSize).X;
			_lastText.CustomMinimumSize = new Vector2(Mathf.Min(Mathf.Ceil(width) + 1, room), 0);
			_last.Visible = true;
			_last.ResetSize();
			_lastTimer.Start();
		}

		private void HideLast()
		{
			_lastTimer.Stop();
			_last.Visible = false;
		}
	}
}
