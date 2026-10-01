using System;
using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// Toque curto e toque longo num controle que não é botão (cartão de monstro, unidade na luta, runa).
	/// O controle repassa o <c>_GuiInput</c> para <see cref="Feed"/>:
	///
	/// - <see cref="Tapped"/>: soltou antes de <see cref="HoldSeconds"/> sem arrastar o dedo.
	/// - <see cref="Held"/>: segurou parado por <see cref="HoldSeconds"/> (ou clique direito, no PC). Depois
	///   disso, soltar não conta como toque.
	///
	/// Arrastar mais que <see cref="Slop"/> cancela os dois: é o dedo rolando a lista. Por isso o controle
	/// deixa o evento seguir para cima (<c>MouseFilter.Pass</c>), e a rolagem de quem o contém funciona.
	/// </summary>
	public sealed class Press
	{
		public const double HoldSeconds = 0.45;
		private const float Slop = 14;

		private Vector2 _start;
		private bool _down;
		private int _token;

		public event Action? Tapped;
		public event Action? Held;

		/// <summary>Liga o toque longo a um controle qualquer pelo sinal de entrada (para quem não sobrescreve <c>_GuiInput</c>).</summary>
		public static Press On(Control control, Action? tapped, Action held)
		{
			var press = new Press();
			if (tapped != null)
				press.Tapped += tapped;
			press.Held += held;
			control.GuiInput += input => press.Feed(control, input);
			if (control.MouseFilter == Control.MouseFilterEnum.Ignore)
				control.MouseFilter = Control.MouseFilterEnum.Pass;
			return press;
		}

		/// <summary>
		/// Toque curto e longo num botão de verdade (<see cref="BaseButton"/>): o toque curto é o
		/// <c>Pressed</c> dele; segurar por <see cref="HoldSeconds"/> chama <paramref name="held"/> e o
		/// soltar seguinte não conta como toque.
		/// </summary>
		public static void OnButton(BaseButton button, Action tapped, Action held)
		{
			var holding = false;
			var token = 0;
			button.ButtonDown += () =>
			{
				holding = false;
				var mine = ++token;
				button.GetTree().CreateTimer(HoldSeconds).Timeout += () =>
				{
					if (mine != token || !GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
						return;
					holding = true;
					held();
				};
			};
			button.ButtonUp += () => token++;
			button.Pressed += () =>
			{
				if (holding)
				{
					holding = false;
					return;
				}

				tapped();
			};
			button.GuiInput += input =>
			{
				if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
				{
					held();
					return;
				}

				// Desligado, o botão não avisa que foi apertado; segurar ainda mostra o que ele é.
				if (!button.Disabled || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
					return;
				var mine = ++token;
				if (click.Pressed)
				{
					button.GetTree().CreateTimer(HoldSeconds).Timeout += () =>
					{
						if (mine == token && GodotObject.IsInstanceValid(button) && button.IsInsideTree())
							held();
					};
				}
			};
		}

		public void Feed(Control owner, InputEvent @event)
		{
			switch (@event)
			{
				case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
					_down = false;
					Held?.Invoke();
					return;

				case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } down:
					_down = true;
					_start = down.GlobalPosition;
					var token = ++_token;
					owner.GetTree().CreateTimer(HoldSeconds).Timeout += () =>
					{
						if (!_down || token != _token || !GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree())
							return;
						_down = false;
						Held?.Invoke();
					};
					return;

				case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
					if (_down)
					{
						_down = false;
						Tapped?.Invoke();
					}

					return;

				case InputEventMouseMotion motion when _down && motion.GlobalPosition.DistanceTo(_start) > Slop:
					_down = false;
					return;
			}
		}
	}
}
