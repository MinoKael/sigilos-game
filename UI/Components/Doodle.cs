using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Silhueta "desenhada a caneta" (adaptado de Rabiscos&amp;Runas): pinta o SVG preto com a cor da
	/// tinta e faz o traço ferver a 8 fps — a "linha tremida" do GDD, seção 5 — pelo shader
	/// Assets/Shaders/doodle.gdshader. Sem textura, não desenha nada.
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

			_shader ??= GD.Load<Shader>("res://Assets/Shaders/doodle.gdshader");
			var material = new ShaderMaterial { Shader = _shader };
			material.SetShaderParameter("ink_color", ink);
			material.SetShaderParameter("boil_strength", boil ? 0.006f : 0f);
			material.SetShaderParameter("seed", GD.Randf() * 100f);
			Material = material;
		}

		public void SetInk(Color ink) => ((ShaderMaterial)Material).SetShaderParameter("ink_color", ink);

		/// <summary>Ícone pequeno de tamanho fixo, parado, para rótulos e botões.</summary>
		public static Doodle Icon(Texture2D? texture, int size, Color? ink = null) =>
			new(texture, ink ?? Palette.Ink, boil: false) { CustomMinimumSize = new Vector2(size, size) };
	}
}
