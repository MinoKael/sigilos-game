using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>O contorno da máscara: círculo, losango, retângulo de cantos redondos ou pentágono regular.</summary>
	public enum MaskShape
	{
		Circle,
		Diamond,
		Rounded,
		Pentagon,
	}

	/// <summary>
	/// Máscara de forma: desenha o contorno só como máscara (<see cref="CanvasItem.ClipChildrenMode.Only"/>)
	/// e o que estiver dentro — o desenho, o símbolo — é recortado nela. Com o desenho em "contain"
	/// (<see cref="Doodle"/> mantém a proporção e cabe inteiro), nada vaza do componente: nem o traço
	/// tremido, nem o crescer do botão, nem a arte grande de um cartão.
	/// </summary>
	public partial class ArtMask : Control
	{
		private readonly MaskShape _shape;
		private readonly float _radius;

		public ArtMask(MaskShape shape, float radius = 8)
		{
			_shape = shape;
			_radius = radius;
			ClipChildren = ClipChildrenMode.Only;
			MouseFilter = MouseFilterEnum.Ignore;
			Resized += QueueRedraw;
		}

		/// <summary>Põe <paramref name="content"/> dentro de uma máscara que ocupa o pai inteiro, com <paramref name="inset"/> de folga até a borda.</summary>
		public static ArtMask Of(Control content, MaskShape shape, float radius = 8, float inset = 0)
		{
			var mask = new ArtMask(shape, radius) { Name = "Mask" };
			mask.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			mask.OffsetLeft = mask.OffsetTop = inset;
			mask.OffsetRight = mask.OffsetBottom = -inset;
			content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			mask.AddChild(content);
			return mask;
		}

		/// <summary>
		/// Os cinco vértices de um pentágono regular de ponta para cima, dentro do quadrado de lado
		/// 2×<paramref name="radius"/> centrado em <paramref name="center"/>: o miolo do polígono desce um pouco
		/// (a ponta de cima vai ao topo do quadrado, e a base fica acima do fundo).
		/// </summary>
		public static Vector2[] Pentagon(Vector2 center, float radius)
		{
			var points = new Vector2[5];
			var lift = radius * (1 - Mathf.Cos(Mathf.Pi / 5)) / 2;
			for (var k = 0; k < 5; k++)
			{
				var angle = -Mathf.Pi / 2 + k * Mathf.Tau / 5;
				points[k] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius + new Vector2(0, lift);
			}

			return points;
		}

		public override void _Draw()
		{
			var white = Colors.White;
			switch (_shape)
			{
				case MaskShape.Circle:
					DrawCircle(Size / 2, Mathf.Min(Size.X, Size.Y) / 2, white);
					break;
				case MaskShape.Diamond:
					var c = Size / 2;
					var r = Mathf.Min(Size.X, Size.Y) / 2;
					DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) }, white);
					break;
				case MaskShape.Pentagon:
					DrawColoredPolygon(Pentagon(Size / 2, Mathf.Min(Size.X, Size.Y) / 2), white);
					break;
				default:
					var box = new StyleBoxFlat { BgColor = white, AntiAliasing = true };
					box.SetCornerRadiusAll((int)_radius);
					DrawStyleBox(box, new Rect2(Vector2.Zero, Size));
					break;
			}
		}
	}
}
