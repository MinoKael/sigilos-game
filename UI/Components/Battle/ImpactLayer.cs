using System.Collections.Generic;
using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Os respingos dos golpes, por cima das unidades: um clarão, um anel que se abre e riscos saindo do
	/// ponto atingido. Vários ao mesmo tempo num golpe em área, e cada
	/// um no seu alvo, para ficar claro quem foi atingido. Cada um dura o tempo do golpe na velocidade da
	/// luta, e para junto com a pausa (o tempo corre no <c>_Process</c>).
	/// </summary>
	public partial class ImpactLayer : Control
	{
		private const int Streaks = 8;

		private readonly List<Impact> _impacts = new();

		public ImpactLayer()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			ZIndex = 20;
			SetProcess(false);
		}

		/// <summary>Um respingo em <paramref name="at"/> (coordenadas desta camada), do tamanho <paramref name="size"/>, que dura <paramref name="life"/> segundos.</summary>
		public void Splash(Vector2 at, Color color, float size, float life)
		{
			_impacts.Add(new Impact(at, color, size, GD.Randf() * Mathf.Tau, Mathf.Max(0.12f, life)));
			SetProcess(true);
		}

		public override void _Process(double delta)
		{
			for (var i = _impacts.Count - 1; i >= 0; i--)
			{
				_impacts[i].Age += (float)delta;
				if (_impacts[i].Age >= _impacts[i].Life)
					_impacts.RemoveAt(i);
			}

			SetProcess(_impacts.Count > 0);
			QueueRedraw();
		}

		public override void _Draw()
		{
			foreach (var impact in _impacts)
			{
				var t = impact.Age / impact.Life;
				var open = 1 - (1 - t) * (1 - t);
				var fade = 1 - t;
				var radius = impact.Size * (0.35f + 0.65f * open);

				DrawCircle(impact.At, radius * 0.9f, new Color(impact.Color, 0.22f * fade));
				DrawCircle(impact.At, impact.Size * 0.28f * (1 - t), new Color(Colors.White, 0.8f * fade));
				DrawArc(impact.At, radius, 0, Mathf.Tau, 32, new Color(impact.Color, 0.9f * fade), 3, true);
				for (var k = 0; k < Streaks; k++)
				{
					var angle = impact.Turn + k * Mathf.Tau / Streaks;
					var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
					DrawLine(impact.At + direction * radius * 0.55f, impact.At + direction * radius * 1.15f, new Color(impact.Color.Lightened(0.3f), fade), 2, true);
				}
			}
		}

		private sealed class Impact
		{
			public Impact(Vector2 at, Color color, float size, float turn, float life)
			{
				At = at;
				Color = color;
				Size = size;
				Turn = turn;
				Life = life;
			}

			public Vector2 At { get; }
			public Color Color { get; }
			public float Size { get; }
			public float Turn { get; }
			public float Life { get; }
			public float Age { get; set; }
		}
	}
}
