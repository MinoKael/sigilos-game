using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura: o Glifo do conjunto na cor das estrelas, o espaço e a melhora. Sem runa,
	/// mostra o espaço vazio. Clicável.
	/// </summary>
	public partial class RuneTile : PanelContainer
	{
		public static readonly Vector2 TileSize = new(76, 84);

		private readonly StyleBoxFlat _box;

		public RuneTile(Rune? rune, int slot)
		{
			Rune = rune;
			Slot = slot;
			CustomMinimumSize = TileSize;
			MouseFilter = MouseFilterEnum.Stop;
			MouseDefaultCursorShape = CursorShape.PointingHand;

			var color = rune == null ? Palette.TextFaded : Palette.RuneGrade(rune.Grade);
			_box = GameTheme.Box(Palette.Inset, color, rune == null ? 1 : 2, 8, 4);
			AddThemeStyleboxOverride("panel", _box);

			var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			column.AddThemeConstantOverride("separation", 0);
			AddChild(column);

			var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			top.AddChild(Small($"{slot}", Palette.TextFaded));
			top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
			if (rune != null)
				top.AddChild(Small($"+{rune.Level}", Palette.Text));
			column.AddChild(top);

			column.AddChild(rune == null
				? new Label { Text = "vazio", ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsVertical = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center }
				: new Doodle(Art.Glyph(rune.Set), color, boil: false) { CustomMinimumSize = new Vector2(0, 44), SizeFlagsVertical = SizeFlags.ExpandFill });

			if (rune != null)
			{
				var stars = Small(Texts.Stars(rune.Grade), color);
				stars.HorizontalAlignment = HorizontalAlignment.Center;
				column.AddChild(stars);
				TooltipText = $"Runa de {Texts.Name(rune.Set)} ({rune.Slot})\n{Texts.Format(rune.Main, rune.MainValue)}";
			}
		}

		public event Action<RuneTile>? Pressed;

		public Rune? Rune { get; }
		public int Slot { get; }

		public void SetSelected(bool selected) => _box.SetBorderWidthAll(selected ? 4 : Rune == null ? 1 : 2);

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
	}
}
