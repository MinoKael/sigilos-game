using System.Collections.Generic;
using Godot;
using Sigilos.Core.Content;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// Onde cada imagem mora em Assets/. Arquivo faltando devolve nulo e a tela mostra só a cor —
	/// o jogo roda mesmo antes de <c>Tools/art/fetch_commons_assets.py</c> baixar tudo.
	/// </summary>
	public static class Art
	{
		private static readonly Dictionary<string, Texture2D?> Cache = new();
		private static readonly Dictionary<Glyph, Texture2D?> Inks = new();

		public static Texture2D? Creature(string image) => Load($"res://Assets/Creatures/{image}.svg");

		/// <summary>O símbolo de runa do Glifo, em Assets/Glyphs com o nome da runa.</summary>
		public static Texture2D? Glyph(Glyph glyph) => Load($"res://Assets/Glyphs/{glyph.ToString().ToLowerInvariant()}.svg");

		/// <summary>
		/// O Glifo em branco, para tingir no texto rico: os SVG são pretos, e a cor da imagem no texto só
		/// escurece. Gerado uma vez por Glifo.
		/// </summary>
		public static Texture2D? GlyphInk(Glyph glyph)
		{
			if (Inks.TryGetValue(glyph, out var ink))
				return ink;

			var image = Glyph(glyph)?.GetImage();
			if (image != null)
			{
				image.Decompress();
				image.Convert(Image.Format.Rgba8);
				for (var y = 0; y < image.GetHeight(); y++)
				{
					for (var x = 0; x < image.GetWidth(); x++)
						image.SetPixel(x, y, new Color(1, 1, 1, image.GetPixel(x, y).A));
				}
			}

			ink = image == null ? null : ImageTexture.CreateFromImage(image);
			Inks[glyph] = ink;
			return ink;
		}

		public static Texture2D? Element(Element element) => Load($"res://Assets/Elements/{element.ToString().ToLowerInvariant()}.svg");

		/// <summary>Ícones de Assets/Icons: scroll, essence, dust, fragments, summon, campaign, storage, chest, team, dungeon, grimoire, compendium, grindstone, gem, rune, search.</summary>
		public static Texture2D? Icon(string name) => Load($"res://Assets/Icons/{name}.svg");

		private static Texture2D? Load(string path)
		{
			if (!Cache.TryGetValue(path, out var texture))
			{
				texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
				Cache[path] = texture;
			}

			return texture;
		}
	}
}
