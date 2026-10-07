using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As cores do jogo num lugar só. Fantasia medieval aconchegante: couro envelhecido nos painéis,
	/// pedra escura nos fundos rebaixados, ouro fosco nas molduras e o brilho das runas energizadas
	/// (azul arcano, verde espiritual) em quem está ativo ou sob o mouse. Cada elemento tem uma cor
	/// forte — "o elemento se lê antes do desenho" (GDD, seção 5).
	/// </summary>
	public static class Palette
	{
		/// <summary>Fundo de tela: pedra quente quase preta, mais clara no centro (<see cref="BackgroundGlow"/>).</summary>
		public static readonly Color Background = Color.Color8(20, 15, 11);

		public static readonly Color BackgroundGlow = Color.Color8(52, 37, 24);

		/// <summary>Couro dos painéis.</summary>
		public static readonly Color Panel = Color.Color8(46, 34, 25);

		public static readonly Color PanelLight = Color.Color8(66, 49, 34);

		/// <summary>Pedra entalhada: listas, barras, cartões.</summary>
		public static readonly Color Inset = Color.Color8(27, 21, 16);

		/// <summary>Pergaminho: o texto.</summary>
		public static readonly Color Text = Color.Color8(240, 228, 202);

		public static readonly Color TextFaded = Color.Color8(168, 150, 120);

		/// <summary>Ouro fosco das molduras e dos ícones.</summary>
		public static readonly Color Gold = Color.Color8(214, 174, 96);

		public static readonly Color GoldDark = Color.Color8(122, 92, 50);
		public static readonly Color Grey = Color.Color8(220, 220, 220);

        /// <summary>Madeira dos botões de texto secundários.</summary>
        public static readonly Color Button = Color.Color8(74, 53, 34);

		public static readonly Color ButtonHover = Color.Color8(94, 68, 43);
		public static readonly Color Disabled = Color.Color8(48, 42, 37);

		/// <summary>O botão da ação principal da tela (Lutar, Invocar, Comprar): âmbar aceso.</summary>
		public static readonly Color Primary = Color.Color8(214, 132, 46);

		public static readonly Color PrimaryDark = Color.Color8(112, 58, 20);

		/// <summary>O botão do que não tem volta (soltar, vender, parar).</summary>
		public static readonly Color Danger = Color.Color8(166, 64, 48);

		public static readonly Color DangerDark = Color.Color8(86, 28, 20);

		/// <summary>O brilho de runa: sob o mouse, sigilo aceso, seleção.</summary>
		public static readonly Color Arcane = Color.Color8(124, 200, 255);

		/// <summary>O brilho verde: o que está pronto para coletar, o que o jogador deve tocar.</summary>
		public static readonly Color Spirit = Color.Color8(143, 227, 166);

		/// <summary>Bônus: o que as runas somam, o que sobe.</summary>
		public static readonly Color Positive = Color.Color8(143, 214, 124);

		public static readonly Color Negative = Color.Color8(226, 100, 84);

		/// <summary>Estrelas e nome de invocação desperta.</summary>
		public static readonly Color Awakened = Color.Color8(192, 132, 252);

		public static readonly Color Health = Color.Color8(112, 186, 96);
		public static readonly Color HealthLow = Color.Color8(214, 84, 66);
		public static readonly Color Shield = Color.Color8(176, 200, 222);
		public static readonly Color Damage = Color.Color8(250, 240, 225);
		public static readonly Color Heal = Positive;

		/// <summary>O violeta místico: sigilos desenhados, selos.</summary>
		public static readonly Color Violet = Awakened;

		/// <summary>As páginas do Grimório do Invocador: o fundo delas e a mancha das barras.</summary>
		public static readonly Color Parchment = Text;

		public static readonly Color ParchmentShade = Inset;

		/// <summary>A tinta escrita nas páginas, e a mais apagada das anotações.</summary>
		public static readonly Color Ink = Text;

		public static readonly Color InkFaded = TextFaded;

		/// <summary>A tinta de destaque das páginas (os títulos de capítulo, os selos lacrados).</summary>
		public static readonly Color Rubric = Color.Color8(120, 72, 170);

		public static Color Stars(bool awakened) => awakened ? Awakened : Gold;

		/// <summary>Moldura por raridade: bronze, prata e ouro a partir da 3★ (GDD, seção 5).</summary>
		public static Color Frame(int stars) => stars switch
		{
			>= 5 => Gold,
			4 => Color.Color8(190, 194, 204),
			3 => Color.Color8(180, 120, 70),
			_ => TextFaded,
		};

		/// <summary>Cor da runa e das pedras pela raridade: branca, verde, azul, roxa, laranja.</summary>
		public static Color Of(RuneRarity rarity) => rarity switch
		{
			RuneRarity.Legendary => Color.Color8(240, 150, 60),
			RuneRarity.Hero => Color.Color8(192, 132, 252),
			RuneRarity.Rare => Color.Color8(100, 160, 240),
			RuneRarity.Magic => Color.Color8(120, 204, 116),
			_ => Color.Color8(206, 200, 190),
		};

		public static Color Of(Element element) => element switch
		{
			Element.Fire => Color.Color8(236, 96, 64),
			Element.Water => Color.Color8(80, 156, 240),
			Element.Wind => Color.Color8(104, 200, 110),
			Element.Light => Color.Color8(244, 210, 100),
			Element.Dark => Color.Color8(176, 110, 226),
			_ => Text,
		};
	}
}
