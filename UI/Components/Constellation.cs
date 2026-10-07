using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A constelação da Canalização, ocupando o painel inteiro: um sigilo grande no centro e sete orbes em
	/// volta, ligados a ele por fios de luz por onde corre uma faísca. Os orbes ficam em vagas fixas, de
	/// ângulos e distâncias desiguais, para parecer uma constelação e não uma roda; são só desenho (cada
	/// um com uma estrela que pisca), e quem se toca é o centro. Por baixo, a carta do céu a tinta de índigo
	/// (duas órbitas, as doze casas e as marcas do grau, como num astrolábio), presa ao tamanho do painel e
	/// não ao tempo. Em volta do centro, um anel mostra o
	/// quanto a canalização já encheu (<see cref="Progress"/>). Embaixo do centro vai o que a tela prender
	/// com <see cref="Attach"/> (o tempo, o que juntou e o Coletar).
	///
	/// Só arruma e desenha: o sigilo do centro e o que ele faz são da tela. Os fios e as estrelas são os de
	/// <see cref="Starlight"/>, os mesmos do mapa da Exploração Estelar (<see cref="StarChart"/>).
	/// </summary>
	public partial class Constellation : Control
	{
		/// <summary>Ângulo (graus, 0 à direita, sentido horário) e distância relativa de cada orbe.</summary>
		private static readonly (float Angle, float Distance)[] Places =
		{
			(-152, 0.96f), (-98, 0.82f), (-42, 1.0f), (6, 0.92f), (42, 0.78f), (142, 0.96f),
		};

		/// <summary>O raio dos orbes em volta.</summary>
		private const float Orb = 30;

		/// <summary>A altura do centro, em fração do painel: um pouco acima do meio, para caber o que vai embaixo.</summary>
		private const float CenterHeight = 0.45f;

		/// <summary>A tinta da carta do céu: índigo, apagada, para ficar atrás dos fios.</summary>
		private static readonly Color ChartInk = new(Palette.Indigo, 0.45f);

		/// <summary>As órbitas da carta, em fração da elipse dos orbes.</summary>
		private static readonly float[] Orbits = { 0.62f, 1.0f };

		private const int Houses = 12;
		private const int Degrees = 72;
		private const int Segments = 96;

		/// <summary>A carta do céu, refeita só quando o painel muda de tamanho.</summary>
		private readonly Vector2[][] _orbits = { new Vector2[Segments + 1], new Vector2[Segments + 1] };
		private readonly Vector2[] _houses = new Vector2[Houses * 2];
		private readonly Vector2[] _degrees = new Vector2[Degrees * 2];

		private Control? _center;
		private Control? _attached;
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

		/// <summary>Prende um controle logo embaixo do anel do centro, centrado (fica por cima dos fios).</summary>
		public void Attach(Control control)
		{
			_attached = control;
			AddChild(control);
			control.MinimumSizeChanged += Arrange;
			Arrange();
		}

		public override Vector2 _GetMinimumSize() => _center == null ? Vector2.Zero : _center.CustomMinimumSize + new Vector2(Orb * 6, Orb * 6);

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

			foreach (var orbit in _orbits)
				DrawPolyline(orbit, ChartInk, 1, true);
			DrawMultiline(_houses, ChartInk, 1);
			DrawMultiline(_degrees, ChartInk, 1);

			for (var i = 0; i < Places.Length; i++)
			{
				var target = OrbAt(i, center, radii);
				var direction = (target - center).Normalized();

				// O fio de luz, com a faísca que corre do centro para o orbe.
				Starlight.Thread(this, center + direction * (core + 12), target - direction * (Orb + 4), _time, i * 0.37f, Palette.Gold, Palette.Spirit);

				// O orbe: o mesmo sigilo de pedra dos botões redondos, com uma estrela que pisca no seu tempo.
				DrawCircle(target, Orb, Palette.Panel);
				DrawArc(target, Orb, 0, Mathf.Tau, 48, Palette.GoldDark, 2, true);
				DrawArc(target, Orb - 4, 0, Mathf.Tau, 48, new Color(Palette.GoldDark, 0.35f), 1, true);
				Starlight.Twinkle(this, target, Orb, _time, i * 1.3f, Palette.Gold);
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

		private Vector2 Middle => new(Size.X / 2, Size.Y * CenterHeight);

		/// <summary>
		/// A elipse dos orbes: larga, como o painel, sem deixar orbe nenhum sair dele (o de cima é o que
		/// encosta primeiro: a vaga dele sobe 81% do raio).
		/// </summary>
		private Vector2 Radii => new(
			Mathf.Max(0, Mathf.Min(Size.X * 0.46f, Size.X / 2 - Orb - 10)),
			Mathf.Max(0, Mathf.Min(Size.Y * 0.41f, (Size.Y * CenterHeight - Orb - 8) / 0.81f)));

		private static Vector2 OrbAt(int index, Vector2 center, Vector2 radii)
		{
			var (angle, distance) = Places[index];
			var rad = Mathf.DegToRad(angle);
			return center + new Vector2(Mathf.Cos(rad) * radii.X, Mathf.Sin(rad) * radii.Y) * distance;
		}

		private void Arrange()
		{
			if (_center == null)
				return;

			var middle = Middle;
			Plot(middle, _center.CustomMinimumSize.X / 2 + 30, Radii);
			_center.Size = _center.CustomMinimumSize;
			_center.Position = middle - _center.Size / 2;
			if (_attached != null)
			{
				_attached.Size = _attached.GetCombinedMinimumSize();
				var top = middle.Y + _center.Size.Y / 2 + 36;
				_attached.Position = new Vector2(middle.X - _attached.Size.X / 2, Mathf.Min(top, Size.Y - _attached.Size.Y));
			}

			QueueRedraw();
		}

		/// <summary>Põe a carta do céu no tamanho do painel: as órbitas na elipse dos orbes, as casas do anel do centro até a órbita de fora e as marcas do grau por fora dela.</summary>
		private void Plot(Vector2 center, float inner, Vector2 radii)
		{
			for (var o = 0; o < Orbits.Length; o++)
				for (var k = 0; k <= Segments; k++)
					_orbits[o][k] = center + Direction(k * Mathf.Tau / Segments) * radii * Orbits[o];

			for (var h = 0; h < Houses; h++)
			{
				var dir = Direction(h * Mathf.Tau / Houses);
				_houses[h * 2] = center + dir * inner;
				_houses[h * 2 + 1] = center + dir * radii;
			}

			for (var d = 0; d < Degrees; d++)
			{
				var dir = Direction(d * Mathf.Tau / Degrees);
				_degrees[d * 2] = center + dir * radii;
				_degrees[d * 2 + 1] = center + dir * (radii + new Vector2(d % 6 == 0 ? 9 : 5, d % 6 == 0 ? 9 : 5));
			}
		}

		private static Vector2 Direction(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));
	}
}
