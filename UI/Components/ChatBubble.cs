using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O balão do Chat global, por cima de qualquer tela, no canto de cima à esquerda (no Santuário, no canto da
	/// constelação: <see cref="Dock"/>): só o símbolo do chat, redondo e meio transparente como o aviso da
	/// Batalha automática (<see cref="AutoBattleBadge"/>). Não conta as linhas que chegam. A borda diz a
	/// conexão: verde ao vivo, apagada sem ela. Tocar abre a janela do chat (<see cref="Pressed"/>). Só aparece
	/// jogando na conta.
	/// </summary>
	public partial class ChatBubble : Button
	{
		/// <summary>Pequeno e colado no canto: cabe antes da margem das telas.</summary>
		private const float Side = 36;

		private const float Corner = 2;

		/// <summary>O controle em cujo canto o balão fica; nulo: o canto da janela.</summary>
		private Control? _dock;

		/// <summary>Meio transparente: o balão fica por cima de qualquer tela.</summary>
		private const float Opacity = 0.8f;

		private const int IconSize = 22;

		private readonly ChatFeed _feed;
		private readonly Doodle _icon = Doodle.Icon(Art.Icon("chat"), IconSize, Palette.Gold);
		private readonly StyleBoxFlat _box;

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

			feed.Changed += Refresh;
			Refresh();
		}

		public override void _ExitTree() => _feed.Changed -= Refresh;

		/// <summary>
		/// Prende o balão no canto de cima à esquerda de <paramref name="corner"/>, acompanhando quando ele muda
		/// de lugar ou tamanho; nulo, ou quando ele sai da tela, o balão volta ao canto da janela.
		/// </summary>
		public void Dock(Control? corner)
		{
			if (_dock != null)
			{
				_dock.ItemRectChanged -= PlaceLater;
				_dock.TreeExiting -= Undock;
			}

			_dock = corner;
			if (corner != null)
			{
				corner.ItemRectChanged += PlaceLater;
				corner.TreeExiting += Undock;
			}

			PlaceLater();
		}

		private void Undock() => Dock(null);

		/// <summary>Depois do arranjo: quando o canto muda, os pais dele já estão no lugar.</summary>
		private void PlaceLater() => Callable.From(Place).CallDeferred();

		private void Place()
		{
			var at = _dock == null
				? new Vector2(Corner, Corner)
				: _dock.GlobalPosition - (GetParentControl()?.GlobalPosition ?? Vector2.Zero);
			OffsetLeft = at.X;
			OffsetTop = at.Y;
			OffsetRight = at.X + Side;
			OffsetBottom = at.Y + Side;
		}

		private void Refresh() => _box.BorderColor = _feed.Live ? Palette.Spirit : Palette.GoldDark;
	}
}
