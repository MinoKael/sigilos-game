using Godot;
using Sigilos.UI.Style;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Um texto em volta de um círculo, letra por letra, do alto para a direita (sentido horário). É o
	/// texto decorativo do símbolo de Transmutação ("sit aliquid boni venire"). Só desenho: não pega toque.
	/// </summary>
	public partial class RingText : Control
	{
		private readonly string _text;
		private readonly Font _font;
		private readonly int _size;
		private readonly Color _color;
		private readonly float _radius;

		/// <param name="radius">A distância das letras ao centro do controle, em pixels.</param>
		public RingText(string text, float radius, int size, Color color)
		{
			_text = text;
			_radius = radius;
			_size = size;
			_color = color;
			_font = GameTheme.Display;
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public override void _Draw()
		{
			var center = Size / 2;
			var count = _text.Length;
			if (count == 0)
				return;

			// A volta inteira, com as letras espaçadas por igual.
			var step = Mathf.Tau / count;
			for (var i = 0; i < count; i++)
			{
				var angle = -Mathf.Pi / 2 + step * i;
				var origin = center + Vector2.FromAngle(angle) * _radius;
				DrawSetTransform(origin, angle + Mathf.Pi / 2, Vector2.One);
				var letter = _text[i].ToString();
				var width = _font.GetStringSize(letter, HorizontalAlignment.Left, -1, _size).X;
				DrawString(_font, new Vector2(-width / 2, 0), letter, HorizontalAlignment.Left, -1, _size, _color);
			}

			DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		}
	}
}
