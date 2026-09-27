using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Um portal alto do Mapa: moldura de couro, o símbolo grande do conteúdo e, embaixo, o que a tela
	/// puser em <see cref="Footer"/> (a fase atual, o progresso). Fechado, fica apagado com o cadeado
	/// por cima. Sob o mouse, a moldura acende em azul arcano.
	/// </summary>
	public partial class DoorCard : Button
	{
		private readonly bool _locked;
		private readonly StyleBoxTexture _frame = Ornament.Panel(Palette.Panel, Palette.GoldDark);
		private readonly StyleBoxTexture _frameHover = Ornament.Panel(Palette.PanelLight, Palette.Gold);

		public DoorCard(Texture2D? icon, Color ink, string tooltip, bool locked, Vector2 size)
		{
			_locked = locked;
			Flat = true;
			FocusMode = FocusModeEnum.None;
			TooltipText = tooltip;
			Disabled = locked;
			CustomMinimumSize = size;
			MouseDefaultCursorShape = locked ? CursorShape.Arrow : CursorShape.PointingHand;
			Juice.Attach(this, 1.03f, 0.97f);

			var column = new VBoxContainer { Name = "Column", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			column.OffsetLeft = column.OffsetTop = 18;
			column.OffsetRight = column.OffsetBottom = -18;
			column.AddThemeConstantOverride("separation", 14);
			AddChild(column);

			var art = new Control { Name = "Art", CustomMinimumSize = new Vector2(0, size.X * 0.62f), MouseFilter = MouseFilterEnum.Ignore };
			art.AddChild(Doodle.Masked(icon, locked ? Palette.TextFaded.Darkened(0.3f) : ink, MaskShape.Rounded, 10));
			column.AddChild(art);
			Footer = new VBoxContainer { Name = "Footer", MouseFilter = MouseFilterEnum.Ignore };
			Footer.AddThemeConstantOverride("separation", 8);
			column.AddChild(Footer);

			if (locked)
			{
				var padlock = Doodle.Icon(Art.Icon("lock"), 56, Palette.Gold);
				padlock.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
				padlock.OffsetLeft = padlock.OffsetTop = -28;
				padlock.OffsetRight = padlock.OffsetBottom = 28;
				AddChild(padlock);
			}
		}

		/// <summary>A parte de baixo do portal: a tela põe número, barra e cápsulas aqui.</summary>
		public VBoxContainer Footer { get; }

		public override void _Draw()
		{
			var hover = !_locked && IsHovered();
			var rect = new Rect2(Vector2.Zero, Size);
			if (hover)
			{
				for (var i = 1; i <= 4; i++)
					DrawRect(rect.Grow(i * 1.5f), new Color(Palette.Arcane, 0.14f * (1 - i / 5f)), false, 2);
			}

			DrawStyleBox(hover ? _frameHover : _frame, rect);
			if (_locked)
				DrawRect(rect.Grow(-4), new Color(0, 0, 0, 0.35f));
		}
	}
}
