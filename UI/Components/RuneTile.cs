using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura, quadrada de cantos redondos: as estrelas no alto, o Glifo do conjunto grande
	/// no meio (na fonte das runas, na cor da raridade), o espaço no canto de baixo à esquerda e a melhora
	/// no de baixo à direita. Sem runa, só o número do espaço, apagado.
	///
	/// Toque curto é <see cref="Pressed"/>; toque longo abre a ficha da runa (<see cref="RuneCard"/>) numa
	/// janela colada nela. Escolhida, fica azul arcano; marcada para vender, ganha o ✓ verde. Na lista, a
	/// runa equipada mostra no canto de baixo à esquerda o medalhão de quem a usa (apagado se ele está no
	/// Baú); a bloqueada, um cadeado no meio da borda de baixo. O toque passa para cima, então arrastar
	/// rola a lista.
	/// </summary>
	public partial class RuneTile : PanelContainer
	{
		/// <summary>O lado da pedra, em px, na escala 1.</summary>
		public const float Side = 60;

		public static readonly Vector2 TileSize = new(Side, Side);

		private readonly StyleBoxFlat _box;
		private readonly Color _color;
		private readonly Control _layer = new() { Name = "Layer", MouseFilter = MouseFilterEnum.Ignore };
		private readonly Doodle _check = new(Art.Icon("confirm"), Palette.Spirit, boil: false) { Name = "Check", Visible = false };
		private readonly float _scale;
		private readonly int _stars;
		private readonly Press _press = new();
		private bool _selected;
		private bool _marked;

		public RuneTile(Rune? rune, int slot, float scale = 1)
		{
			Rune = rune;
			Slot = slot;
			_scale = scale;
			_stars = rune?.Grade ?? 0;
			CustomMinimumSize = TileSize * scale;
			MouseFilter = MouseFilterEnum.Pass;
			MouseDefaultCursorShape = CursorShape.PointingHand;
			_press.Tapped += () => Pressed?.Invoke(this);
			_press.Held += () =>
			{
				if (Rune != null)
					RuneDialog.Show(this, Rune);
			};

			_color = rune == null ? Palette.GoldDark : Palette.Of(rune.Rarity);
			_box = GameTheme.Box(Palette.Inset, _color, rune == null ? 1 : 2, (int)(9 * scale), 0);
			AddThemeStyleboxOverride("panel", _box);
			AddChild(_layer);

			if (rune == null)
			{
				var number = Small(slot.ToString(), new Color(Palette.GoldDark, 0.8f), 22).Named("Slot");
				number.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				number.HorizontalAlignment = HorizontalAlignment.Center;
				number.VerticalAlignment = VerticalAlignment.Center;
				_layer.AddChild(number);
			}
			else
			{
				// O Glifo ocupa o miolo, um pouco abaixo do centro para deixar a fileira de cima livre.
				var glyph = new RuneGlyph(RuneSets.For(rune.Set).Glyph, (int)(40 * scale), _color, outline: true) { Name = "Glyph" };
				glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				glyph.OffsetTop = 6 * scale;
				_layer.AddChild(glyph);
				Corner(Small(slot.ToString(), Palette.Text, 12).Named("Slot"), LayoutPreset.BottomLeft);
				Corner(Small($"{(rune.Level > 0 ? $"+{rune.Level}" : "")}", Palette.Text, 12, HorizontalAlignment.Right).Named("Level"), LayoutPreset.BottomRight);
				if (rune.Locked)
					LockBadge();
			}

			_check.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_check.OffsetLeft = _check.OffsetTop = 10 * scale;
			_check.OffsetRight = _check.OffsetBottom = -10 * scale;
			_layer.AddChild(_check);

			MouseEntered += Restyle;
			MouseExited += Restyle;
		}

		public event Action<RuneTile>? Pressed;

		public Rune? Rune { get; }
		public int Slot { get; }

		public void SetSelected(bool selected)
		{
			_selected = selected;
			Restyle();
		}

		/// <summary>Quem usa a runa: o medalhão do monstro no canto de baixo à esquerda (apagado se ele está no Baú).</summary>
		public void SetOwner(Texture2D? creature, Color ink, bool stored)
		{
			var size = 18 * _scale;
			var holder = new Control { Name = "Owner", MouseFilter = MouseFilterEnum.Ignore };
			holder.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
			holder.OffsetLeft = 2 * _scale;
			holder.OffsetRight = holder.OffsetLeft + size;
			holder.OffsetBottom = -2 * _scale;
			holder.OffsetTop = holder.OffsetBottom - size;
			holder.AddChild(Doodle.Masked(creature, stored ? ink.Darkened(0.5f) : ink, MaskShape.Circle, boil: false));
			_layer.AddChild(holder);
		}

		/// <summary>Uma faixa escrita por cima da runa ("Vendida"), e a runa apagada.</summary>
		public void SetStamp(string text)
		{
			Modulate = new Color(1, 1, 1, 0.55f);
			var plate = new PanelContainer { Name = "Stamp", MouseFilter = MouseFilterEnum.Ignore };
			var box = GameTheme.Box(new Color(Palette.Inset, 0.95f), Palette.Negative, 1, 5, 0);
			box.ContentMarginLeft = box.ContentMarginRight = 4;
			plate.AddThemeStyleboxOverride("panel", box);
			var label = new Label { Name = "Text", Text = text, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", (int)(12 * _scale));
			label.AddThemeColorOverride("font_color", Palette.Negative.Lightened(0.3f));
			plate.AddChild(label);
			plate.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
			plate.GrowHorizontal = GrowDirection.Both;
			plate.GrowVertical = GrowDirection.Both;
			_layer.AddChild(plate);
		}

		/// <summary>O cadeado da runa bloqueada: uma plaquinha no meio da borda de baixo, entre o espaço e a melhora.</summary>
		private void LockBadge()
		{
			var size = 16 * _scale;
			var badge = new PanelContainer { Name = "Lock", MouseFilter = MouseFilterEnum.Ignore };
			badge.AddThemeStyleboxOverride("panel", GameTheme.Box(new Color(Palette.Inset, 0.92f), Palette.GoldDark, 1, (int)size, (int)(2 * _scale)));
			badge.AddChild(Doodle.Icon(Art.Icon("lock"), (int)(size - 4 * _scale), Palette.Gold).Named("Icon"));
			badge.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
			badge.GrowHorizontal = GrowDirection.Both;
			badge.GrowVertical = GrowDirection.Begin;
			badge.OffsetTop = badge.OffsetBottom = -2 * _scale;
			_layer.AddChild(badge);
		}

		/// <summary>Marca para desfazer em massa.</summary>
		public void SetMarked(bool marked)
		{
			_marked = marked;
			_check.Visible = marked;
			Restyle();
		}

		public override void _GuiInput(InputEvent @event) => _press.Feed(this, @event);

		/// <summary>
		/// As estrelas da runa, em fileira no alto, ao lado do número do espaço: o desenho da estrela (o PNG
		/// renderizado, em branco para tingir) em ouro, sobre a mesma estrela em preto, um pouco maior.
		/// </summary>
		public override void _Draw()
		{
			if (_stars == 0 || Art.IconInk("star") is not { } star)
				return;

			var size = 9 * _scale;
			var step = size * 0.92f;
			var top = 3.5f * _scale;
			var left = 5 * _scale;
			for (var i = 0; i < _stars; i++)
			{
				var rect = new Rect2(left + i * step, top, size, size);
				DrawTextureRect(star, rect.Grow(1.2f), false, new Color(0, 0, 0, 0.85f));
				DrawTextureRect(star, rect, false, Palette.Gold);
			}
		}

		private void Restyle()
		{
			var hover = IsInsideTree() && GetGlobalRect().HasPoint(GetGlobalMousePosition());
			var width = Rune == null ? 1 : 2;
			_box.BorderColor = _selected ? Palette.Arcane : hover ? _color.Lightened(0.3f) : _color;
			_box.SetBorderWidthAll(_selected ? width + 1 : width);
			_box.BgColor = _marked ? Palette.Inset.Lerp(Palette.Spirit, 0.18f) : hover ? Palette.Inset.Lightened(0.06f) : Palette.Inset;
			_box.ShadowColor = _selected ? new Color(Palette.Arcane, 0.4f) : new Color(_color, hover ? 0.3f : 0);
			_box.ShadowSize = _selected || hover ? 5 : 0;
		}

		/// <summary>Um número num canto, por cima do Glifo.</summary>
		private void Corner(Label label, LayoutPreset corner)
		{
			label.SetAnchorsAndOffsetsPreset(corner);
			var right = corner is LayoutPreset.TopRight or LayoutPreset.BottomRight;
			var bottom = corner is LayoutPreset.BottomLeft or LayoutPreset.BottomRight;
			label.GrowHorizontal = right ? GrowDirection.Begin : GrowDirection.End;
			label.GrowVertical = bottom ? GrowDirection.Begin : GrowDirection.End;
			var pad = 4 * _scale;
			label.OffsetLeft += right ? -pad : pad;
			label.OffsetRight += right ? -pad : pad;
			label.OffsetTop += bottom ? -pad : pad;
			label.OffsetBottom += bottom ? pad : -pad;
			_layer.AddChild(label);
		}

		private Label Small(string text, Color color, int size, HorizontalAlignment? horizontalAlignment = null, VerticalAlignment? verticalAlignment = null)
		{
			var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", (int)(size * _scale));
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
			label.AddThemeConstantOverride("outline_size", 2);
			label.AddThemeConstantOverride("line_spacing", -8);
			label.HorizontalAlignment = horizontalAlignment ?? HorizontalAlignment.Center;
			label.VerticalAlignment = verticalAlignment ?? VerticalAlignment.Center;
			return label;
		}
	}
}
