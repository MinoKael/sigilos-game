using Godot;
using Sigilos.UI.Audio;

namespace Sigilos.UI.Animations
{
	/// <summary>
	/// O "peso" de um botão: afunda ao apertar e volta com um repique (<see cref="Motion.SpringTo"/>). Sob o
	/// mouse não cresce: quem mostra o foco é a luz (a aura do sigilo, a moldura acesa). Ligado uma vez por
	/// botão (<see cref="Components.TouchButton"/> já liga); o centro da escala acompanha o tamanho. O clique
	/// soa como reserva (<see cref="Sfx.Fallback"/>): calado quando a ação tem som próprio; o botão de ligar e
	/// desligar soa o lado para onde foi.
	/// </summary>
	public static class Juice
	{
		public static void Attach(BaseButton button, float press = 0.95f)
		{
			Tween? tween = null;
			void Bounce(float scale) => tween = Motion.SpringTo(button, tween, "scale", Vector2.One * (button.Disabled ? 1 : scale));

			button.Resized += () => button.PivotOffset = button.Size / 2;
			button.ButtonDown += () => Bounce(press);
			button.ButtonUp += () => Bounce(1f);
			button.Pressed += () => Sfx.Fallback(!button.ToggleMode ? "ui.button_click" : button.ButtonPressed ? "ui.toggle_on" : "ui.toggle_off");
		}
	}
}
