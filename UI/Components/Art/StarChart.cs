using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O mapa de uma faixa do céu da Exploração Estelar, feito com a ideia da constelação da Canalização
	/// (<see cref="Constellation"/>): cada constelação é um orbe de pedra no lugar dela no céu de verdade
	/// (<see cref="ConstellationDefinition.Chart"/>), e o percurso passa de uma à seguinte por fios de luz.
	/// No trecho já vencido no mês, o fio é verde e a faísca corre por ele; até a próxima, dourado; dali em
	/// diante, apagado e sem faísca. A próxima a vencer pulsa em verde; a escolhida fica acesa; as que ainda
	/// não abriram ficam apagadas, mas se tocam (dá para olhar o que vem). Sem o ícone da constelação, o orbe
	/// mostra a estrela que pisca, como os da Canalização. Atrás, um campo de estrelas e a grade de
	/// ascensão reta e declinação a tinta de índigo, como a carta do céu da Canalização.
	///
	/// Só desenha e avisa o toque (<see cref="Chosen"/>): o que fazer com a constelação é da tela.
	/// </summary>
	public partial class StarChart : Control
	{
		private const float Orb = 64;

		/// <summary>Quantas estrelas de fundo por 10.000 px² do mapa.</summary>
		private const float StarDensity = 1.1f;

		/// <summary>Colunas de ascensão reta na largura do mapa; as linhas de declinação têm o mesmo passo.</summary>
		private const int Hours = 8;

		private static readonly Color GridInk = new(Palette.Indigo, 0.4f);

		private readonly List<(int Number, SigilButton Node, Vector2 Spot)> _orbs = new();
		private readonly List<(Vector2 Spot, float Size, float Phase)> _field = new();
		private readonly List<(SigilButton Node, float Phase)> _twinklers = new();
		private readonly Layer _chart;
		private readonly Layer _dust;
		private readonly Layer _sparks;
		private readonly float _chartWidth;
		private readonly float _chartHeight;
		private readonly int _cleared;
		private Vector2[] _grid = [];
		private float _time;

		/// <param name="cleared">Constelações vencidas no mês (o fio brilha até aí).</param>
		/// <param name="selected">A constelação escolhida (o andar no percurso).</param>
		public StarChart(ExplorationDefinition exploration, Hemisphere hemisphere, int cleared, int selected)
		{
			_cleared = cleared;
			_chartWidth = (float)exploration.ChartWidth;
			MouseFilter = MouseFilterEnum.Ignore;
			ClipContents = true;

			// Por baixo dos orbes: a grade e os fios, parados, e o campo de estrelas. Por cima, só as faíscas.
			_chart = new Layer("Chart", PaintChart);
			_dust = new Layer("Dust", PaintDust);
			_sparks = new Layer("Sparks", PaintSparks);
			AddChild(_chart);
			AddChild(_dust);

			var constellations = Exploration.Of(exploration, hemisphere).ToList();
			_chartHeight = constellations.Count == 0 ? 0 : constellations.Max(c => (float)c.Constellation.Chart[1]) + Orb;
			foreach (var (number, constellation) in constellations)
			{
				var icon = Art.Icon(constellation.Icon);
				var node = new SigilButton(icon, number == exploration.Constellations.Count ? Orb + 12 : Orb)
				{
					Name = Layout.NodeName(constellation.Id),
					ToggleMode = true,
					ButtonPressed = number == selected,
					Badge = number.ToString(),
				};
				if (icon == null)
					_twinklers.Add((node, (number % 7) * 0.9f));
				if (number <= cleared)
					node.Accent = Palette.Spirit;
				else if (number > cleared + 1)
					node.Modulate = new Color(1, 1, 1, 0.55f);
				node.Highlight = number == cleared + 1 && number != selected;
				var captured = number;
				node.Pressed += () => Chosen?.Invoke(captured);
				_orbs.Add((number, node, new Vector2((float)constellation.Chart[0], (float)constellation.Chart[1])));
				AddChild(node);
			}

			// O campo de estrelas: sempre o mesmo para a mesma faixa.
			var random = new Random((int)hemisphere * 7919 + 17);
			var count = (int)(_chartWidth * Math.Max(_chartHeight, 1) / 10000 * StarDensity);
			for (var i = 0; i < count; i++)
				_field.Add((new Vector2((float)random.NextDouble(), (float)random.NextDouble()), 0.6f + (float)random.NextDouble() * 1.4f, (float)random.NextDouble() * Mathf.Tau));

			AddChild(_sparks);
			Resized += Arrange;
		}

		/// <summary>O jogador tocou numa constelação (o andar dela no percurso).</summary>
		public event Action<int>? Chosen;

		/// <summary>O orbe de uma constelação, para a tela rolar até ele.</summary>
		public Control? OrbOf(int number) => _orbs.FirstOrDefault(o => o.Number == number).Node;

		/// <summary>Quanto o mapa cresce para ocupar a largura que recebeu (o desenho é de <see cref="ExplorationDefinition.ChartWidth"/>).</summary>
		private float Zoom => Size.X > 0 ? Size.X / _chartWidth : 1;

		public override Vector2 _GetMinimumSize() => new(Mathf.Min(_chartWidth, 420), _chartHeight * Zoom);

		public override void _Ready() => Arrange();

		/// <summary>Quantas vezes por segundo a faísca e o brilho das estrelas andam; o campo de estrelas, muito mais devagar.</summary>
		private const float SparkSteps = 20;
		private const float FieldSteps = 6;

		private float _sparkClock;
		private float _fieldClock;

		public override void _Process(double delta)
		{
			// O mapa parado (grade e fios) é desenhado uma vez; as camadas de cima andam a passos e só
			// desenham o que está na tela: o mapa é comprido e a maior parte dele fica fora da rolagem.
			_time += (float)delta;
			_sparkClock += (float)delta;
			_fieldClock += (float)delta;
			if (_sparkClock >= 1 / SparkSteps)
			{
				_sparkClock = 0;
				_sparks.QueueRedraw();
			}

			if (_fieldClock >= 1 / FieldSteps)
			{
				_fieldClock = 0;
				_dust.QueueRedraw();
			}
		}

		public override void _Notification(int what)
		{
			if (what == NotificationVisibilityChanged)
				SetProcess(IsVisibleInTree());
		}

		/// <summary>A grade e os fios, sem as faíscas: desenhados uma vez por tamanho do mapa.</summary>
		private void PaintChart(CanvasItem canvas)
		{
			if (_grid.Length > 0)
				canvas.DrawMultiline(_grid, GridInk, 1);

			for (var i = 0; i + 1 < _orbs.Count; i++)
			{
				var (number, a, _) = _orbs[i];
				var (from, to) = Span(i);
				if (number + 1 <= _cleared)
					Starlight.Thread(canvas, from, to, 0, 0, Palette.Spirit, Palette.Spirit, sparkle: false);
				else if (number + 1 == _cleared + 1)
					Starlight.Thread(canvas, from, to, 0, 0, Palette.Gold, Palette.Spirit, sparkle: false);
				else
					Starlight.Thread(canvas, from, to, 0, 0, Palette.GoldDark, Palette.Spirit, 0.6f, sparkle: false);
			}
		}

		/// <summary>As pontas do fio entre o orbe <paramref name="index"/> e o seguinte, deixando a folga dos orbes.</summary>
		private (Vector2 From, Vector2 To) Span(int index)
		{
			var a = _orbs[index].Node;
			var b = _orbs[index + 1].Node;
			var from = Center(a);
			var to = Center(b);
			var direction = (to - from).Normalized();
			return (from + direction * (a.Size.X / 2 + 4), to - direction * (b.Size.X / 2 + 4));
		}

		/// <summary>O pedaço do mapa que aparece na tela agora (com folga), em coordenadas do mapa.</summary>
		private Rect2 OnScreen() => (GetGlobalTransformWithCanvas().AffineInverse() * GetViewportRect()).Grow(48);

		/// <summary>As faíscas dos fios vencidos e do que leva à próxima, e a estrela que pisca nos orbes sem ícone.</summary>
		private void PaintSparks(CanvasItem canvas)
		{
			var screen = OnScreen();
			for (var i = 0; i + 1 < _orbs.Count; i++)
			{
				var number = _orbs[i].Number;
				if (number + 1 > _cleared + 1)
					continue;

				var (from, to) = Span(i);
				if (!screen.Intersects(new Rect2(from.Min(to), (to - from).Abs())))
					continue;
				Starlight.Spark(canvas, from, to, _time, i * 0.37f, Palette.Spirit);
			}

			foreach (var (node, phase) in _twinklers)
			{
				var at = Center(node);
				if (!screen.HasPoint(at))
					continue;

				var radius = Mathf.Min(node.Size.X, node.Size.Y) / 2 - 6;
				var tint = Palette.Gold with { A = node.Modulate.A };
				if (Art.IconInk("polar_star") is { } star)
					Starlight.Twinkle(canvas, star, at, radius, _time, phase, tint);
				else
					Starlight.Twinkle(canvas, at, radius, _time, phase, tint);
			}
		}

		/// <summary>O campo de estrelas de fundo, que respira devagar: só as que estão na tela.</summary>
		private void PaintDust(CanvasItem canvas)
		{
			var screen = OnScreen();
			foreach (var (spot, size, phase) in _field)
			{
				var at = new Vector2(spot.X * Size.X, spot.Y * Size.Y);
				if (!screen.HasPoint(at))
					continue;

				var light = 0.25f + 0.2f * Mathf.Sin(_time * 0.8f + phase);
				canvas.DrawCircle(at, size, new Color(Palette.Text, light));
			}
		}

		/// <summary>Uma camada de desenho do mapa: o mapa diz o que pintar nela e quando refazer.</summary>
		private sealed partial class Layer : Control
		{
			private readonly Action<CanvasItem> _paint;

			public Layer(string name, Action<CanvasItem> paint)
			{
				Name = name;
				_paint = paint;
				MouseFilter = MouseFilterEnum.Ignore;
				SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			}

			public override void _Draw() => _paint(this);
		}

		private static Vector2 Center(Control control) => control.Position + control.Size / 2;

		/// <summary>A grade do céu no tamanho do mapa, refeita só quando ele muda (pares de pontos, para um traço só).</summary>
		private static Vector2[] Grid(Vector2 size)
		{
			if (size.X <= 0 || size.Y <= 0)
				return [];
			var step = size.X / Hours;
			var lines = new List<Vector2>();
			for (var x = step; x < size.X - 1; x += step)
			{
				lines.Add(new Vector2(x, 0));
				lines.Add(new Vector2(x, size.Y));
			}
			for (var y = step; y < size.Y - 1; y += step)
			{
				lines.Add(new Vector2(0, y));
				lines.Add(new Vector2(size.X, y));
			}
			return lines.ToArray();
		}

		private void Arrange()
		{
			UpdateMinimumSize();
			var zoom = Zoom;
			foreach (var (_, node, spot) in _orbs)
			{
				node.Size = node.CustomMinimumSize;
				node.Position = spot * zoom - node.Size / 2;
			}

			_grid = Grid(Size);
			_chart.QueueRedraw();
			_dust.QueueRedraw();
			_sparks.QueueRedraw();
		}
	}
}
