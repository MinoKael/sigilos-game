using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O campo da luta: um círculo de conjuração oval no chão, com os aliados no arco de baixo à esquerda
	/// e os inimigos no arco de cima à direita, frente a frente pela diagonal.
	///
	/// Quem ataca corre até o alvo e para na frente dele (<see cref="Approach"/>), dá um tranco a cada golpe
	/// (<see cref="Bump"/>) e volta ao seu lugar (<see cref="Return"/>), como em Summoners War; num golpe em
	/// área corre até o meio do grupo. Cada alvo atingido ganha um respingo (<see cref="Splash"/>), todos
	/// juntos quando o golpe é em área.
	///
	/// As unidades são filhas diretas, em posição absoluta. <see cref="Arrange"/> as recoloca a cada
	/// mudança de tamanho: o espaço entre vizinhas é medido ao longo da elipse, e o grupo fica centrado no
	/// seu arco, tenha ele 1 ou 5 unidades. Numa onda com chefe, ele fica no meio do arco (o cartão dele é
	/// maior) e os outros em volta, um de cada lado, com mais folga junto dele; quem ficaria com os efeitos
	/// embaixo da barra do chefe (<see cref="Ceiling"/>) desce o bastante para eles aparecerem.
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

		/// <summary>A folga a mais (em passos) entre o chefe e quem fica ao lado dele.</summary>
		private const float BossRoom = 0.3f;

		private readonly List<Control> _allies = new();
		private readonly List<Control> _enemies = new();
		private Control? _boss;
		private readonly Dictionary<Control, Vector2> _homes = new();
		private readonly ImpactLayer _impacts = new() { Name = "Impacts" };

		public BattleArena()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			_impacts.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(_impacts);
			Resized += Arrange;
		}

		/// <summary>
		/// A altura, no campo, de onde termina a barra do chefe (zero sem ela): nenhum inimigo fica com a
		/// fileira de efeitos acima dela (<see cref="UnitView.Headroom"/>). Vale a partir do próximo
		/// <see cref="SetEnemies"/>, para ninguém pular de lugar no meio de um golpe.
		/// </summary>
		public float Ceiling { get; set; }

		/// <summary>O centro do oval, um pouco abaixo do meio.</summary>
		public Vector2 Middle => new(Size.X * 0.5f, Size.Y * 0.52f);

		private Vector2 Radii => new(Mathf.Min(Size.X * 0.34f, 430), Mathf.Min(Size.Y * 0.36f, 260));

		public void SetAllies(IReadOnlyList<Control> allies) => Replace(_allies, allies);

		/// <summary>Troca os inimigos (a cada onda); os da onda anterior saem. <paramref name="boss"/> fica no meio.</summary>
		public void SetEnemies(IReadOnlyList<Control> enemies, Control? boss = null)
		{
			_boss = boss;
			Replace(_enemies, enemies);
		}

		/// <summary>
		/// Corre até os alvos em <paramref name="seconds"/>: para na frente de um só, do lado de onde veio, ou
		/// na frente do meio do grupo num golpe em área. Sem alvo (cura, reforço), dá um passo para o centro.
		/// </summary>
		public void Approach(Control actor, IReadOnlyList<Control> targets, double seconds)
		{
			if (!_homes.ContainsKey(actor))
				return;

			var from = CenterOf(actor);
			Vector2 destination;
			if (targets.Count == 0)
			{
				destination = from + (Middle - from).Normalized() * 40;
			}
			else
			{
				var spot = targets.Aggregate(Vector2.Zero, (sum, target) => sum + CenterOf(target)) / targets.Count;
				var direction = (from - spot).Normalized();
				// Encosta no alvo sem cobrir o cartão: anda até os dois retângulos se tocarem (um pouco
				// antes, no meio de um grupo, para não pisar em ninguém). O cartão do chefe é maior.
				var extent = targets.Count == 1 ? (actor.Size + targets[0].Size) / 2 : actor.Size;
				var reach = Mathf.Min(
					Mathf.Abs(direction.X) > 0.01f ? extent.X / Mathf.Abs(direction.X) : float.MaxValue,
					Mathf.Abs(direction.Y) > 0.01f ? extent.Y / Mathf.Abs(direction.Y) : float.MaxValue);
				destination = spot + direction * (reach * (targets.Count == 1 ? 1f : 1.35f) + 6);
			}

			actor.ZIndex = 5;
			var tween = actor.CreateTween();
			tween.TweenProperty(actor, "position", destination - actor.Size / 2, seconds * 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}

		/// <summary>O tranco de um golpe: um passo curto na direção do alvo e de volta.</summary>
		public void Bump(Control actor, Control target, double seconds)
		{
			var direction = (CenterOf(target) - CenterOf(actor)).Normalized();
			var start = actor.Position;
			var tween = actor.CreateTween();
			tween.TweenProperty(actor, "position", start + direction * 16, seconds * 0.35).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(actor, "position", start, seconds * 0.4).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		}

		/// <summary>Volta ao seu lugar no arco em <paramref name="seconds"/>.</summary>
		public void Return(Control actor, double seconds)
		{
			if (!_homes.TryGetValue(actor, out var home))
				return;

			var tween = actor.CreateTween();
			tween.TweenProperty(actor, "position", home, seconds * 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.TweenCallback(Callable.From(() => actor.ZIndex = 0));
		}

		/// <summary>
		/// O respingo de um golpe no meio de <paramref name="target"/>, que some em <paramref name="seconds"/>
		/// (o tempo do golpe na velocidade da luta); <paramref name="strong"/> num crítico.
		/// </summary>
		public void Splash(Control target, Color color, double seconds, bool strong = false) =>
			_impacts.Splash(CenterOf(target), color, target.Size.X * (strong ? 0.75f : 0.55f), (float)seconds);

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

			// A diagonal do confronto, de um grupo ao outro, e o círculo do centro.
			DrawLine(Point((AllyFrom + AllyTo) / 2), Point((EnemyFrom + EnemyTo) / 2), new Color(Palette.Gold, 0.14f), 1.5f, true);
			DrawArc(middle, radii.Y * 0.28f, 0, Mathf.Tau, 64, new Color(Palette.Gold, 0.22f), 1.2f, true);
		}

		private static Vector2 CenterOf(Control control) => control.Position + control.Size / 2;

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
			Place(_enemies, EnemyFrom, EnemyTo, _boss, Ceiling);
			QueueRedraw();
		}

		/// <summary>
		/// Põe o grupo no arco: vizinhas à mesma distância ao longo da elipse, o grupo no meio do arco. Com
		/// <paramref name="center"/> (o chefe), ele fica no meio e os outros se alternam dos dois lados.
		/// Quem ficaria com os efeitos acima de <paramref name="ceiling"/> desce até eles caberem.
		/// </summary>
		private void Place(List<Control> group, float from, float to, Control? center = null, float ceiling = 0)
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
			var spots = new List<(Control Unit, float At)>();
			if (center != null && group.Contains(center))
			{
				// O chefe no meio; os outros em anéis dos dois lados. O primeiro anel tem folga a mais para
				// o cartão maior, e o passo encolhe se os anéis não couberem no arco.
				var others = group.Where(unit => unit != center).ToList();
				var rings = (others.Count + 1) / 2;
				var gap = rings == 0 ? step : Mathf.Min(step, total / 2 / (rings + BossRoom));
				spots.Add((center, total / 2));
				for (var i = 0; i < others.Count; i++)
				{
					var side = i % 2 == 0 ? 1 : -1;
					spots.Add((others[i], total / 2 + side * gap * (i / 2 + 1 + BossRoom)));
				}
			}
			else
			{
				var start = (total - step * (group.Count - 1)) / 2;
				for (var k = 0; k < group.Count; k++)
					spots.Add((group[k], start + k * step));
			}

			foreach (var (unit, at) in spots)
			{
				var segment = 1;
				while (segment < Samples && lengths[segment] < at)
					segment++;
				var span = lengths[segment] - lengths[segment - 1];
				var point = points[segment - 1].Lerp(points[segment], span > 0 ? Mathf.Clamp((at - lengths[segment - 1]) / span, 0, 1) : 0);

				unit.Size = unit.CustomMinimumSize;
				unit.Position = point - unit.Size / 2;
				var headroom = unit is UnitView view ? view.Headroom : 0;
				if (unit.Position.Y - headroom < ceiling)
					unit.Position = new Vector2(unit.Position.X, ceiling + headroom);
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
	}
}
