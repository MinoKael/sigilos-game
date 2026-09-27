using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Silhueta "desenhada a caneta" (adaptado de Rabiscos&amp;Runas): pinta o desenho preto com a cor da
	/// tinta e faz o traço ferver a 8 fps — a "linha tremida" do GDD, seção 5 — pelo shader
	/// Assets/Shaders/doodle.gdshader. Sem textura, não desenha nada.
	///
	/// O desenho fica em "contain": mantém a proporção e cabe inteiro no retângulo. A resolução segue o
	/// tamanho na tela: ao mudar de tamanho, troca pelo PNG renderizado mais próximo (<see cref="Art.Sized"/>),
	/// e o Godot reduz o resto com mipmaps — nada serrilhado. Para recortar no contorno de um componente
	/// (círculo, cartão), use <see cref="Masked"/>.
	/// </summary>
	public partial class Doodle : TextureRect
	{
		private static Shader? _shader;

		public Doodle(Texture2D? texture, Color ink, bool boil = true)
		{
			Texture = texture;
			ExpandMode = ExpandModeEnum.IgnoreSize;
			StretchMode = StretchModeEnum.KeepAspectCentered;
			MouseFilter = MouseFilterEnum.Ignore;
			TextureFilter = TextureFilterEnum.LinearWithMipmaps;

			_shader ??= GD.Load<Shader>("res://Assets/Shaders/doodle.gdshader");
			var material = new ShaderMaterial { Shader = _shader };
			material.SetShaderParameter("ink_color", ink);
			material.SetShaderParameter("boil_strength", boil ? 0.006f : 0f);
			material.SetShaderParameter("seed", GD.Randf() * 100f);
			Material = material;
			Resized += Refine;
		}

		public void SetInk(Color ink) => ((ShaderMaterial)Material).SetShaderParameter("ink_color", ink);

		/// <summary>Troca o desenho, já na resolução do tamanho atual.</summary>
		public void SetArt(Texture2D? texture)
		{
			Texture = texture;
			Refine();
		}

		/// <summary>Ícone pequeno de tamanho fixo, parado, para rótulos e botões. O nó leva o nome do desenho (<c>Lock</c>).</summary>
		public static Doodle Icon(Texture2D? texture, int size, Color? ink = null) =>
			new(texture, ink ?? Palette.Text, boil: false)
			{
				Name = Art.NameOf(texture) is { } name ? Layout.NodeName(name) : "Icon",
				CustomMinimumSize = new Vector2(size, size),
			};

		/// <summary>O desenho (<c>Art</c>) recortado numa forma (círculo, losango, cartão), com folga até a borda.</summary>
		public static ArtMask Masked(Texture2D? texture, Color ink, MaskShape shape, float radius = 8, float inset = 0, bool boil = true) =>
			ArtMask.Of(new Doodle(texture, ink, boil) { Name = "Art" }, shape, radius, inset);

		public override void _Ready() => Refine();

		/// <summary>Escolhe o PNG que cobre o tamanho na tela (com a escala da janela) sem ampliar.</summary>
		private void Refine()
		{
			if (Texture == null || !IsInsideTree())
				return;

			var scale = GetScreenTransform().Scale.Abs();
			var pixels = Mathf.Max(Size.X * scale.X, Size.Y * scale.Y) * 1.1f;
			if (pixels < 1)
				return;

			var sized = Art.Sized(Texture, pixels);
			if (sized != Texture)
				Texture = sized;
		}
	}
}
