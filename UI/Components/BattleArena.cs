using System.Collections.Generic;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O campo da luta: um círculo de conjuração oval no chão, com os aliados no arco de baixo à esquerda
	/// e os inimigos no arco de cima à direita, frente a frente pela diagonal. Quem age avança para o
	/// centro, e uma seta risca o chão até o outro lado (<see cref="Strike"/>).
	///
	/// As unidades são filhas diretas, em posição absoluta. <see cref="Arrange"/> as recoloca a cada
	/// mudança de tamanho: o espaço entre vizinhas é medido ao longo da elipse, e o grupo fica centrado no
	/// seu arco, tenha ele 1 ou 5 unidades.
	/// </summary>
	public partial class BattleArena : Control
	{
		// Os arcos de cada lado, em graus (0 = direita, 90 = embaixo).
		private const float AllyFrom = 190;
		private const float AllyTo = 80;
		private const float EnemyFrom = -100;
		private const float EnemyTo = 10;

		/// <summary>Vagas de cada arco: o espaço entre vizinhas é o de um grupo cheio.</summary>
		private const int Slots = 5;

		private const int Samples = 64;

		private readonly List<Control> _allies = new();
		private readonly List<Control> _enemies = new();
		private readonly Dictionary<Control, Vector2> _homes = new();
		private Vector2 _arrowFrom;
		private Vector2 _arrowTo;
		private float _arrow;

		public BattleArena()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			Resized += Arrange;
		}

		/// <summary>O centro do oval, um pouco abaixo do meio.</summary>
		public Vector2 Middle => new(Size.X * 0.5f, Size.Y * 0.52f);

		private Vector2 Radii => new(Mathf.Min(Size.X * 0.34f, 430), Mathf.Min(Size.Y * 0.36f, 260));

		public void SetAllies(IReadOnlyList<Control> allies) => Replace(_allies, allies);

		/// <summary>Troca os inimigos (a cada onda); os da onda anterior saem.</summary>
		public void SetEnemies(IReadOnlyList<Control> enemies) => Replace(_enemies, enemies);

		/// <summary>
		/// Quem age avança um terço do caminho até o centro enquanto a seta risca o chão até o outro lado,
		/// e volta ao seu lugar. <paramref name="speed"/> é o multiplicador de velocidade da luta.
		/// </summary>
		public void Strike(Control unit, float speed)
		{
			if (!_homes.TryGetValue(unit, out var home))
				return;

			var middle = Middle;
			var center = home + unit.Size / 2;
			var step = (middle - center) * 0.35f;
			var other = Point(_allies.Contains(unit) ? (EnemyFrom + EnemyTo) / 2 : (AllyFrom + AllyTo) / 2);
			_arrowFrom = center + step;
			_arrowTo = other + (middle - other) * 0.3f;

			unit.ZIndex = 5;
			var tween = CreateTween();
			tween.TweenProperty(unit, "position", home + step, 0.14 / speed).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tween.Parallel().TweenMethod(Callable.From<float>(SetArrow), 0f, 1f, 0.14 / speed);
			tween.TweenInterval(0.16 / speed);
			tween.TweenProperty(unit, "position", home, 0.2 / speed);
			tween.Parallel().TweenMethod(Callable.From<float>(SetArrow), 1f, 0f, 0.3 / speed);
			tween.TweenCallback(Callable.From(() =>
			{
				if (IsInstanceValid(unit))
					unit.ZIndex = 0;
			}));
		}

		public override void _Draw()
		{
			var middle = Middle;
			var radii = Radii;
			var ring = Ellipse(middle, radii);

			// O chão: o oval escurecido, o sulco, dois anéis de ouro e as marcas de runa entre eles.
			DrawColoredPolygon(ring[..^1], new Color(Palette.Inset, 0.35f));
			DrawPolyline(ring, new Color(0, 0, 0, 0.5f), 8, true);
			DrawPolyline(ring, Palette.GoldDark, 2, true);
			DrawPolyline(Ellipse(middle, radii - new Vector2(16, 16)), new Color(Palette.Gold, 0.4f), 1.5f, true);
			for (var k = 0; k < 48; k++)
			{
				var angle = k * Mathf.Tau / 48;
				var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				var length = k % 4 == 0 ? 10 : 5;
				DrawLine(middle + direction * (radii - new Vector2(15, 15)), middle + direction * (radii - new Vector2(15 - length, 15 - length)), new Color(Palette.Gold, 0.45f), 1.2f, true);
			}

			// A diagonal do confronto, de um grupo ao outro, e o círculo do centro onde quem age pisa.
			DrawLine(Point((AllyFrom + AllyTo) / 2), Point((EnemyFrom + EnemyTo) / 2), new Color(Palette.Gold, 0.14f), 1.5f, true);
			DrawArc(middle, radii.Y * 0.28f, 0, Mathf.Tau, 64, new Color(Palette.Gold, 0.22f), 1.2f, true);

			if (_arrow <= 0)
				return;

			var ink = new Color(Palette.Arcane, 0.85f * _arrow);
			var tip = _arrowFrom.Lerp(_arrowTo, _arrow);
			var forward = (_arrowTo - _arrowFrom).Normalized();
			var side = new Vector2(-forward.Y, forward.X);
			DrawLine(_arrowFrom, tip, new Color(Palette.Arcane, 0.2f * _arrow), 10, true);
			DrawLine(_arrowFrom, tip, ink, 3, true);
			DrawColoredPolygon(new[] { tip + forward * 14, tip - forward * 6 + side * 9, tip - forward * 6 - side * 9 }, ink);
		}

		private void Replace(List<Control> group, IReadOnlyList<Control> units)
		{
			foreach (var old in group)
			{
				_homes.Remove(old);
				if (IsInstanceValid(old))
					Layout.Discard(old);
			}

			group.Clear();
			foreach (var unit in units)
			{
				group.Add(unit);
				AddChild(unit);
			}

			Arrange();
		}

		private void Arrange()
		{
			Place(_allies, AllyFrom, AllyTo);
			Place(_enemies, EnemyFrom, EnemyTo);
			QueueRedraw();
		}

		/// <summary>Põe o grupo no arco: vizinhas à mesma distância ao longo da elipse, o grupo no meio do arco.</summary>
		private void Place(List<Control> group, float from, float to)
		{
			if (group.Count == 0 || Size.X <= 0)
				return;

			// A elipse não tem fórmula simples de comprimento: soma a distância entre pontos amostrados.
			var points = new Vector2[Samples + 1];
			var lengths = new float[Samples + 1];
			for (var i = 0; i <= Samples; i++)
			{
				points[i] = Point(Mathf.Lerp(from, to, i / (float)Samples));
				if (i > 0)
					lengths[i] = lengths[i - 1] + points[i].DistanceTo(points[i - 1]);
			}

			var total = lengths[Samples];
			var step = total / (Mathf.Max(Slots, group.Count) - 1);
			var start = (total - step * (group.Count - 1)) / 2;
			var segment = 1;
			for (var k = 0; k < group.Count; k++)
			{
				var target = start + k * step;
				while (segment < Samples && lengths[segment] < target)
					segment++;
				var span = lengths[segment] - lengths[segment - 1];
				var point = points[segment - 1].Lerp(points[segment], span > 0 ? (target - lengths[segment - 1]) / span : 0);

				var unit = group[k];
				unit.Size = unit.CustomMinimumSize;
				unit.Position = point - unit.Size / 2;
				_homes[unit] = unit.Position;
			}
		}

		/// <summary>O ponto da borda do oval no ângulo dado, em graus.</summary>
		private Vector2 Point(float degrees)
		{
			var angle = Mathf.DegToRad(degrees);
			var radii = Radii;
			return Middle + new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
		}

		private static Vector2[] Ellipse(Vector2 middle, Vector2 radii)
		{
			var points = new Vector2[Samples + 1];
			for (var i = 0; i <= Samples; i++)
			{
				var angle = i * Mathf.Tau / Samples;
				points[i] = middle + new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
			}

			return points;
		}

		private void SetArrow(float amount)
		{
			_arrow = amount;
			QueueRedraw();
		}
	}
}
