using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O menu de atalhos do Santuário: um sigilo grande no centro e estrelas em volta, ligadas por fios
	/// de luz por onde corre uma faísca. As estrelas ficam em vagas fixas, de ângulos e distâncias
	/// desiguais, para parecer uma constelação e não uma roda. Em volta do centro, um anel mostra o
	/// quanto a canalização já encheu (<see cref="Progress"/>).
	///
	/// Só arruma e desenha: quem cria os sigilos e o que eles fazem é a tela.
	/// </summary>
	public partial class Constellation : Control
	{
		/// <summary>Ângulo (graus, 0 à direita, sentido horário) e distância relativa de cada vaga.</summary>
		private static readonly (float Angle, float Distance)[] Places =
		{
			(-152, 0.96f), (-98, 0.82f), (-42, 1.0f), (6, 0.92f), (46, 0.74f), (102, 0.86f), (152, 0.96f),
		};

		private readonly List<Control?> _satellites = new();
		private Control? _center;
		private float _time;

		public Constellation()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			Resized += Arrange;
		}

		/// <summary>Quanto o anel do centro encheu, de 0 a 1.</summary>
		public float Progress { get; set; }

		/// <summary>Quantas vagas existem.</summary>
		public static int Capacity => Places.Length;

		/// <summary>Põe o sigilo do centro e os das vagas (nulo = vaga sem ninguém).</summary>
		public void Set(Control center, IReadOnlyList<Control?> satellites)
		{
			foreach (var child in GetChildren())
				Layout.Discard(child);

			_center = center;
			AddChild(center);
			_satellites.Clear();
			foreach (var satellite in satellites)
			{
				_satellites.Add(satellite);
				if (satellite != null)
					AddChild(satellite);
			}

			Arrange();
		}

		/// <summary>Um controle extra preso ao centro (a canalização rápida, as recompensas): não entra nas vagas.</summary>
		public void Attach(Control control, Vector2 offsetFromCenter)
		{
			control.SetMeta("offset", offsetFromCenter);
			AddChild(control);
			Arrange();
		}

		public override void _Process(double delta)
		{
			_time += (float)delta;
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (_center == null)
				return;

			var center = Middle;
			var core = _center.Size.X / 2;

			for (var i = 0; i < _satellites.Count; i++)
			{
				if (_satellites[i] is not { } star)
					continue;

				var target = star.Position + star.Size / 2;
				var direction = (target - center).Normalized();
				var from = center + direction * (core + 12);
				var to = target - direction * (star.Size.X / 2 + 4);
				DrawLine(from, to, new Color(Palette.Arcane, 0.07f), 7, true);
				DrawLine(from, to, new Color(Palette.Gold, 0.4f), 1.5f, true);

				// A faísca que corre do centro para a estrela.
				var t = Mathf.PosMod(_time * 0.22f + i * 0.37f, 1f);
				var spark = from.Lerp(to, t);
				DrawCircle(spark, 5, new Color(Palette.Spirit, 0.12f));
				DrawCircle(spark, 2.2f, new Color(Palette.Spirit, 0.85f * Mathf.Sin(t * Mathf.Pi)));
			}

			// O anel da canalização em volta do centro.
			var ring = core + 8;
			DrawArc(center, ring, 0, Mathf.Tau, 72, new Color(0, 0, 0, 0.55f), 7, true);
			if (Progress > 0)
				DrawArc(center, ring, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Mathf.Clamp(Progress, 0, 1), 72, Palette.Spirit, 4, true);
			for (var k = 0; k < 24; k++)
			{
				var angle = k * Mathf.Tau / 24 + _time * 0.05f;
				var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				DrawLine(center + dir * (ring + 7), center + dir * (ring + (k % 3 == 0 ? 14 : 10)), new Color(Palette.Gold, 0.35f), 1.5f, true);
			}
		}

		private Vector2 Middle => new(Size.X / 2, Size.Y * 0.47f);

		private void Arrange()
		{
			if (_center == null)
				return;

			var middle = Middle;
			_center.Size = _center.CustomMinimumSize;
			_center.Position = middle - _center.Size / 2;

			// Elipse: a tela é larga, as estrelas se espalham mais para os lados.
			var radii = new Vector2(Mathf.Min(Size.X * 0.36f, 440), Mathf.Min(Size.Y * 0.4f, 260));
			for (var i = 0; i < _satellites.Count && i < Places.Length; i++)
			{
				if (_satellites[i] is not { } star)
					continue;
				var (angle, distance) = Places[i];
				var rad = Mathf.DegToRad(angle);
				star.Size = star.CustomMinimumSize;
				star.Position = middle + new Vector2(Mathf.Cos(rad) * radii.X, Mathf.Sin(rad) * radii.Y) * distance - star.Size / 2;
			}

			foreach (var child in GetChildren())
			{
				if (child is Control control && control.HasMeta("offset"))
				{
					control.Size = control.GetCombinedMinimumSize();
					control.Position = middle + control.GetMeta("offset").AsVector2() - control.Size / 2;
				}
			}

			QueueRedraw();
		}
	}
}
