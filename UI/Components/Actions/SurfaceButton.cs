using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O botão genérico: uma superfície de toque com as caixas e o conteúdo que o chamador quiser (uma opção de
	/// lista, uma cápsula do cabeçalho, um retrato, um texto que abre uma explicação). Afundar, clique, cursor e
	/// apagar desligado vêm de <see cref="TouchButton"/>; o conteúdo vai em <see cref="Body"/>.
	///
	/// <code>
	/// var row = new SurfaceButton { Name = "Option1", CustomMinimumSize = new Vector2(0, 52) }
	///     .Boxes(rest, hover, pressed)
	///     .Padded(12, 12);
	/// row.Body.AddChild(label);
	/// row.Pressed += Choose;
	/// </code>
	///
	/// Com <see cref="Hug"/>, o tamanho mínimo acompanha o conteúdo (e as margens), nunca abaixo de
	/// <see cref="Floor"/>; sem ele, o tamanho é o que o chamador pôs em <c>CustomMinimumSize</c>.
	/// </summary>
	public partial class SurfaceButton : TouchButton
	{
		private bool _hug;
		private Vector2 _floor;

		/// <param name="press">A escala ao afundar (veja <see cref="TouchButton"/>).</param>
		public SurfaceButton(float press = 0.95f) : base(press)
		{
		}

		public SurfaceButton() : this(0.95f)
		{
		}

		/// <summary>Onde vai o conteúdo: preenche o botão, menos as margens de <see cref="Padded"/>; não recebe toque.</summary>
		public MarginContainer Body => Content;

		/// <summary>O tamanho mínimo acompanha o conteúdo (o <c>Button</c> não mede filhos).</summary>
		public bool Hug
		{
			get => _hug;
			set
			{
				_hug = value;
				Fit();
			}
		}

		/// <summary>O menor tamanho com <see cref="Hug"/> (a altura de uma cápsula, por exemplo).</summary>
		public Vector2 Floor
		{
			get => _floor;
			set
			{
				_floor = value;
				Fit();
			}
		}

		/// <summary>
		/// As caixas de cada estado: <paramref name="hover"/> sob o dedo e <paramref name="pressed"/> apertado
		/// (sem elas, repetem a anterior). Nula em <paramref name="rest"/> não desenha nada parado.
		/// </summary>
		public SurfaceButton Boxes(StyleBox? rest, StyleBox? hover = null, StyleBox? pressed = null)
		{
			rest ??= new StyleBoxEmpty();
			hover ??= rest;
			pressed ??= hover;
			AddThemeStyleboxOverride("normal", rest);
			AddThemeStyleboxOverride("hover", hover);
			AddThemeStyleboxOverride("pressed", pressed);
			AddThemeStyleboxOverride("hover_pressed", pressed);
			return this;
		}

		/// <summary>As margens de <see cref="Body"/>: esquerda, direita, em cima e embaixo.</summary>
		public SurfaceButton Padded(int left, int right, int top = 0, int bottom = 0)
		{
			Pad(left, right, top, bottom);
			return this;
		}

		protected override Vector2? MinimumFor(Vector2 content) =>
			_hug ? new Vector2(Mathf.Max(content.X, _floor.X), Mathf.Max(content.Y, _floor.Y)) : null;
	}
}
