using Godot;

namespace Sigilos.UI.Animations
{
	/// <summary>
	/// O pulso do chamado (<see cref="Components.TouchButton.Highlight"/>): uma fase que vai de 0 a 1 e volta,
	/// a cada ~2 s. Quem desenha avança o relógio no <c>_Process</c> e lê a <see cref="Phase"/>. Não liga o
	/// processo sozinho: quem usa liga só enquanto o chamado está aceso.
	/// </summary>
	public sealed class Pulse
	{
		/// <summary>A velocidade do seno, em radianos por segundo.</summary>
		public const float Speed = 3.2f;

		private float _time;

		/// <summary>De 0 a 1: 0 apagado, 1 no auge.</summary>
		public float Phase => 0.5f + 0.5f * Mathf.Sin(_time * Speed);

		public void Advance(double delta) => _time += (float)delta;
	}
}
