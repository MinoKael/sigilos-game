using Godot;
using Sigilos.Core.Content;
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
	///
	/// Desperto (<c>aura</c> com o elemento), o desenho é o mesmo, mas pelo
	/// Assets/Shaders/doodle_awakened.gdshader: a silhueta um pouco menor, com a borda acesa por dentro
	/// na cor do elemento e o anel dele por cima, cada elemento mexendo do seu jeito.
	/// </summary>
	public partial class Doodle : TextureRect
	{
		private static Shader? _shader;
		private static Shader? _awakenedShader;

		/// <param name="aura">O elemento do monstro desperto: troca o traço pelo do desperto (aura e anel).</param>
		public Doodle(Texture2D? texture, Color ink, bool boil = true, Element? aura = null)
		{
			Texture = texture;
			ExpandMode = ExpandModeEnum.IgnoreSize;
			StretchMode = StretchModeEnum.KeepAspectCentered;
			MouseFilter = MouseFilterEnum.Ignore;
			TextureFilter = TextureFilterEnum.LinearWithMipmaps;

			_shader ??= GD.Load<Shader>("res://Assets/Shaders/doodle.gdshader");
			var material = new ShaderMaterial { Shader = _shader };
			if (aura is { } element)
			{
				_awakenedShader ??= GD.Load<Shader>("res://Assets/Shaders/doodle_awakened.gdshader");
				material.Shader = _awakenedShader;
				material.SetShaderParameter("aura_color", AuraColor(element));
				material.SetShaderParameter("element", (int)element);
			}

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

		/// <summary>O desenho (<c>Art</c>) recortado numa forma (círculo, losango, cartão), com folga até a borda; com <paramref name="aura"/>, o desperto.</summary>
		public static ArtMask Masked(Texture2D? texture, Color ink, MaskShape shape, float radius = 8, float inset = 0, bool boil = true, Element? aura = null) =>
			ArtMask.Of(new Doodle(texture, ink, boil, aura) { Name = "Art" }, shape, radius, inset);

		/// <summary>A cor da borda acesa e do anel do desperto: a do elemento, mais clara (a Luz quase branca).</summary>
		public static Color AuraColor(Element element) => element switch
		{
			Element.Light => new Color(1f, 0.96f, 0.78f),
			Element.Dark => Palette.Of(Element.Dark).Lightened(0.2f),
			_ => Palette.Of(element).Lightened(0.3f),
		};

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
