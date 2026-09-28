using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O fundo de tela (Assets/Shaders/backdrop.gdshader): pedra quente, luz de vela e o anel de sigilo
	/// girando. O centro da luz e do anel fica no meio da tela ou, com um <c>focus</c>, no centro dele —
	/// e acompanha se ele se mexer ou a janela mudar de tamanho.
	/// </summary>
	public partial class Backdrop : ColorRect
	{
		private static Shader? _shader;

		private readonly Control? _focus;
		private readonly ShaderMaterial _material;
		private Vector2 _center = new(-1, -1);

		/// <param name="ring">Com o anel de sigilo girando; a batalha tira, porque o chão dela já é o oval.</param>
		public Backdrop(Control? focus, bool ring = true)
		{
			_focus = focus;
			_shader ??= GD.Load<Shader>("res://Assets/Shaders/backdrop.gdshader");
			_material = new ShaderMaterial { Shader = _shader };
			_material.SetShaderParameter("edge_color", Palette.Background);
			_material.SetShaderParameter("glow_color", Palette.BackgroundGlow);
			_material.SetShaderParameter("rune_color", Palette.Gold);
			_material.SetShaderParameter("ring_strength", ring ? 1f : 0f);
			Material = _material;
			Color = Palette.Background;
			MouseFilter = MouseFilterEnum.Ignore;
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			Resized += () => _material.SetShaderParameter("aspect", Size.X / Mathf.Max(1, Size.Y));
			SetProcess(focus != null);
		}

		public override void _Ready() => Follow();

		public override void _Process(double delta) => Follow();

		/// <summary>Põe o centro no foco (em UV do fundo); sem foco, no meio.</summary>
		private void Follow()
		{
			var center = new Vector2(0.5f, 0.5f);
			if (_focus != null && IsInstanceValid(_focus) && _focus.IsInsideTree() && Size.X > 0 && Size.Y > 0)
			{
				var rect = _focus.GetGlobalRect();
				center = (rect.GetCenter() - GetGlobalRect().Position) / Size;
			}

			if (center.IsEqualApprox(_center))
				return;
			_center = center;
			_material.SetShaderParameter("center", center);
		}
	}
}
