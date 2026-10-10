using Godot;

namespace Sigilos.UI.Animations
{
	/// <summary>
	/// As durações e curvas das animações de interface, com nome. Quem anima um controle pede daqui, e o
	/// jogo inteiro responde no mesmo ritmo.
	/// </summary>
	public static class Motion
	{
		/// <summary>O retorno de um toque: o botão afunda e volta (<see cref="Juice"/>).</summary>
		public const double Quick = 0.12;

		/// <summary>A curva do retorno: passa um pouco do ponto e volta (o repique).</summary>
		public const Tween.TransitionType Spring = Tween.TransitionType.Back;

		/// <summary>Começa rápido e chega devagar.</summary>
		public const Tween.EaseType Settle = Tween.EaseType.Out;

		/// <summary>Anima <paramref name="property"/> de <paramref name="node"/> até <paramref name="to"/> com o repique, matando a animação anterior.</summary>
		public static Tween SpringTo(Node node, Tween? previous, NodePath property, Variant to, double duration = Quick)
		{
			previous?.Kill();
			var tween = node.CreateTween();
			tween.TweenProperty(node, property, to, duration).SetTrans(Spring).SetEase(Settle);
			return tween;
		}
	}
}
