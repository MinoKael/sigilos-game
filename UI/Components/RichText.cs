using System.Text.RegularExpressions;
using Godot;
using Sigilos.Core.Content;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Texto com termos dourados e Glifos (<see cref="Texts.Term(string, Glyph?)"/>): BBCode do Godot mais a
	/// marca [glifo=Nome], que vira o desenho do Glifo em dourado, do tamanho da letra.
	/// </summary>
	public static class RichText
	{
		private static readonly Regex GlyphMark = new(@"\[glifo=(\w+)\]", RegexOptions.Compiled);

		/// <summary>Um rótulo de texto rico que cresce com o conteúdo e quebra linha.</summary>
		public static RichTextLabel Label(string text, float width = 0, string? variation = null, int fontSize = 0)
		{
			var label = new RichTextLabel
			{
				BbcodeEnabled = true,
				FitContent = true,
				ScrollActive = false,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(width, 0),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Pass,
			};
			if (fontSize > 0)
				label.AddThemeFontSizeOverride("normal_font_size", fontSize);
			if (variation == GameTheme.Faded)
				label.AddThemeColorOverride("default_color", Palette.TextFaded);
			Set(label, text);
			return label;
		}

		/// <summary>Troca o conteúdo: o BBCode vai inteiro; cada [glifo=Nome] vira imagem.</summary>
		public static void Set(RichTextLabel label, string text)
		{
			label.Clear();
			var size = label.GetThemeFontSize("normal_font_size") + 2;
			var last = 0;
			foreach (Match match in GlyphMark.Matches(text))
			{
				label.AppendText(text[last..match.Index]);
				if (System.Enum.TryParse<Glyph>(match.Groups[1].Value, out var glyph) && Art.GlyphInk(glyph) is { } texture)
					label.AddImage(texture, size, size, Palette.Gold);
				last = match.Index + match.Length;
			}

			label.AppendText(text[last..]);
		}
	}
}
