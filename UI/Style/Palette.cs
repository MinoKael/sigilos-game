using Godot;
using Sigilos.Core.Content;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As cores do jogo num lugar só. Base visual de Summoners War: painéis escuros com moldura dourada,
	/// texto claro, estrelas douradas (roxas depois do Despertar) e uma cor forte por elemento — "o
	/// elemento se lê antes do desenho" (GDD, seção 5).
	/// </summary>
	public static class Palette
	{
		public static readonly Color Background = Color.Color8(14, 16, 24);
		public static readonly Color Panel = Color.Color8(28, 33, 46);
		public static readonly Color PanelLight = Color.Color8(42, 49, 66);

		/// <summary>Fundo de área rebaixada: listas, barras, cartões.</summary>
		public static readonly Color Inset = Color.Color8(20, 24, 34);

		public static readonly Color Text = Color.Color8(232, 226, 210);
		public static readonly Color TextFaded = Color.Color8(150, 146, 132);
		public static readonly Color Gold = Color.Color8(214, 172, 76);
		public static readonly Color GoldDark = Color.Color8(120, 92, 40);

		public static readonly Color Button = Color.Color8(78, 62, 34);
		public static readonly Color ButtonHover = Color.Color8(106, 84, 44);
		public static readonly Color Disabled = Color.Color8(46, 46, 50);

		/// <summary>Bônus: o que as runas somam, o que sobe.</summary>
		public static readonly Color Positive = Color.Color8(126, 206, 116);

		public static readonly Color Negative = Color.Color8(222, 92, 80);

		/// <summary>Estrelas e nome de invocação desperta.</summary>
		public static readonly Color Awakened = Color.Color8(186, 118, 250);

		public static readonly Color Ether = Color.Color8(78, 200, 190);
		public static readonly Color Health = Color.Color8(96, 180, 90);
		public static readonly Color HealthLow = Color.Color8(210, 74, 60);
		public static readonly Color Shield = Color.Color8(170, 196, 220);
		public static readonly Color Damage = Color.Color8(250, 240, 225);
		public static readonly Color Heal = Positive;

		public static Color Stars(bool awakened) => awakened ? Awakened : Gold;

		/// <summary>Moldura por raridade: bronze, prata e ouro a partir da 3★ (GDD, seção 5).</summary>
		public static Color Frame(int stars) => stars switch
		{
			>= 5 => Gold,
			4 => Color.Color8(186, 192, 204),
			3 => Color.Color8(178, 118, 66),
			_ => TextFaded,
		};

		/// <summary>Cor da runa pelas estrelas, como as raridades de runa de Summoners War.</summary>
		public static Color RuneGrade(int grade) => grade switch
		{
			>= 5 => Color.Color8(240, 150, 60),
			4 => Color.Color8(186, 118, 250),
			3 => Color.Color8(90, 150, 240),
			2 => Color.Color8(110, 200, 110),
			_ => Color.Color8(190, 190, 190),
		};

		public static Color Of(Element element) => element switch
		{
			Element.Fire => Color.Color8(236, 88, 64),
			Element.Water => Color.Color8(70, 150, 240),
			Element.Wind => Color.Color8(96, 196, 104),
			Element.Light => Color.Color8(242, 206, 90),
			Element.Dark => Color.Color8(170, 104, 222),
			_ => Text,
		};
	}
}
