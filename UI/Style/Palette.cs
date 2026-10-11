using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.UI.Style
{
	/// <summary>
	/// As cores do jogo num lugar só. O grimório de um invocador que estuda as estrelas: couro de capa
	/// de livro (ameixa, violeta escuro) nos painéis, tinta de noite (índigo quase preto) nos fundos
	/// rebaixados, pergaminho no texto e nas páginas, ouro velho só no que é destaque e pontos de luz
	/// de estrela no que está aceso. A regra das cores:
	///
	/// - roxo e violeta: o místico (couro, sigilos, selos, invocação);
	/// - azul e índigo: o céu (fundo, mapa, seleção, o que acende sob o dedo);
	/// - ouro: hierarquia e o que é precioso (molduras, títulos, a ação principal);
	/// - verde-aurora: o que está pronto ou o caminho a seguir.
	///
	/// Cada elemento tem uma cor forte — "o elemento se lê antes do desenho" (GDD, seção 5).
	/// </summary>
	public static class Palette
	{
		/// <summary>Fundo de tela: céu de noite quase preto, mais claro e violeta no centro (<see cref="BackgroundGlow"/>).</summary>
		public static readonly Color Background = Color.Color8(12, 10, 22);

		public static readonly Color BackgroundGlow = Color.Color8(38, 29, 64);

		/// <summary>Couro de capa de grimório, ameixa: os painéis.</summary>
		public static readonly Color Panel = Color.Color8(40, 29, 50);

		public static readonly Color PanelLight = Color.Color8(58, 43, 72);

		/// <summary>Tinta de noite: listas, barras, cartões, o que fica rebaixado no painel.</summary>
		public static readonly Color Inset = Color.Color8(17, 15, 30);

		/// <summary>Pergaminho: o texto.</summary>
		public static readonly Color Text = Color.Color8(240, 230, 208);

		/// <summary>O texto secundário: cinza de lavanda, frio, para não competir com o pergaminho.</summary>
		public static readonly Color TextFaded = Color.Color8(170, 160, 186);

		/// <summary>Ouro velho das molduras, dos títulos e dos ícones.</summary>
		public static readonly Color Gold = Color.Color8(216, 178, 104);

		/// <summary>Latão gasto: a moldura em repouso, os filetes.</summary>
		public static readonly Color GoldDark = Color.Color8(124, 98, 62);

		public static readonly Color Grey = Color.Color8(220, 220, 220);

		/// <summary>Couro violeta da lombada: os botões de texto secundários.</summary>
		public static readonly Color Button = Color.Color8(58, 42, 76);

		public static readonly Color ButtonHover = Color.Color8(76, 56, 98);
		public static readonly Color Disabled = Color.Color8(40, 37, 50);

		/// <summary>O botão da ação principal da tela (Lutar, Invocar, Comprar): âmbar dourado, o único quente.</summary>
		public static readonly Color Primary = Color.Color8(206, 146, 58);

		public static readonly Color PrimaryDark = Color.Color8(106, 64, 24);

		/// <summary>O botão do que não tem volta (soltar, vender, parar): carmim de lacre.</summary>
		public static readonly Color Danger = Color.Color8(156, 50, 64);

		public static readonly Color DangerDark = Color.Color8(78, 22, 36);

		/// <summary>A luz de estrela azul: sob o mouse, sigilo aceso, seleção.</summary>
		public static readonly Color Arcane = Color.Color8(150, 178, 255);

		/// <summary>O verde-aurora: o que está pronto para coletar, o que o jogador deve tocar, o trecho vencido.</summary>
		public static readonly Color Spirit = Color.Color8(126, 222, 186);

		/// <summary>Bônus: o que as runas somam, o que sobe.</summary>
		public static readonly Color Positive = Color.Color8(143, 214, 124);

		public static readonly Color Negative = Color.Color8(226, 100, 84);

		/// <summary>Estrelas e nome de invocação desperta.</summary>
		public static readonly Color Awakened = Color.Color8(196, 146, 255);

		public static readonly Color Health = Color.Color8(112, 186, 96);
		public static readonly Color HealthLow = Color.Color8(214, 84, 66);
		public static readonly Color Shield = Color.Color8(176, 200, 222);
		public static readonly Color Damage = Color.Color8(250, 240, 225);
		public static readonly Color Heal = Positive;

		/// <summary>O violeta místico: sigilos desenhados, selos, os traços das constelações na página.</summary>
		public static readonly Color Violet = Color.Color8(134, 98, 206);

		/// <summary>O índigo do céu: a grade do mapa celeste, os anéis do astrolábio.</summary>
		public static readonly Color Indigo = Color.Color8(70, 76, 150);

		/// <summary>O ponto de luz de estrela: o brilho de fundo, as estrelas do mapa.</summary>
		public static readonly Color Starlight = Color.Color8(232, 236, 255);

		/// <summary>A página de pergaminho (o Grimório do Invocador): o papel e a mancha das bordas.</summary>
		public static readonly Color Parchment = Color.Color8(226, 210, 174);

		public static readonly Color ParchmentShade = Color.Color8(178, 150, 108);

		/// <summary>A tinta escrita no pergaminho, e a mais apagada das anotações.</summary>
		public static readonly Color Ink = Color.Color8(48, 34, 44);

		public static readonly Color InkFaded = Color.Color8(98, 78, 84);

		/// <summary>A tinta de destaque da página (os títulos de capítulo): violeta, como as iluminuras.</summary>
		public static readonly Color Rubric = Color.Color8(98, 54, 138);

		public static Color Stars(bool awakened) => awakened ? Awakened : Gold;

		/// <summary>Moldura por raridade: bronze, prata e ouro a partir da 3★ (GDD, seção 5).</summary>
		public static Color Frame(int stars) => stars switch
		{
			>= 5 => Gold,
			4 => Color.Color8(190, 194, 210),
			3 => Color.Color8(180, 120, 76),
			_ => TextFaded,
		};

		/// <summary>Cor da runa e das pedras pela raridade: branca, verde, azul, roxa, laranja.</summary>
		public static Color Of(RuneRarity rarity) => rarity switch
		{
			RuneRarity.Legendary => Color.Color8(240, 150, 60),
			RuneRarity.Hero => Color.Color8(196, 140, 252),
			RuneRarity.Rare => Color.Color8(104, 156, 244),
			RuneRarity.Magic => Color.Color8(120, 204, 132),
			_ => Color.Color8(206, 202, 196),
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
