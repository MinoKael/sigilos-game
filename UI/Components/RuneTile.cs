using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura, quadrada de cantos redondos: o espaço no canto de cima à esquerda, as
	/// estrelas ao lado dele, o Glifo do conjunto grande no meio (na fonte das runas, na cor da
	/// raridade) e a melhora no canto de baixo à direita. O nome e os atributos vêm na dica. Sem runa,
	/// só o número do espaço, apagado.
	///
	/// Sob o mouse a moldura acende; escolhida, fica azul arcano; marcada para desfazer, ganha o ✓ verde.
	/// Na lista, a runa equipada mostra no canto de baixo à esquerda o medalhão de quem a usa (apagado se
	/// ele está no Baú).
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
		private bool _selected;
		private bool _marked;

		public RuneTile(Rune? rune, int slot, float scale = 1)
		{
			Rune = rune;
			Slot = slot;
			_scale = scale;
			_stars = rune?.Grade ?? 0;
			CustomMinimumSize = TileSize * scale;
			MouseFilter = MouseFilterEnum.Stop;
			MouseDefaultCursorShape = CursorShape.PointingHand;

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
				TooltipText = T("rune.empty_slot", slot);
			}
			else
			{
				// O Glifo ocupa o miolo, um pouco abaixo do centro para deixar a fileira de cima livre.
				var glyph = new RuneGlyph(RuneSets.For(rune.Set).Glyph, (int)(40 * scale), _color, outline: true) { Name = "Glyph" };
				glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				glyph.OffsetTop = 6 * scale;
				glyph.OffsetRight = -6 * scale;
				_layer.AddChild(glyph);
				Corner(Small(slot.ToString(), Palette.Text, 12).Named("Slot"), LayoutPreset.TopLeft);
				Corner(Small($"+{rune.Level}", Palette.Text, 12).Named("Level"), LayoutPreset.BottomRight);
				TooltipText = T("rune.tip", Texts.Title(rune), Texts.Name(rune.Rarity), Texts.Stars(rune.Grade), rune.Level, Texts.Format(rune.Main, rune.MainValue));
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
		public void SetOwner(Texture2D? creature, Color ink, bool stored, string name)
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
			TooltipText += "\n" + T(stored ? "rune.owner_vault" : "rune.owner", name);
		}

		/// <summary>Marca para desfazer em massa.</summary>
		public void SetMarked(bool marked)
		{
			_marked = marked;
			_check.Visible = marked;
			Restyle();
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Pressed?.Invoke(this);
		}

		/// <summary>As estrelas da runa, em fileira no alto, ao lado do número do espaço.</summary>
		public override void _Draw()
		{
			if (_stars == 0)
				return;

			var radius = 3.4f * _scale;
			var step = radius * 2.05f;
			var y = 8 * _scale;
			var right = Size.X - 6 * _scale;
			for (var i = 0; i < _stars; i++)
			{
				var center = new Vector2(right - i * step - radius, y);
				DrawColoredPolygon(Star(center, radius + 1.2f), new Color(0, 0, 0, 0.85f));
				DrawColoredPolygon(Star(center, radius), Palette.Gold);
			}
		}

		private static Vector2[] Star(Vector2 center, float radius)
		{
			var points = new Vector2[10];
			for (var i = 0; i < 10; i++)
			{
				var angle = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
				var r = i % 2 == 0 ? radius : radius * 0.45f;
				points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
			}

			return points;
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
			label.OffsetTop += bottom ? -1 * _scale : 0;
			label.OffsetBottom += bottom ? -1 * _scale : 0;
			_layer.AddChild(label);
		}

		private Label Small(string text, Color color, int size)
		{
			var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", (int)(size * _scale));
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
			label.AddThemeConstantOverride("outline_size", 2);
			label.AddThemeConstantOverride("line_spacing", -4);
			return label;
		}
	}
}
