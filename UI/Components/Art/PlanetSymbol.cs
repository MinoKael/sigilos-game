using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O símbolo de um Sigilo (ou de um atributo): o planeta (desenho de Assets/Planets), na cor dada e centrado
	/// no espaço que tiver. Sem planeta (nulo), só reserva o espaço.
	/// </summary>
	public partial class PlanetSymbol : Control
	{
		public PlanetSymbol(Planet? planet, int size, Color color, bool outline = false)
		{
			Planet = planet;
			MouseFilter = MouseFilterEnum.Ignore;
			CustomMinimumSize = new Vector2(size, size);
			CustomMaximumSize = new Vector2(size, size);

            if (planet is not { } value || Art.PlanetImage(value) is not { } texture)
				return;

			if (outline)
				AddChild(Image(texture, size, new Color(0, 0, 0, 0.8f), 1.2f).Named("Outline"));
			AddChild(Image(texture, size, color, 1.1f).Named("Planet"));
		}

		public Planet? Planet { get; }

		/// <summary>O desenho tingido pelo shader do Doodle (os SVG são pretos, a cor vem da tinta).</summary>
		private static Control Image(Texture2D texture, int size, Color ink, float scale)
		{
			var rect = (Control)Doodle.Icon(texture, size, ink);
			rect.MouseFilter = MouseFilterEnum.Ignore;
			rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			if (scale != 1f)
			{
				rect.PivotOffsetRatio = new Vector2(0.5f, 0.5f);
				rect.Scale = new Vector2(scale, scale);
			}

			return rect;
		}
	}
}
