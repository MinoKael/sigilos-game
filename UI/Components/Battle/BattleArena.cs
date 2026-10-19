using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O campo da luta, em perspectiva: a câmera olha para o fundo. O nosso time fica embaixo, perto e maior; os inimigos, no alto, ao fundo e menores. Cada grupo está
	/// numa fileira em arco (o do meio mais ao fundo, as pontas mais perto, como a metade de trás de um oval) sobre o
	/// seu disco de conjuração, um oval deitado (o dos inimigos quase achatado, pela inclinação). Quem está mais ao
	/// fundo é menor (<see cref="DepthScale"/>) e quem está mais perto é desenhado por cima.
	///
	/// Os números de cada fileira e de cada disco são frações do campo (<see cref="Row"/>, <see cref="Disc"/>),
	/// tirados do desenho de referência do jogo.
	///
	/// Quem ataca corre até o alvo e para na frente dele (<see cref="Approach"/>), dá um tranco a cada golpe
	/// (<see cref="Bump"/>) e volta ao seu lugar (<see cref="Return"/>); num golpe em
	/// área corre até o meio do grupo, e o tamanho acompanha a profundidade no caminho. Cada alvo atingido
	/// ganha um respingo (<see cref="Splash"/>), todos juntos quando o golpe é em área.
	///
	/// As unidades são filhas diretas, em posição absoluta. <see cref="Arrange"/> as recoloca a cada
	/// mudança de tamanho: o grupo fica centrado na sua fileira, tenha ele 1 ou 5 unidades. Numa onda com
	/// chefe, ele fica no meio da fileira (o cartão dele é maior) e os outros em volta, um de cada lado, com
	/// mais folga junto dele; quem ficaria com os efeitos embaixo da barra do chefe (<see cref="Ceiling"/>)
	/// desce o bastante para eles aparecerem.
	/// </summary>
	public partial class BattleArena : Control
	{
		/// <summary>Vagas de cada fileira: o espaço entre vizinhas é o de um grupo cheio.</summary>
		private const int Slots = 5;

		private const int Samples = 64;

		/// <summary>A folga a mais (em passos) entre o chefe e quem fica ao lado dele.</summary>
		private const float BossRoom = 0.3f;

		/// <summary>
		/// Uma fileira em arco, em frações do campo: <paramref name="Apex"/> é o centro da unidade do meio (a mais ao
		/// fundo); a unidade da vaga <c>t</c> (de −1 a 1, de meio em meio) fica em
		/// <c>Apex + (HalfSpan·t, Sag·t² + Tilt·t)</c>, com <c>Sag</c> o quanto as pontas descem e <c>Tilt</c> o quanto a
		/// direita desce a mais que a esquerda.
		/// </summary>
		private readonly record struct Row(Vector2 Apex, float HalfSpan, float Sag, float Tilt);

		/// <summary>O disco sob um grupo, em frações do campo: o centro e os dois raios (sem giro).</summary>
		private readonly record struct Disc(Vector2 Center, float RadiusX, float RadiusY);

		private static readonly Row AllyRow = new(new Vector2(0.445f, 0.641f), 0.2754f, 0.0765f, 0.0087f);
		private static readonly Row EnemyRow = new(new Vector2(0.666f, 0.2728f), 0.1963f, 0.0548f, 0.0073f);
		private static readonly Disc AllyDisc = new(new Vector2(0.4398f, 0.7336f), 0.3733f, 0.0915f);
		private static readonly Disc EnemyDisc = new(new Vector2(0.6597f, 0.3481f), 0.2656f, 0.0436f);


		/// <summary>O tamanho de quem está no fundo (o inimigo mais alto) e de quem está bem perto.</summary>
		private const float FarScale = 0.72f;
		private const float NearScale = 1.12f;

		private readonly List<Control> _allies = new();
		private readonly List<Control> _enemies = new();
		private Control? _boss;
		private readonly Dictionary<Control, Vector2> _homes = new();
		private readonly Dictionary<Control, float> _homeScales = new();
		private readonly ImpactLayer _impacts = new() { Name = "Impacts", ZIndex = 0 };

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

		private Vector2 Frac(Vector2 fraction) => new(Size.X * fraction.X, Size.Y * fraction.Y);

		/// <summary>O meio do campo, entre os dois grupos: onde o texto do que acontece e o painel de Efeitos ficam.</summary>
		public Vector2 Middle => (Frac(AllyRow.Apex) + Frac(EnemyRow.Apex)) / 2;

		/// <summary>O tamanho de uma unidade pela altura do centro dela no campo: mais alto, mais ao fundo, menor.</summary>
		public float DepthScale(float y) => Mathf.Lerp(FarScale, NearScale, Mathf.Clamp((y / Mathf.Max(1, Size.Y) - 0.25f) / 0.53f, 0, 1));

		public void SetAllies(IReadOnlyList<Control> allies) => Replace(_allies, allies);

		/// <summary>Troca os inimigos (a cada onda); os da onda anterior saem. <paramref name="boss"/> fica no meio.</summary>
		public void SetEnemies(IReadOnlyList<Control> enemies, Control? boss = null)
		{
			_boss = boss;
			Replace(_enemies, enemies);
		}

		/// <summary>
		/// Corre até os alvos em <paramref name="seconds"/>: para na frente de um só, do lado de onde veio, ou
		/// na frente do meio do grupo num golpe em área. Sem alvo (cura, reforço), dá um passo para o centro. O
		/// tamanho muda no caminho, para o da profundidade onde ele para.
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
				// antes, no meio de um grupo, para não pisar em ninguém). O cartão do chefe é maior; o que vale é o
				// tamanho que se vê, com a escala.
				var extent = targets.Count == 1 ? (Seen(actor) + Seen(targets[0])) / 2 : Seen(actor);
				var reach = Mathf.Min(
					Mathf.Abs(direction.X) > 0.01f ? extent.X / Mathf.Abs(direction.X) : float.MaxValue,
					Mathf.Abs(direction.Y) > 0.01f ? extent.Y / Mathf.Abs(direction.Y) : float.MaxValue);
				destination = spot + direction * (reach * (targets.Count == 1 ? 1f : 1.35f) + 6);
			}

			actor.ZIndex = 5;
			var tween = actor.CreateTween().SetParallel();
			tween.TweenProperty(actor, "position", destination - actor.Size / 2, seconds * 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.TweenProperty(actor, "scale", Vector2.One * DepthScale(destination.Y), seconds * 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
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

		/// <summary>Volta ao seu lugar na fileira em <paramref name="seconds"/>, com o tamanho de lá.</summary>
		public void Return(Control actor, double seconds)
		{
			if (!_homes.TryGetValue(actor, out var home))
				return;

			var tween = actor.CreateTween().SetParallel();
			tween.TweenProperty(actor, "position", home, seconds * 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.TweenProperty(actor, "scale", Vector2.One * _homeScales.GetValueOrDefault(actor, 1f), seconds * 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.Chain().TweenCallback(Callable.From(() => actor.ZIndex = 0));
		}

		/// <summary>
		/// O respingo de um golpe no meio de <paramref name="target"/>, que some em <paramref name="seconds"/>
		/// (o tempo do golpe na velocidade da luta); <paramref name="strong"/> num crítico.
		/// </summary>
		public void Splash(Control target, Color color, double seconds, bool strong = false) =>
			_impacts.Splash(CenterOf(target), color, Seen(target).X * (strong ? 0.75f : 0.55f), (float)seconds);

		public override void _Draw()
		{
			DrawDisc(AllyDisc);
			DrawDisc(EnemyDisc);
		}

		/// <summary>
		/// O disco de um grupo: um oval deitado, com a borda da frente (a de baixo) mais grossa, como quem olha de cima e de
		/// longe; o anel de dentro e as marcas de runa.
		/// </summary>
		private void DrawDisc(Disc disc)
		{
			var radii = new Vector2(Size.X * disc.RadiusX, Size.Y * disc.RadiusY);
			DrawSetTransform(Frac(disc.Center), 0, Vector2.One);
			var ring = Ellipse(radii);
			DrawColoredPolygon(ring[..^1], new Color(Palette.Inset, 0.35f));
			DrawPolyline(ring, new Color(0, 0, 0, 0.5f), 4, true);
			DrawPolyline(ring, Palette.GoldDark, 1.5f, true);
			// A metade da frente (de 0 a 180 graus, para baixo) em traço grosso.
			DrawPolyline(ring[..(Samples / 2 + 1)], Palette.GoldDark, 2f + Mathf.Min(6, radii.Y * 0.12f), true);
			var inner = radii - new Vector2(14, 14 * radii.Y / radii.X);
			DrawPolyline(Ellipse(inner), new Color(Palette.Gold, 0.4f), 1.2f, true);
			for (var k = 0; k < 40; k++)
			{
				var angle = k * Mathf.Tau / 40;
				var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				var length = k % 4 == 0 ? 8 : 4;
				DrawLine(inner * direction, (inner - new Vector2(length, length * radii.Y / radii.X)) * direction, new Color(Palette.Gold, 0.45f), 1f, true);
			}

			DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		}

		private static Vector2 CenterOf(Control control) => control.Position + control.Size / 2;

		/// <summary>O tamanho que o cartão tem na tela, com a escala da profundidade.</summary>
		private static Vector2 Seen(Control control) => control.Size * control.Scale;

		private void Replace(List<Control> group, IReadOnlyList<Control> units)
		{
			foreach (var old in group)
			{
				_homes.Remove(old);
				_homeScales.Remove(old);
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
			Place(_allies, AllyRow);
			Place(_enemies, EnemyRow, _boss, Ceiling);
			//Sort();
			QueueRedraw();
		}

		/// <summary>Quem está mais perto (mais embaixo) é desenhado por cima de quem está atrás; os respingos ficam sobre todos.</summary>
		private void Sort()
		{
			var depth = 0;
			foreach (var unit in _allies.Concat(_enemies).OrderBy(unit => CenterOf(unit).Y))
				MoveChild(unit, depth++);
			MoveChild(_impacts, -1);
		}

		/// <summary>
		/// Põe o grupo na fileira em arco: vizinhos a meio passo (<c>t</c> de −1 a 1 em 5 vagas), o grupo no meio da fileira.
		/// Com <paramref name="boss"/>, ele fica no meio (<c>t = 0</c>) e os outros se alternam dos dois lados. Cada um ganha a
		/// escala da altura onde fica. Quem ficaria com os efeitos acima de <paramref name="ceiling"/> desce até eles caberem.
		/// </summary>
		private void Place(List<Control> group, Row row, Control? boss = null, float ceiling = 0)
		{
			if (group.Count == 0 || Size.X <= 0)
				return;

			const float step = 2f / (Slots - 1);
			var spots = new List<(Control Unit, float T)>();
			if (boss != null && group.Contains(boss))
			{
				// O chefe no meio; os outros em anéis dos dois lados. O primeiro anel tem folga a mais para
				// o cartão maior, e o passo encolhe se os anéis não couberem na fileira.
				var others = group.Where(unit => unit != boss).ToList();
				var rings = (others.Count + 1) / 2;
				var gap = rings == 0 ? step : Mathf.Min(step, 1f / (rings + BossRoom));
				spots.Add((boss, 0));
				for (var i = 0; i < others.Count; i++)
				{
					var side = i % 2 == 0 ? 1 : -1;
					spots.Add((others[i], side * gap * (i / 2 + 1 + BossRoom)));
				}
			}
			else
			{
				var start = -step * (group.Count - 1) / 2;
				for (var k = 0; k < group.Count; k++)
					spots.Add((group[k], start + k * step));
			}

			foreach (var (unit, t) in spots)
			{
				var point = Frac(row.Apex) + new Vector2(Size.X * row.HalfSpan * t, Size.Y * (row.Sag * t * t + row.Tilt * t));
				var scale = DepthScale(point.Y);
				unit.Size = unit.CustomMinimumSize;
				unit.Scale = Vector2.One * scale;
				if (unit is UnitView view)
					view.SetDepth(scale);
				unit.Position = point - unit.Size / 2;
				var headroom = unit is UnitView card ? card.Headroom : 0;
				// O topo que se vê: o centro menos meia altura com a escala, e a fileira dos efeitos acima.
				var top = point.Y - unit.Size.Y * scale / 2 - headroom;
				if (top < ceiling)
					unit.Position += new Vector2(0, ceiling - top);
				_homes[unit] = unit.Position;
				_homeScales[unit] = scale;
			}
		}

		private static Vector2[] Ellipse(Vector2 radii)
		{
			var points = new Vector2[Samples + 1];
			for (var i = 0; i <= Samples; i++)
			{
				var angle = i * Mathf.Tau / Samples;
				points[i] = new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
			}

			return points;
		}
	}
}
