using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Anel de energia entalhado: sulco escuro e a luz correndo por dentro, a partir do alto, no sentido
	/// horário. É a barra de progresso redonda (a garantia do ritual, a carga de um sigilo).
	/// </summary>
	public partial class EnergyRing : Control
	{
		private float _progress;

		public EnergyRing(Color color, float thickness = 6)
		{
			Color = color;
			Thickness = thickness;
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public Color Color { get; set; }
		public float Thickness { get; }

		public float Progress
		{
			get => _progress;
			set
			{
				_progress = Mathf.Clamp(value, 0, 1);
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			var center = Size / 2;
			var radius = Mathf.Min(Size.X, Size.Y) / 2 - Thickness;
			DrawArc(center, radius, 0, Mathf.Tau, 96, new Color(0, 0, 0, 0.55f), Thickness + 3, true);
			DrawArc(center, radius + Thickness / 2 + 2, 0, Mathf.Tau, 96, new Color(Palette.GoldDark, 0.8f), 1.2f, true);
			if (_progress <= 0)
				return;
			var end = -Mathf.Pi / 2 + Mathf.Tau * _progress;
			DrawArc(center, radius, -Mathf.Pi / 2, end, 96, new Color(Color, 0.25f), Thickness + 6, true);
			DrawArc(center, radius, -Mathf.Pi / 2, end, 96, Color, Thickness, true);
		}
	}
}
