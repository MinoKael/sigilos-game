using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Arrastar com o mouse rola a lista, como o dedo no celular: o <see cref="ScrollContainer"/> do Godot
	/// só rola arrastando numa tela de toque. Apertar o botão esquerdo sobre a lista e mover mais que
	/// <see cref="Slop"/> rola nas direções que ela permite; o botão que estava sob o mouse perde o
	/// clique, então soltar depois de arrastar não compra nem abre nada. Os cartões e as runas (<see cref="Press"/>)
	/// já cancelam o toque sozinhos quando o mouse anda. Num aparelho de toque não faz nada: lá o Godot
	/// já rola arrastando.
	/// </summary>
	public partial class DragScroll : Node
	{
		/// <summary>Quanto o mouse anda, em px, antes de virar arrasto: a mesma folga do toque.</summary>
		private const float Slop = 14;

		private readonly ScrollContainer _scroll;
		private bool _armed;
		private bool _dragging;
		private Vector2 _start;
		private Vector2 _origin;
		private BaseButton? _button;

		private DragScroll(ScrollContainer scroll)
		{
			_scroll = scroll;
			Name = "DragScroll";
		}

		/// <summary>Liga o arrasto com o mouse na lista (fora de aparelho de toque).</summary>
		public static void Enable(ScrollContainer scroll)
		{
			if (!DisplayServer.IsTouchscreenAvailable())
				scroll.AddChild(new DragScroll(scroll));
		}

		public override void _Input(InputEvent @event)
		{
			switch (@event)
			{
				case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
					_dragging = false;
					_armed = Grabs(GetViewport().GuiGetHoveredControl());
					if (!_armed)
						return;
					_start = _scroll.GetGlobalMousePosition();
					_origin = new Vector2(_scroll.ScrollHorizontal, _scroll.ScrollVertical);
					return;

				case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
					_armed = _dragging = false;
					_button = null;
					return;

				// O movimento segue para a interface: é ele que faz o cartão sob o mouse cancelar o toque.
				case InputEventMouseMotion when _armed:
					var moved = _scroll.GetGlobalMousePosition() - _start;
					if (!_dragging)
					{
						if (moved.Length() < Slop)
							return;
						_dragging = true;
						CancelPress();
					}

					if (_scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled)
						_scroll.ScrollHorizontal = Mathf.RoundToInt(_origin.X - moved.X);
					if (_scroll.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled)
						_scroll.ScrollVertical = Mathf.RoundToInt(_origin.Y - moved.Y);
					return;
			}
		}

		/// <summary>
		/// O aperto é desta lista: sobre algo dentro dela, que não esteja em outra lista mais por dentro, e
		/// que não seja a barra de rolagem (que já se arrasta), um controle deslizante ou um campo de texto.
		/// Guarda o botão sob o mouse, para cancelar o clique dele se virar arrasto.
		/// </summary>
		private bool Grabs(Control? hovered)
		{
			if (hovered == null || !_scroll.IsVisibleInTree() || hovered is ScrollBar or Slider or LineEdit or TextEdit)
				return false;

			_button = null;
			for (Node? node = hovered; node != null; node = node.GetParent())
			{
				if (node is BaseButton button && _button == null)
					_button = button;
				if (node is ScrollContainer scroll)
					return scroll == _scroll;
			}

			return false;
		}

		/// <summary>
		/// Desligar e religar o botão desfaz o aperto (soltar não vira clique); o "soltou" avisado à mão
		/// desfaz o que começou no aperto (o salto do <see cref="Animations.Juice"/>, o toque longo do <see cref="Press"/>).
		/// </summary>
		private void CancelPress()
		{
			if (_button == null || !IsInstanceValid(_button) || _button.Disabled)
				return;

			_button.Disabled = true;
			_button.Disabled = false;
			_button.EmitSignal(BaseButton.SignalName.ButtonUp);
		}
	}
}
