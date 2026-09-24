using System;
using Godot;
using Sigilos.Core.Runes;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Uma runa em miniatura: moldura e Glifo na cor da raridade,
	/// espaço e melhora em cima, o conjunto e as estrelas embaixo. Sem runa, mostra o espaço vazio.
	/// Clicável.
	/// </summary>
	public partial class RuneTile : PanelContainer
	{
		public static readonly Vector2 TileSize = new(86, 104);

		private readonly StyleBoxFlat _box;

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

			var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			top.AddChild(Small($"{slot}", Palette.TextFaded));
			top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
			if (rune != null)
				top.AddChild(Small($"+{rune.Level}", Palette.Text));
			column.AddChild(top);

			if (rune == null)
			{
				column.AddChild(new Label { Text = "vazio", ThemeTypeVariation = GameTheme.Faded, HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsVertical = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
				return;
			}

			var set = RuneSets.For(rune.Set);
			column.AddChild(new Doodle(Art.Glyph(set.Glyph), color, boil: false) { CustomMinimumSize = new Vector2(0, 42), SizeFlagsVertical = SizeFlags.ExpandFill });
			column.AddChild(Centered(Small(Texts.Name(rune.Set), color)));
			column.AddChild(Centered(Small(Texts.Stars(rune.Grade), Palette.Gold)));

			TooltipText = $"{Texts.Title(rune)} · {Texts.Name(rune.Rarity)} {Texts.Stars(rune.Grade)} +{rune.Level}\n{Texts.Format(rune.Main, rune.MainValue)}";
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

		private static Label Centered(Label label)
		{
			label.HorizontalAlignment = HorizontalAlignment.Center;
			return label;
		}
	}
}
