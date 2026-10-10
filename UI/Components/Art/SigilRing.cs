using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Um círculo de conjuração: dois anéis entalhados com marcas de runa, os controles em volta, a
	/// intervalos iguais a partir do alto, e um no centro. É o círculo das 6 runas.
	/// </summary>
	public partial class SigilRing : Control
	{
		private readonly List<Control> _around = new();
		private Control? _center;

		public SigilRing(float diameter)
		{
			CustomMinimumSize = new Vector2(diameter, diameter);
			MouseFilter = MouseFilterEnum.Ignore;
			Resized += Arrange;
		}

		/// <summary>Distância dos controles de volta até o centro, relativa ao raio.</summary>
		public float Spread { get; set; } = 0.78f;

		public void Set(Control? center, IReadOnlyList<Control> around)
		{
			foreach (var child in GetChildren())
				Layout.Discard(child);
			_center = center;
			if (center != null)
				AddChild(center);
			_around.Clear();
			foreach (var control in around)
			{
				_around.Add(control);
				AddChild(control);
			}

			Arrange();
		}

		public override void _Draw()
		{
			var center = Size / 2;
			var radius = Mathf.Min(Size.X, Size.Y) / 2 - 6;
			DrawCircle(center, radius, new Color(Palette.Inset, 0.55f));
			DrawArc(center, radius, 0, Mathf.Tau, 96, new Color(0, 0, 0, 0.5f), 8, true);
			DrawArc(center, radius, 0, Mathf.Tau, 96, Palette.GoldDark, 2, true);
			DrawArc(center, radius - 14, 0, Mathf.Tau, 96, new Color(Palette.Gold, 0.45f), 1.5f, true);
			DrawArc(center, radius * 0.42f, 0, Mathf.Tau, 64, new Color(Palette.Gold, 0.25f), 1, true);
			for (var k = 0; k < 36; k++)
			{
				var angle = k * Mathf.Tau / 36;
				var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				var length = k % 3 == 0 ? 10 : 5;
				DrawLine(center + dir * (radius - 13), center + dir * (radius - 13 + length), new Color(Palette.Gold, 0.5f), 1.2f, true);
			}

			// Fios do centro a cada controle: o traçado do sigilo.
			for (var i = 0; i < _around.Count; i++)
			{
				var a = Place(i, center, radius);
				var b = Place((i + 1) % _around.Count, center, radius);
				if (_around.Count > 2)
					DrawLine(a, b, new Color(Palette.Gold, 0.18f), 1.2f, true);
				DrawLine(center, a, new Color(Palette.Arcane, 0.08f), 4, true);
			}
		}

		private Vector2 Place(int index, Vector2 center, float radius)
		{
			var angle = -Mathf.Pi / 2 + index * Mathf.Tau / Mathf.Max(1, _around.Count);
			return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * Spread;
		}

		private void Arrange()
		{
			var center = Size / 2;
			var radius = Mathf.Min(Size.X, Size.Y) / 2 - 6;
			if (_center != null)
			{
				_center.Size = _center.CustomMinimumSize;
				_center.Position = center - _center.Size / 2;
			}

			for (var i = 0; i < _around.Count; i++)
			{
				var control = _around[i];
				control.Size = control.CustomMinimumSize;
				control.Position = Place(i, center, radius) - control.Size / 2;
			}

			QueueRedraw();
		}
	}
}
