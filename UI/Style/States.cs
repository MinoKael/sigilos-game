using Godot;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As cores dos estados de interação, iguais em todo o jogo: o que está escolhido acende em azul arcano,
	/// o que está marcado numa seleção de vários fica esverdeado, o que está sob o mouse clareia.
	/// </summary>
	public static class States
	{
		/// <summary>A borda do escolhido (o cartão, a opção atual, o sigilo ligado).</summary>
		public static readonly Color Selected = Palette.Arcane;

		/// <summary>A aura do escolhido.</summary>
		public static readonly Color SelectedGlow = new(Palette.Arcane, 0.4f);

		/// <summary>O fundo aceso do escolhido (o sigilo ligado, a opção atual de uma lista).</summary>
		public static readonly Color LitFill = Palette.Inset.Lerp(Palette.Arcane, 0.14f);

		/// <summary>O fundo do marcado numa seleção de vários (fundir, soltar, vender).</summary>
		public static readonly Color MarkedFill = Palette.Inset.Lerp(Palette.Spirit, 0.18f);

		/// <summary>O fundo rebaixado sob o mouse.</summary>
		public static readonly Color HoverFill = Palette.Inset.Lightened(0.06f);
	}
}
