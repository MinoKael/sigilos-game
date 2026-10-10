using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// A moldura de um cartão que se escolhe e se marca (o monstro, a runa): pinta a caixa dele conforme o
	/// estado. Escolhido acende em azul com aura e a borda engrossa 1 px; marcado esverdeia o fundo; sob o
	/// mouse a moldura clareia. O cartão guarda os estados e chama <see cref="Apply"/> quando um deles muda.
	///
	/// O que muda de um cartão para outro é a sombra fora da escolha (<see cref="Rest"/> e <see cref="Hover"/>).
	/// </summary>
	public sealed class SelectionFrame
	{
		private readonly StyleBoxFlat _box;

		/// <param name="box">A caixa do cartão, que esta moldura pinta.</param>
		/// <param name="frame">A cor da moldura em repouso (raridade, estrelas).</param>
		/// <param name="width">A espessura da borda em repouso.</param>
		public SelectionFrame(StyleBoxFlat box, Color frame, int width)
		{
			_box = box;
			Frame = frame;
			Width = width;
		}

		public Color Frame { get; set; }
		public int Width { get; set; }

		/// <summary>O quanto a moldura clareia sob o mouse.</summary>
		public float HoverLighten { get; init; } = 0.3f;

		/// <summary>A aura do escolhido, em px.</summary>
		public int GlowSize { get; init; } = 5;

		/// <summary>A sombra em repouso: cor, tamanho e deslocamento.</summary>
		public (Color Color, int Size, Vector2 Offset) Rest { get; init; } = (Colors.Transparent, 0, Vector2.Zero);

		/// <summary>A sombra sob o mouse.</summary>
		public (Color Color, int Size, Vector2 Offset) Hover { get; init; } = (Colors.Transparent, 0, Vector2.Zero);

		public void Apply(bool selected, bool marked, bool hover)
		{
			_box.BorderColor = selected ? States.Selected : hover ? Frame.Lightened(HoverLighten) : Frame;
			_box.SetBorderWidthAll(selected ? Width + 1 : Width);
			_box.BgColor = marked ? States.MarkedFill : hover ? States.HoverFill : Palette.Inset;
			var (color, size, offset) = selected ? (States.SelectedGlow, GlowSize, Vector2.Zero) : hover ? Hover : Rest;
			_box.ShadowColor = color;
			_box.ShadowSize = size;
			_box.ShadowOffset = offset;
		}
	}
}
