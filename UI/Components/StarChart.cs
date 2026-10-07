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
					node.AddChild(new Twinkler((number % 7) * 0.9f) { Name = "Star" });
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

		public override void _Process(double delta)
		{
			_time += (float)delta;
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (_grid.Length > 0)
				DrawMultiline(_grid, GridInk, 1);

			foreach (var (spot, size, phase) in _field)
			{
				var light = 0.25f + 0.2f * Mathf.Sin(_time * 0.8f + phase);
				DrawCircle(new Vector2(spot.X * Size.X, spot.Y * Size.Y), size, new Color(Palette.Text, light));
			}

			for (var i = 0; i + 1 < _orbs.Count; i++)
			{
				var (number, a, _) = _orbs[i];
				var b = _orbs[i + 1].Node;
				var from = Center(a);
				var to = Center(b);
				var direction = (to - from).Normalized();
				from += direction * (a.Size.X / 2 + 4);
				to -= direction * (b.Size.X / 2 + 4);

				// O trecho vencido no mês é verde e vivo; o que leva à próxima, dourado; o resto, apagado.
				if (number + 1 <= _cleared)
					Starlight.Thread(this, from, to, _time, i * 0.37f, Palette.Spirit, Palette.Spirit);
				else if (number + 1 == _cleared + 1)
					Starlight.Thread(this, from, to, _time, i * 0.37f, Palette.Gold, Palette.Spirit);
				else
					Starlight.Thread(this, from, to, _time, 0, Palette.GoldDark, Palette.Spirit, 0.6f, sparkle: false);
			}
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
			QueueRedraw();
		}

		/// <summary>
		/// A estrela que pisca no orbe sem ícone, como os orbes da Canalização, desenhada com a estrela polar
		/// (<c>polar_star</c>) em vez do polígono, que serrilha nesse tamanho.
		/// </summary>
		private sealed partial class Twinkler : Control
		{
			private readonly float _phase;
			private float _time;

			public Twinkler(float phase)
			{
				_phase = phase;
				MouseFilter = MouseFilterEnum.Ignore;
				SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			}

			public override void _Process(double delta)
			{
				_time += (float)delta;
				QueueRedraw();
			}

			public override void _Draw()
			{
				var radius = Mathf.Min(Size.X, Size.Y) / 2 - 6;
				if (Art.IconInk("polar_star") is { } star)
					Starlight.Twinkle(this, star, Size / 2, radius, _time, _phase, Palette.Gold);
				else
					Starlight.Twinkle(this, Size / 2, radius, _time, _phase, Palette.Gold);
			}
		}
	}
}
