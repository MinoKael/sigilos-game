using System.Linq;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Número que sobe e some sobre uma unidade: dano, cura, "Errou", nome de efeito. Pequeno e com subida
	/// curta; vários ao mesmo tempo no mesmo cartão fazem fila para baixo, um por linha, em vez de se
	/// empilhar no mesmo lugar.
	/// </summary>
	public static class FloatingText
	{
		/// <summary>O tamanho de efeito, erro e aviso; o dano é maior.</summary>
		public const int SmallSize = FontSize.Small;

		private const float Rise = 22;
		private const float Line = 12;

		/// <summary>Numera os textos: vários sobem juntos do mesmo pai, e cada um precisa de nome próprio.</summary>
		private static int _count;

		public static void Spawn(Control parent, string text, Color color, int size = SmallSize)
		{
			// Os que ainda estão subindo neste cartão empurram o novo uma linha para baixo.
			var busy = parent.GetChildren().Count(child => child.HasMeta("float") && !child.IsQueuedForDeletion());
			var label = new Label
			{
				Name = $"Float{++_count}",
				Text = text,
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ZIndex = 10,
				Modulate = new Color(1, 1, 1, 0),
			};
			label.SetMeta("float", true);
			label.AddThemeFontOverride("font", GameTheme.Serif);
			label.AddThemeFontSizeOverride("font_size", size);
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeColorOverride("font_outline_color", Palette.Background);
			label.AddThemeConstantOverride("outline_size", 4);
			parent.AddChild(label);

			var start = new Vector2(0, parent.Size.Y * 0.14f + busy % 4 * Line);
			label.Size = new Vector2(parent.Size.X, size + 6);
			label.Position = start;

			var tween = label.CreateTween();
			tween.TweenProperty(label, "modulate:a", 1f, 0.05);
			tween.Parallel().TweenProperty(label, "position", start + new Vector2(0, -Rise), 0.8);
			tween.TweenProperty(label, "modulate:a", 0f, 0.2);
			tween.TweenCallback(Callable.From(label.QueueFree));
		}
	}
}
