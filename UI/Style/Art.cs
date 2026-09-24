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

		public static Texture2D? Creature(string image) => Load($"res://Assets/Creatures/{image}.svg");

		public static Texture2D? Glyph(Glyph glyph) => Load($"res://Assets/Glyphs/{glyph.ToString().ToLowerInvariant()}.svg");

		public static Texture2D? Element(Element element) => Load($"res://Assets/Elements/{element.ToString().ToLowerInvariant()}.svg");

		/// <summary>scroll, essence, fragments, grimoire, summon, campaign.</summary>
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
