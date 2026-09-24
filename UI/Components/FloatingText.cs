using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>Número que sobe e some sobre uma unidade: dano, cura, "Errou", nome de efeito.</summary>
	public static class FloatingText
	{
		public static void Spawn(Control parent, string text, Color color, float delay = 0, int size = 26)
		{
			var label = new Label
			{
				Text = text,
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ZIndex = 10,
				Modulate = new Color(1, 1, 1, 0),
			};
			label.AddThemeFontOverride("font", GameTheme.Serif);
			label.AddThemeFontSizeOverride("font_size", size);
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeColorOverride("font_outline_color", Palette.Ink);
			label.AddThemeConstantOverride("outline_size", 6);
			parent.AddChild(label);

			var start = new Vector2(0, parent.Size.Y * 0.25f);
			label.Size = new Vector2(parent.Size.X, 32);
			label.Position = start;

			var tween = label.CreateTween();
			tween.TweenInterval(delay);
			tween.TweenProperty(label, "modulate:a", 1f, 0.05);
			tween.Parallel().TweenProperty(label, "position", start + new Vector2(0, -48), 0.9);
			tween.TweenProperty(label, "modulate:a", 0f, 0.25);
			tween.TweenCallback(Callable.From(label.QueueFree));
		}
	}
}
