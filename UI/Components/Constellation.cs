using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A constelação da Canalização: um sigilo grande no centro e estrelas em volta, ligadas a ele por
	/// fios de luz por onde corre uma faísca. As estrelas ficam em vagas fixas, de ângulos e distâncias
	/// desiguais, para parecer uma constelação e não uma roda, e são só desenho: brilham e piscam, mas
	/// quem se toca é o centro. Em volta do centro, um anel mostra o quanto a canalização já encheu
	/// (<see cref="Progress"/>).
	///
	/// Só arruma e desenha: o sigilo do centro e o que ele faz são da tela.
	/// </summary>
	public partial class Constellation : Control
	{
		/// <summary>Ângulo (graus, 0 à direita, sentido horário) e distância relativa de cada estrela.</summary>
		private static readonly (float Angle, float Distance)[] Places =
		{
			(-152, 0.96f), (-98, 0.82f), (-42, 1.0f), (6, 0.92f), (46, 0.74f), (102, 0.86f), (152, 0.96f),
		};

		/// <summary>O raio da estrela maior; cada uma tem o seu, entre 60% e 100% disso.</summary>
		private const float StarSize = 9;

		private Control? _center;
		private float _time;

		public Constellation()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			Resized += Arrange;
		}

		/// <summary>Quanto o anel do centro encheu, de 0 a 1.</summary>
		public float Progress { get; set; }

		/// <summary>Põe o sigilo do centro (o tamanho dele é o <see cref="Control.CustomMinimumSize"/>).</summary>
		public void SetCenter(Control center)
		{
			if (_center != null)
				Layout.Discard(_center);
			_center = center;
			AddChild(center);
			Arrange();
		}

		public override Vector2 _GetMinimumSize() => _center == null ? Vector2.Zero : _center.CustomMinimumSize + new Vector2(80, 80);

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
			var radii = Radii;

			for (var i = 0; i < Places.Length; i++)
			{
				var target = Star(i, center, radii);
				var direction = (target - center).Normalized();
				var from = center + direction * (core + 20);
				var to = target - direction * (StarSize + 3);
				DrawLine(from, to, new Color(Palette.Arcane, 0.07f), 7, true);
				DrawLine(from, to, new Color(Palette.Gold, 0.4f), 1.5f, true);

				// A faísca que corre do centro para a estrela.
				var t = Mathf.PosMod(_time * 0.22f + i * 0.37f, 1f);
				var spark = from.Lerp(to, t);
				DrawCircle(spark, 5, new Color(Palette.Spirit, 0.12f));
				DrawCircle(spark, 2.2f, new Color(Palette.Spirit, 0.85f * Mathf.Sin(t * Mathf.Pi)));

				// A estrela: um brilho de quatro pontas, que pisca devagar, cada uma no seu tempo.
				var twinkle = 0.65f + 0.35f * Mathf.Sin(_time * 1.7f + i * 1.3f);
				var size = StarSize * (0.6f + 0.4f * ((i * 37) % 10) / 9f);
				Sparkle(target, size * 2.2f, new Color(Palette.Gold, 0.12f * twinkle));
				Sparkle(target, size, new Color(Palette.Gold.Lightened(0.25f), twinkle));
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

		private Vector2 Middle => Size / 2;

		/// <summary>A elipse das estrelas: o espaço que sobra, menos a margem da estrela maior.</summary>
		private Vector2 Radii => new(Mathf.Max(0, Size.X / 2 - StarSize * 2.4f), Mathf.Max(0, Size.Y / 2 - StarSize * 2.4f));

		private static Vector2 Star(int index, Vector2 center, Vector2 radii)
		{
			var (angle, distance) = Places[index];
			var rad = Mathf.DegToRad(angle);
			return center + new Vector2(Mathf.Cos(rad) * radii.X, Mathf.Sin(rad) * radii.Y) * distance;
		}

		private void Sparkle(Vector2 at, float size, Color color)
		{
			var waist = size * 0.24f;
			DrawColoredPolygon(new[]
			{
				at + new Vector2(0, -size), at + new Vector2(waist, -waist),
				at + new Vector2(size, 0), at + new Vector2(waist, waist),
				at + new Vector2(0, size), at + new Vector2(-waist, waist),
				at + new Vector2(-size, 0), at + new Vector2(-waist, -waist),
			}, color);
		}

		private void Arrange()
		{
			if (_center == null)
				return;

			_center.Size = _center.CustomMinimumSize;
			_center.Position = Middle - _center.Size / 2;
			QueueRedraw();
		}
	}
}
