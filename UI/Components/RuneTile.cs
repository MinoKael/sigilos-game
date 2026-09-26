using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura: moldura e Glifo do conjunto na cor da raridade, espaço e melhora em cima,
	/// o conjunto e as estrelas embaixo. Sem runa, mostra o espaço vazio. Clicável; na seleção em massa,
	/// a runa marcada ganha um ✓. Na lista, a runa equipada mostra no alto o desenho de quem a usa (e o
	/// Baú, se ele estiver lá).
	/// </summary>
	public partial class RuneTile : PanelContainer
	{
		public static readonly Vector2 TileSize = new(86, 104);

		private readonly StyleBoxFlat _box;
		private readonly HBoxContainer _top = new() { MouseFilter = MouseFilterEnum.Ignore };
		private readonly Label _mark = new() { Text = "✓", Visible = false, MouseFilter = MouseFilterEnum.Ignore };

		public RuneTile(Rune? rune, int slot)
		{
			Rune = rune;
			Slot = slot;
			CustomMinimumSize = TileSize;
			MouseFilter = MouseFilterEnum.Stop;
			MouseDefaultCursorShape = CursorShape.PointingHand;

			var color = rune == null ? Palette.TextFaded : Palette.Of(rune.Rarity);
			_box = GameTheme.Box(Palette.Inset, color, rune == null ? 1 : 2, 8, 4);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 0);
			AddChild(column);

			_top.AddThemeConstantOverride("separation", 2);
			_top.AddChild(Small($"{slot}", Palette.TextFaded));
			_top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			if (rune != null)
				_top.AddChild(Small($"+{rune.Level}", Palette.Text));
			column.AddChild(_top);

			if (rune == null)
			{
				column.AddChild(new Label { Text = T("runa.vazio"), ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsVertical = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
				return;
			}

			var set = RuneSets.For(rune.Set);
			column.AddChild(new Doodle(Art.Glyph(set.Glyph), color, boil: false) { CustomMinimumSize = new Vector2(0, 42), SizeFlagsVertical = SizeFlags.ExpandFill });
			column.AddChild(Centered(Small(Texts.Name(rune.Set), color)));
			column.AddChild(Centered(Small(Texts.Stars(rune.Grade), Palette.Gold)));

			_mark.AddThemeColorOverride("font_color", Palette.Positive);
			_mark.AddThemeFontSizeOverride("font_size", 26);
			_mark.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
			AddChild(_mark);

			TooltipText = T("runa.dica", Texts.Title(rune), Texts.Name(rune.Rarity), Texts.Stars(rune.Grade), rune.Level, Texts.Format(rune.Main, rune.MainValue));
		}

		public event Action<RuneTile>? Pressed;

		public Rune? Rune { get; }
		public int Slot { get; }

		public void SetSelected(bool selected) => _box.SetBorderWidthAll(selected ? 4 : Rune == null ? 1 : 2);

		/// <summary>Quem usa a runa: o desenho do monstro no alto e, se ele está no Baú, o baú ao lado.</summary>
		public void SetOwner(Texture2D? creature, Color ink, bool stored, string name)
		{
			var index = 1;
			_top.AddChild(Doodle.Icon(creature, 18, ink));
			_top.MoveChild(_top.GetChild(_top.GetChildCount() - 1), index++);
			if (stored)
			{
				_top.AddChild(Doodle.Icon(Art.Icon("chest"), 14, Palette.TextFaded));
				_top.MoveChild(_top.GetChild(_top.GetChildCount() - 1), index);
			}

			TooltipText += "\n" + T(stored ? "runa.dono_bau" : "runa.dono", name);
		}

		/// <summary>Marca para desfazer em massa.</summary>
		public void SetMarked(bool marked)
		{
			_mark.Visible = marked;
			_box.BgColor = marked ? Palette.Inset.Lerp(Palette.Positive, 0.15f) : Palette.Inset;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Pressed?.Invoke(this);
		}

		private static Label Small(string text, Color color)
		{
			var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeFontSizeOverride("font_size", 11);
			label.AddThemeColorOverride("font_color", color);
			return label;
		}

		private static Label Centered(Label label)
		{
			label.HorizontalAlignment = HorizontalAlignment.Center;
			return label;
		}
	}
}
