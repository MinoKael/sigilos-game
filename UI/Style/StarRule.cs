using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// O divisor ornamental do grimório, o estilo de todo <c>HSeparator</c>: um traço fino que nasce
	/// apagado nas pontas e acende para o meio, onde fica uma estrela de quatro pontas entre duas menores,
	/// como as marcas que separam as anotações de um livro de estudo.
	/// </summary>
	public partial class StarRule : StyleBox
	{
		private readonly Color _line;
		private readonly Color _star;

		public StarRule() : this(Palette.GoldDark, Palette.Gold)
		{
		}

		public StarRule(Color line, Color star)
		{
			_line = line;
			_star = star;
			ContentMarginTop = ContentMarginBottom = 6;
		}

		public override void _Draw(Rid canvas, Rect2 rect)
		{
			const float inset = 6;
			const float gap = 22;
			var y = rect.GetCenter().Y;
			var middle = rect.GetCenter().X;
			var left = rect.Position.X + inset;
			var right = rect.End.X - inset;
			var clear = new Color(_line, 0);

			if (middle - gap > left)
			{
				RenderingServer.CanvasItemAddPolyline(canvas, new[] { new Vector2(left, y), new Vector2(middle - gap, y) }, new[] { clear, _line }, 1, true);
				RenderingServer.CanvasItemAddPolyline(canvas, new[] { new Vector2(middle + gap, y), new Vector2(right, y) }, new[] { _line, clear }, 1, true);
			}

			Sparkle(canvas, new Vector2(middle, y), 6, _star);
			Sparkle(canvas, new Vector2(middle - 13, y), 2.5f, _line);
			Sparkle(canvas, new Vector2(middle + 13, y), 2.5f, _line);
		}

		/// <summary>A estrela de quatro pontas (a mesma de <see cref="Components.Starlight.Sparkle"/>).</summary>
		private static void Sparkle(Rid canvas, Vector2 at, float size, Color color)
		{
			var waist = size * 0.24f;
			RenderingServer.CanvasItemAddPolygon(canvas, new[]
			{
				at + new Vector2(0, -size), at + new Vector2(waist, -waist),
				at + new Vector2(size, 0), at + new Vector2(waist, waist),
				at + new Vector2(0, size), at + new Vector2(-waist, waist),
				at + new Vector2(-size, 0), at + new Vector2(-waist, -waist),
			}, new[] { color });
		}
	}
}
