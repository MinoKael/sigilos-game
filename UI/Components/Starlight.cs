using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O desenho das constelações, num lugar só: o fio de luz entre dois pontos, com a faísca que corre
	/// por ele, e a estrela de quatro pontas que pisca. A constelação da Canalização
	/// (<see cref="Constellation"/>) e o mapa da Exploração Estelar (<see cref="StarChart"/>) desenham
	/// com estas peças, para os dois céus serem o mesmo.
	/// </summary>
	public static class Starlight
	{
		/// <summary>
		/// Um fio de luz de <paramref name="from"/> a <paramref name="to"/>: o halo largo, a linha fina e a
		/// faísca, que corre no tempo <paramref name="time"/> (cada fio no seu passo, por <paramref name="phase"/>).
		/// </summary>
		public static void Thread(CanvasItem canvas, Vector2 from, Vector2 to, float time, float phase, Color line, Color spark, float alpha = 1, bool sparkle = true)
		{
			canvas.DrawLine(from, to, new Color(Palette.Arcane, 0.07f * alpha), 7, true);
			canvas.DrawLine(from, to, new Color(line, 0.4f * alpha), 1.5f, true);
			if (!sparkle)
				return;

			var t = Mathf.PosMod(time * 0.22f + phase, 1f);
			var at = from.Lerp(to, t);
			canvas.DrawCircle(at, 5, new Color(spark, 0.12f * alpha));
			canvas.DrawCircle(at, 2.2f, new Color(spark, 0.85f * alpha * Mathf.Sin(t * Mathf.Pi)));
		}

		/// <summary>A estrela de quatro pontas, de raio <paramref name="size"/>.</summary>
		public static void Sparkle(CanvasItem canvas, Vector2 at, float size, Color color)
		{
			var waist = size * 0.24f;
			canvas.DrawColoredPolygon(new[]
			{
				at + new Vector2(0, -size), at + new Vector2(waist, -waist),
				at + new Vector2(size, 0), at + new Vector2(waist, waist),
				at + new Vector2(0, size), at + new Vector2(-waist, waist),
				at + new Vector2(-size, 0), at + new Vector2(-waist, -waist),
			}, color);
		}

		/// <summary>A estrela que pisca no seu tempo (<paramref name="phase"/>): um brilho largo e o miolo aceso.</summary>
		public static void Twinkle(CanvasItem canvas, Vector2 at, float radius, float time, float phase, Color color)
		{
			var twinkle = 0.55f + 0.45f * Mathf.Sin(time * 1.7f + phase);
			Sparkle(canvas, at, radius * 0.75f, new Color(color, 0.12f * twinkle));
			Sparkle(canvas, at, radius * 0.38f, new Color(color.Lightened(0.2f), 0.45f + 0.5f * twinkle));
		}
	}
}
