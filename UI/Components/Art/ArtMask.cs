using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>O contorno da máscara: círculo, losango ou retângulo de cantos redondos.</summary>
	public enum MaskShape
	{
		Circle,
		Diamond,
		Rounded,
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
				default:
					var box = new StyleBoxFlat { BgColor = white, AntiAliasing = true };
					box.SetCornerRadiusAll((int)_radius);
					DrawStyleBox(box, new Rect2(Vector2.Zero, Size));
					break;
			}
		}
	}
}
