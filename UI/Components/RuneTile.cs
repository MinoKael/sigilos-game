using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura, quadrada: o Glifo do conjunto na cor da raridade, recortado na pedra; o
	/// espaço no canto de cima, as estrelas no outro canto e a melhora embaixo, em números pequenos com
	/// contorno. O nome e os atributos vêm na dica. Sem runa, só o número do espaço, apagado.
	///
	/// Sob o mouse a moldura acende; escolhida, fica azul arcano; marcada para desfazer, ganha o ✓ verde.
	/// Na lista, a runa equipada mostra no canto o desenho de quem a usa (apagado se ele está no Baú).
	/// </summary>
	public partial class RuneTile : PanelContainer
	{
		/// <summary>O lado da pedra, em px, na escala 1.</summary>
		public const float Side = 48;

		public static readonly Vector2 TileSize = new(Side, Side);

		private readonly StyleBoxFlat _box;
		private readonly Color _color;
		private readonly Control _layer = new() { MouseFilter = MouseFilterEnum.Ignore };
		private readonly Doodle _check = new(Art.Icon("confirm"), Palette.Spirit, boil: false) { Visible = false };
		private readonly float _scale;
		private bool _selected;
		private bool _marked;

		public RuneTile(Rune? rune, int slot, float scale = 1)
		{
			Rune = rune;
			Slot = slot;
			_scale = scale;
			CustomMinimumSize = TileSize * scale;
			MouseFilter = MouseFilterEnum.Stop;
			MouseDefaultCursorShape = CursorShape.PointingHand;

			_color = rune == null ? Palette.GoldDark : Palette.Of(rune.Rarity);
			_box = GameTheme.Box(Palette.Inset, _color, rune == null ? 1 : 2, (int)(7 * scale), 0);
			AddThemeStyleboxOverride("panel", _box);
			AddChild(_layer);

			if (rune == null)
			{
				var number = Small(slot.ToString(), new Color(Palette.GoldDark, 0.8f), 18);
				number.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				number.HorizontalAlignment = HorizontalAlignment.Center;
				number.VerticalAlignment = VerticalAlignment.Center;
				_layer.AddChild(number);
				TooltipText = T("rune.empty_slot", slot);
			}
			else
			{
				var set = RuneSets.For(rune.Set);
				_layer.AddChild(Doodle.Masked(Art.Glyph(set.Glyph), _color, MaskShape.Rounded, 5 * scale, 7 * scale, boil: false));
				Corner(Small(slot.ToString(), Palette.TextFaded, 9), LayoutPreset.TopLeft);
				Corner(Small($"{rune.Grade}★", Palette.Gold, 9), LayoutPreset.TopRight);
				Corner(Small($"+{rune.Level}", Palette.Text, 10), LayoutPreset.BottomRight);
				TooltipText = T("rune.tip", Texts.Title(rune), Texts.Name(rune.Rarity), Texts.Stars(rune.Grade), rune.Level, Texts.Format(rune.Main, rune.MainValue));
			}

			_check.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_check.OffsetLeft = _check.OffsetTop = 8 * scale;
			_check.OffsetRight = _check.OffsetBottom = -8 * scale;
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

		/// <summary>Quem usa a runa: o desenho do monstro, num medalhão no canto de baixo (apagado se ele está no Baú).</summary>
		public void SetOwner(Texture2D? creature, Color ink, bool stored, string name)
		{
			var size = 16 * _scale;
			var medal = Doodle.Masked(creature, stored ? ink.Darkened(0.5f) : ink, MaskShape.Circle, boil: false);
			var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
			holder.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
			holder.OffsetTop = -size - 1;
			holder.OffsetRight = size + 1;
			holder.OffsetLeft = 1;
			holder.OffsetBottom = -1;
			holder.AddChild(medal);
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
			label.GrowHorizontal = corner is LayoutPreset.TopRight or LayoutPreset.BottomRight ? GrowDirection.Begin : GrowDirection.End;
			label.GrowVertical = corner is LayoutPreset.BottomLeft or LayoutPreset.BottomRight ? GrowDirection.Begin : GrowDirection.End;
			var pad = 2 * _scale;
			label.OffsetLeft += corner is LayoutPreset.TopLeft or LayoutPreset.BottomLeft ? pad : -pad;
			label.OffsetRight += corner is LayoutPreset.TopLeft or LayoutPreset.BottomLeft ? pad : -pad;
			_layer.AddChild(label);
		}

		private Label Small(string text, Color color, int size)
		{
			var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", (int)(size * _scale));
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
			label.AddThemeConstantOverride("outline_size", 4);
			label.AddThemeConstantOverride("line_spacing", -4);
			return label;
		}
	}
}
