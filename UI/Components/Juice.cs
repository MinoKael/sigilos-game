using Godot;
using Sigilos.UI.Audio;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O "peso" de um botão: afunda ao apertar e volta com um repique. Sob o mouse não cresce: quem mostra
	/// o foco é a luz (a aura do sigilo, a moldura acesa). Ligado uma vez por botão; o centro da escala
	/// acompanha o tamanho. O clique soa como reserva (<see cref="Sfx.Fallback"/>): calado quando a ação
	/// tem som próprio; o botão de ligar e desligar soa o lado para onde foi.
	/// </summary>
	public static class Juice
	{
		public static void Attach(BaseButton button, float press = 0.95f)
		{
			Tween? tween = null;
			void Bounce(float scale)
			{
				if (button.Disabled)
					scale = 1;
				tween?.Kill();
				tween = button.CreateTween();
				tween.TweenProperty(button, "scale", Vector2.One * scale, 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}

			button.Resized += () => button.PivotOffset = button.Size / 2;
			button.ButtonDown += () => Bounce(press);
			button.ButtonUp += () => Bounce(1f);
			button.Pressed += () => Sfx.Fallback(!button.ToggleMode ? "ui.button_click" : button.ButtonPressed ? "ui.toggle_on" : "ui.toggle_off");
		}
	}
}
