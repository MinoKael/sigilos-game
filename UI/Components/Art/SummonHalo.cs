using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O brilho atrás de um cartão de invocação rara: raios girando devagar e um halo que pulsa, nas duas
	/// cores dadas (a segunda alterna nos raios). Desenhado, sem textura; não pega toque.
	/// </summary>
	public partial class SummonHalo : Control
	{
		private readonly Color _color;
		private readonly Color _accent;
		private readonly int _rays;
		private readonly float _spin;
		private float _time;

		/// <param name="rays">Quantos raios: mais raios, invocação mais rara.</param>
		/// <param name="spin">Voltas por segundo, em radianos.</param>
		public SummonHalo(Color color, Color accent, int rays, float spin = 0.5f)
		{
			_color = color;
			_accent = accent;
			_rays = rays;
			_spin = spin;
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public override void _Process(double delta)
		{
			_time += (float)delta;
			QueueRedraw();
		}

		public override void _Draw()
		{
			var center = Size / 2;
			var radius = Mathf.Min(Size.X, Size.Y) / 2;
			var pulse = 1 + 0.05f * Mathf.Sin(_time * 3);

			for (var ring = 6; ring >= 1; ring--)
				DrawCircle(center, radius * (0.3f + 0.11f * ring) * pulse, new Color(_color, 0.07f));

			for (var i = 0; i < _rays; i++)
			{
				var angle = _time * _spin + i * Mathf.Tau / _rays;
				var direction = Vector2.FromAngle(angle);
				var side = Vector2.FromAngle(angle + Mathf.Pi / 2) * radius * 0.07f;
				var inner = center + direction * radius * 0.25f;
				var outer = center + direction * radius * pulse;
				var ink = i % 2 == 0 ? _color : _accent;
				DrawColoredPolygon(new[] { inner + side, outer, inner - side }, new Color(ink, 0.42f));
			}
		}
	}
}
