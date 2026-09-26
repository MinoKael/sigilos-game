using Godot;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// O "peso" de um botão: cresce um pouco sob o mouse, afunda ao apertar e volta com um repique.
	/// Ligado uma vez por botão; o centro da escala acompanha o tamanho.
	/// </summary>
	public static class Juice
	{
		public static void Attach(BaseButton button, float hover = 1.06f, float press = 0.95f)
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
			button.MouseEntered += () => Bounce(hover);
			button.MouseExited += () => Bounce(1f);
			button.ButtonDown += () => Bounce(press);
			button.ButtonUp += () => Bounce(button.IsHovered() ? hover : 1f);
		}
	}
}
