using Godot;
using Sigilos.Core.Content;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As cores do jogo num lugar só (GDD, seção 5): pergaminho e pedra neutros, uma cor forte por
	/// elemento. "O elemento se lê antes do desenho."
	/// </summary>
	public static class Palette
	{
		/// <summary>Fundo das telas.</summary>
		public static readonly Color Stone = Color.Color8(24, 22, 19);

		/// <summary>Faixas e painéis escuros sobre a pedra.</summary>
		public static readonly Color StoneLight = Color.Color8(46, 42, 36);

		public static readonly Color Parchment = Color.Color8(233, 223, 199);
		public static readonly Color ParchmentDark = Color.Color8(205, 190, 156);

		/// <summary>Texto sobre pergaminho.</summary>
		public static readonly Color Ink = Color.Color8(43, 33, 24);

		/// <summary>Texto secundário sobre pergaminho.</summary>
		public static readonly Color InkFaded = Color.Color8(112, 97, 80);

		/// <summary>Texto sobre pedra.</summary>
		public static readonly Color Bone = Color.Color8(226, 218, 200);

		public static readonly Color Gold = Color.Color8(201, 160, 48);
		public static readonly Color Ether = Color.Color8(78, 188, 180);
		public static readonly Color Health = Color.Color8(96, 156, 78);
		public static readonly Color HealthLow = Color.Color8(196, 72, 56);
		public static readonly Color Shield = Color.Color8(170, 190, 210);
		public static readonly Color Damage = Color.Color8(250, 240, 225);
		public static readonly Color Heal = Color.Color8(120, 210, 110);

		/// <summary>Estrelas: bronze, prata e ouro a partir da 3★ (GDD, seção 5).</summary>
		public static Color Rarity(int stars) => stars switch
		{
			>= 5 => Gold,
			4 => Color.Color8(176, 180, 188),
			3 => Color.Color8(176, 116, 64),
			_ => InkFaded,
		};

		public static Color Of(Element element) => element switch
		{
			Element.Fire => Color.Color8(196, 58, 42),
			Element.Water => Color.Color8(42, 104, 196),
			Element.Wind => Color.Color8(56, 146, 76),
			Element.Light => Color.Color8(206, 160, 32),
			Element.Dark => Color.Color8(128, 66, 168),
			_ => Ink,
		};
	}
}
