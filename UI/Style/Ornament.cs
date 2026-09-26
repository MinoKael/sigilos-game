using System;
using System.Collections.Generic;
using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As texturas da moldura, geradas em código uma vez por cor — nenhuma imagem pronta no projeto.
	///
	/// <see cref="Panel"/> é couro com grão, borda de ouro, um filete interno e um losango em cada canto;
	/// vira um <see cref="StyleBoxTexture"/> de 9 partes: os cantos ficam fixos e o miolo repete (Tile),
	/// então o grão não estica em painel grande. <see cref="Gem"/> é a pedra da barra de rolagem e
	/// <see cref="Sigil"/> o círculo que acende no lugar da caixinha de marcar.
	/// </summary>
	public static class Ornament
	{
		private const int PanelSize = 48;
		private const int PanelCorner = 14;

		private static readonly Dictionary<string, Texture2D> Cache = new();

		/// <summary>Painel de couro com moldura: o estilo de todo <c>PanelContainer</c>.</summary>
		public static StyleBoxTexture Panel(Color fill, Color border, int margin = 14)
		{
			var box = new StyleBoxTexture
			{
				Texture = PanelTexture(fill, border),
				AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit,
				AxisStretchVertical = StyleBoxTexture.AxisStretchMode.TileFit,
			};
			box.SetTextureMarginAll(PanelCorner);
			box.SetContentMarginAll(margin);
			return box;
		}

		private static Texture2D PanelTexture(Color fill, Color border) => Cached($"panel{fill.ToHtml()}{border.ToHtml()}", () =>
		{
			var image = Image.CreateEmpty(PanelSize, PanelSize, false, Image.Format.Rgba8);
			var random = new Random(7);
			const float radius = 7f;
			for (var y = 0; y < PanelSize; y++)
			{
				for (var x = 0; x < PanelSize; x++)
				{
					// Canto arredondado: fora do raio fica transparente, com meia-borda suavizada.
					var cornerX = Math.Min(x, PanelSize - 1 - x);
					var cornerY = Math.Min(y, PanelSize - 1 - y);
					var edge = Math.Min(cornerX, cornerY);
					var outside = 0f;
					if (cornerX < radius && cornerY < radius)
					{
						var distance = new Vector2(radius - cornerX - 0.5f, radius - cornerY - 0.5f).Length();
						outside = Mathf.Clamp(distance - radius + 1, 0, 1);
						edge = (int)Math.Max(0, radius - distance);
					}

					// Grão do couro: variação pequena, mais escura perto da borda.
					var grain = (float)(random.NextDouble() - 0.5) * 0.05f;
					var shade = edge < 6 ? (6 - edge) * 0.018f : 0;
					var color = new Color(fill.R + grain - shade, fill.G + grain - shade, fill.B + grain * 0.8f - shade, fill.A);

					if (edge < 2)
						color = border;
					else if (edge == 2)
						color = border.Darkened(0.55f);
					else if (edge == 5)
						color = color.Lerp(border, 0.45f);

					color.A *= 1 - outside;
					image.SetPixel(x, y, color);
				}
			}

			// Um losango de ouro em cada canto, sobre o filete.
			foreach (var (cx, cy) in new[] { (5, 5), (PanelSize - 6, 5), (5, PanelSize - 6), (PanelSize - 6, PanelSize - 6) })
			{
				for (var dy = -2; dy <= 2; dy++)
				{
					for (var dx = -2; dx <= 2; dx++)
					{
						if (Math.Abs(dx) + Math.Abs(dy) <= 2)
							image.SetPixel(cx + dx, cy + dy, Math.Abs(dx) + Math.Abs(dy) == 0 ? border.Lightened(0.3f) : border);
					}
				}
			}

			return ImageTexture.CreateFromImage(image);
		});

		/// <summary>A gema que desliza na barra de rolagem: pontuda nas pontas, o miolo estica.</summary>
		public static StyleBoxTexture Gem(Color color)
		{
			var box = new StyleBoxTexture { Texture = GemTexture(color) };
			box.TextureMarginTop = 7;
			box.TextureMarginBottom = 7;
			box.SetContentMarginAll(3);
			return box;
		}

		private static Texture2D GemTexture(Color color) => Cached($"gem{color.ToHtml()}", () =>
		{
			const int width = 8;
			const int height = 24;
			var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
			for (var y = 0; y < height; y++)
			{
				// Largura da pedra em cada linha: cresce nas pontas, reta no miolo.
				var tip = Math.Min(y, height - 1 - y);
				var half = Math.Min(width / 2f, 1 + tip * 0.6f);
				for (var x = 0; x < width; x++)
				{
					var offset = Math.Abs(x + 0.5f - width / 2f);
					if (offset > half)
						continue;
					var facet = x < width / 2 ? color.Lightened(0.25f) : color.Darkened(0.2f);
					image.SetPixel(x, y, offset > half - 1 ? facet.Darkened(0.35f) : facet);
				}
			}

			return ImageTexture.CreateFromImage(image);
		});

		/// <summary>O sigilo de marcar: aceso (brilho e ponto no meio) ou apagado (só o anel).</summary>
		public static Texture2D Sigil(bool lit, Color ring, Color glow) => Cached($"sigil{lit}{ring.ToHtml()}{glow.ToHtml()}", () =>
		{
			const int size = 24;
			var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
			var center = new Vector2(size / 2f, size / 2f);
			for (var y = 0; y < size; y++)
			{
				for (var x = 0; x < size; x++)
				{
					var distance = (new Vector2(x + 0.5f, y + 0.5f) - center).Length();
					var color = new Color(0, 0, 0, 0);
					if (lit && distance < 11.5f)
						color = glow with { A = 0.18f * (1 - distance / 11.5f) + 0.06f };
					if (distance is > 7.5f and < 9.5f)
						color = lit ? glow : ring;
					if (lit && distance < 3.5f)
						color = glow.Lightened(0.3f);
					image.SetPixel(x, y, color);
				}
			}

			return ImageTexture.CreateFromImage(image);
		});

		private static Texture2D Cached(string key, Func<Texture2D> build)
		{
			if (!Cache.TryGetValue(key, out var texture))
			{
				texture = build();
				Cache[key] = texture;
			}

			return texture;
		}
	}
}
