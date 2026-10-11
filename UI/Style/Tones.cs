using Godot;
using Sigilos.UI.Components;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As cores de cada peso de botão (<see cref="ButtonKind"/>), num lugar só: o preenchimento, a borda, a
	/// tinta do símbolo e o contorno das letras. <see cref="GameButton"/> e <see cref="TileButton"/> leem
	/// daqui; quem precisa de um tom diferente parte deste e ajusta (o cartão de destino, mais escuro).
	/// </summary>
	public static class Tones
	{
		/// <summary>O contorno das letras na madeira: quase preto, puxado para o marrom.</summary>
		public static readonly Color WoodOutline = new(0.08f, 0.05f, 0.03f);

		/// <summary>O preenchimento e a borda: âmbar, madeira com moldura de latão, carmim. O de texto não tem nenhum dos dois.</summary>
		public static (Color Fill, Color Border) Of(ButtonKind kind) => kind switch
		{
			ButtonKind.Primary => (Palette.Primary, Palette.PrimaryDark),
			ButtonKind.Danger => (Palette.Danger, Palette.DangerDark),
			ButtonKind.Text => (Colors.Transparent, Colors.Transparent),
			_ => (Palette.Button, Palette.GoldDark),
		};

		/// <summary>A tinta do símbolo e do título: ouro na madeira e no texto solto, creme no âmbar e no vermelho.</summary>
		public static Color Ink(ButtonKind kind) => kind is ButtonKind.Secondary or ButtonKind.Text ? Palette.Gold : Palette.Text;

		/// <summary>O contorno das letras: um tom escuro da própria cor do botão.</summary>
		public static Color Outline(ButtonKind kind) => kind switch
		{
			ButtonKind.Primary => Palette.PrimaryDark.Darkened(0.35f),
			ButtonKind.Danger => Palette.DangerDark.Darkened(0.3f),
			_ => WoodOutline,
		};
	}
}
