using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Um Glifo escrito na fonte das runas (Kehdrai): nítido em qualquer tamanho, na cor dada, centrado
	/// no espaço que tiver. Substitui o desenho do Glifo em tudo o que mostra runa.
	/// </summary>
	public partial class RuneGlyph : Label
	{
		public RuneGlyph(Glyph glyph, int size, Color color, bool outline = false)
		{
			Glyph = glyph;
			Text = Texts.Rune(glyph);
			HorizontalAlignment = HorizontalAlignment.Center;
			VerticalAlignment = VerticalAlignment.Center;
			MouseFilter = MouseFilterEnum.Ignore;
			CustomMinimumSize = new Vector2(size, size);
			AddThemeFontOverride("font", GameTheme.Runes);
			AddThemeFontSizeOverride("font_size", size);
			AddThemeColorOverride("font_color", color);
			AddThemeConstantOverride("line_spacing", -size / 2);
			if (outline)
			{
				AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
				AddThemeConstantOverride("outline_size", 4);
			}
		}

		public Glyph Glyph { get; }
	}
}
