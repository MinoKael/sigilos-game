using System;
using System.Collections.Generic;
using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As texturas do grimório, geradas em código uma vez por cor — nenhuma imagem pronta no projeto.
	///
	/// <see cref="Panel"/> é o couro de capa de livro com grão, a moldura gravada (beira escura, fio de ouro
	/// e um filete por dentro) e uma estrela de quatro pontas em cada canto, onde os filetes se cruzam; vira
	/// um <see cref="StyleBoxTexture"/> de 9 partes: os cantos ficam fixos e o miolo repete (Tile), então o
	/// grão não estica em painel grande. <see cref="Page"/> é a folha de pergaminho das páginas do
	/// Grimório do Invocador. <see cref="Gem"/> é a pedra da barra de rolagem e <see cref="Sigil"/> o
	/// círculo que acende uma estrela no lugar da caixinha de marcar.
	/// </summary>
	public static class Ornament
	{
		private const int PanelSize = 48;
		private const int PanelCorner = 14;
		private const int PageSize = 128;
		private const int PageCorner = 30;

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
					var (edge, outside) = Edge(x, y, PanelSize, radius);

					// Grão do couro: variação pequena e, perto da beira, o couro gasto mais escuro.
					var grain = (float)(random.NextDouble() - 0.5) * 0.06f;
					var shade = edge < 8 ? (8 - edge) * 0.016f : 0;
					var color = new Color(fill.R + grain - shade, fill.G + grain - shade, fill.B + grain - shade * 0.6f, fill.A);

					// A moldura gravada: a beira escura da capa, o fio de ouro, a sombra dele e o filete por dentro.
					if (edge == 0)
						color = border.Darkened(0.45f);
					else if (edge <= 2)
						color = border;
					else if (edge == 3)
						color = color.Lerp(new Color(0, 0, 0, fill.A), 0.35f);
					else if (edge == 6)
						color = color.Lerp(border, 0.5f);

					color.A *= 1 - outside;
					image.SetPixel(x, y, color);
				}
			}

			// A estrela de quatro pontas em cada canto, onde os filetes se cruzam.
			foreach (var (cx, cy) in new[] { (6, 6), (PanelSize - 7, 6), (6, PanelSize - 7), (PanelSize - 7, PanelSize - 7) })
			{
				var center = new Vector2(cx + 0.5f, cy + 0.5f);
				Star(image, center, 5.5f, border.Lightened(0.2f));
				Star(image, center, 2.2f, border.Lightened(0.55f));
			}

			return ImageTexture.CreateFromImage(image);
		});

		/// <summary>
		/// A folha de pergaminho: papel claro com manchas suaves, as bordas amareladas de velho e um fio de
		/// tinta na beira. Estica em vez de repetir: o grão esticado vira a mancha do papel, sem emenda.
		/// </summary>
		public static StyleBoxTexture Page(int margin = 18)
		{
			var box = new StyleBoxTexture { Texture = PageTexture(Palette.Parchment, Palette.ParchmentShade, Palette.Ink) };
			box.SetTextureMarginAll(PageCorner);
			box.SetContentMarginAll(margin);
			return box;
		}

		private static Texture2D PageTexture(Color paper, Color shade, Color ink) => Cached($"page{paper.ToHtml()}{shade.ToHtml()}", () =>
		{
			var image = Image.CreateEmpty(PageSize, PageSize, false, Image.Format.Rgba8);
			var random = new Random(11);
			var blots = Grid(random, 9);
			var fibers = Grid(random, 33);
			const float radius = 5f;
			for (var y = 0; y < PageSize; y++)
			{
				for (var x = 0; x < PageSize; x++)
				{
					var (edge, outside) = Edge(x, y, PageSize, radius);
					var u = x / (float)(PageSize - 1);
					var v = y / (float)(PageSize - 1);

					// As manchas do papel (grandes e pequenas) e o amarelado que cresce para a beira.
					var blot = Sample(blots, u, v) * 0.7f + Sample(fibers, u, v) * 0.3f;
					var age = Mathf.Clamp(1 - edge / 24f, 0, 1);
					var color = paper.Lerp(shade, age * age * 0.85f + (blot - 0.5f) * 0.22f);
					var grain = (float)(random.NextDouble() - 0.5) * 0.035f;
					color = new Color(color.R + grain, color.G + grain, color.B + grain * 0.8f);

					// O fio de tinta na beira da folha.
					if (edge == 0)
						color = color.Lerp(ink, 0.7f);
					else if (edge == 1)
						color = color.Lerp(ink, 0.25f);

					color.A = 1 - outside;
					image.SetPixel(x, y, color);
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

		/// <summary>A gema lapidada que corre na barra de energia do controle deslizante (losango com facetas).</summary>
		public static Texture2D GemIcon(Color color) => Cached($"gemicon{color.ToHtml()}", () =>
		{
			const int size = 26;
			var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
			var center = size / 2f;
			for (var y = 0; y < size; y++)
			{
				for (var x = 0; x < size; x++)
				{
					var dx = x + 0.5f - center;
					var dy = y + 0.5f - center;
					var distance = Math.Abs(dx) + Math.Abs(dy);
					if (distance > center - 1)
						continue;
					// Quatro facetas: a de cima à esquerda mais clara, a de baixo à direita mais escura.
					var facet = dx < 0 && dy < 0 ? color.Lightened(0.35f)
						: dx >= 0 && dy >= 0 ? color.Darkened(0.35f)
						: color;
					var edge = distance > center - 3 ? Colors.Black.Lerp(facet, 0.35f) : facet;
					image.SetPixel(x, y, edge);
				}
			}

			return ImageTexture.CreateFromImage(image);
		});

		/// <summary>O sigilo de marcar: aceso (brilho e uma estrela no meio) ou apagado (só o anel).</summary>
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
					image.SetPixel(x, y, color);
				}
			}

			if (lit)
				Star(image, center, 6f, glow.Lightened(0.3f));
			return ImageTexture.CreateFromImage(image);
		});

		/// <summary>
		/// A distância do pixel até a beira da textura quadrada de lado <paramref name="size"/>, com os cantos
		/// arredondados no raio dado, e quanto dele fica fora do canto (0 dentro, 1 fora, suavizado).
		/// </summary>
		private static (int Edge, float Outside) Edge(int x, int y, int size, float radius)
		{
			var cornerX = Math.Min(x, size - 1 - x);
			var cornerY = Math.Min(y, size - 1 - y);
			var edge = Math.Min(cornerX, cornerY);
			if (cornerX >= radius || cornerY >= radius)
				return (edge, 0);
			var distance = new Vector2(radius - cornerX - 0.5f, radius - cornerY - 0.5f).Length();
			return ((int)Math.Max(0, radius - distance), Mathf.Clamp(distance - radius + 1, 0, 1));
		}

		/// <summary>
		/// Pinta a estrela de quatro pontas (a de <see cref="Components.Starlight.Sparkle"/>) de raio
		/// <paramref name="radius"/>, com a borda suavizada por 16 amostras por pixel.
		/// </summary>
		private static void Star(Image image, Vector2 center, float radius, Color color)
		{
			var reach = (int)Mathf.Ceil(radius) + 1;
			var limit = Mathf.Sqrt(radius);
			for (var y = (int)center.Y - reach; y <= (int)center.Y + reach; y++)
			{
				for (var x = (int)center.X - reach; x <= (int)center.X + reach; x++)
				{
					if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
						continue;
					var cover = 0f;
					for (var sy = 0; sy < 4; sy++)
					{
						for (var sx = 0; sx < 4; sx++)
						{
							var dx = Math.Abs(x + (sx + 0.5f) / 4 - center.X);
							var dy = Math.Abs(y + (sy + 0.5f) / 4 - center.Y);
							if (Mathf.Sqrt(dx) + Mathf.Sqrt(dy) <= limit)
								cover += 1 / 16f;
						}
					}

					if (cover > 0)
						image.SetPixel(x, y, image.GetPixel(x, y).Lerp(color, cover));
				}
			}
		}

		/// <summary>Uma grade de <paramref name="cells"/>² valores ao acaso, para o ruído suave do papel.</summary>
		private static float[,] Grid(Random random, int cells)
		{
			var grid = new float[cells, cells];
			for (var y = 0; y < cells; y++)
			{
				for (var x = 0; x < cells; x++)
					grid[x, y] = (float)random.NextDouble();
			}

			return grid;
		}

		/// <summary>O ruído da grade em (<paramref name="u"/>, <paramref name="v"/>), de 0 a 1, interpolado suave.</summary>
		private static float Sample(float[,] grid, float u, float v)
		{
			var cells = grid.GetLength(0) - 1;
			var gx = u * cells;
			var gy = v * cells;
			var x = Math.Min((int)gx, cells - 1);
			var y = Math.Min((int)gy, cells - 1);
			var fx = Smooth(gx - x);
			var fy = Smooth(gy - y);
			var top = Mathf.Lerp(grid[x, y], grid[x + 1, y], fx);
			var bottom = Mathf.Lerp(grid[x, y + 1], grid[x + 1, y + 1], fx);
			return Mathf.Lerp(top, bottom, fy);
		}

		private static float Smooth(float t) => t * t * (3 - 2 * t);

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
