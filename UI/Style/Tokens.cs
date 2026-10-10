namespace Sigilos.UI.Style
{
	/// <summary>
	/// O espaço entre peças (a <c>separation</c> dos contêineres e as margens): a escala que as telas já
	/// usavam, com nome. Um valor fora da escala é exceção e diz por quê.
	/// </summary>
	public static class Space
	{
		public const int None = 0;

		/// <summary>2 px: linhas de texto coladas, ícone e número dentro de uma cápsula pequena.</summary>
		public const int Hair = 2;

		/// <summary>4 px: símbolo e número de um custo, peças de uma ficha.</summary>
		public const int Tight = 4;

		/// <summary>6 px: abas lado a lado, ícone e valor numa cápsula.</summary>
		public const int Small = 6;

		/// <summary>8 px: células de grade, linhas de uma lista, abas em pé.</summary>
		public const int Medium = 8;

		/// <summary>10 px: o padrão entre peças de uma fileira (símbolo e texto de um botão).</summary>
		public const int Regular = 10;

		/// <summary>12 px: blocos de uma coluna (o conteúdo de uma janela, a coluna da tela).</summary>
		public const int Large = 12;

		/// <summary>14 px: grupos de um cabeçalho, botões de ação de uma janela.</summary>
		public const int Wide = 14;

		/// <summary>16 px: partes de uma tela.</summary>
		public const int Loose = 16;
	}

	/// <summary>O raio dos cantos das caixas.</summary>
	public static class Radius
	{
		/// <summary>6 px: plaquinhas e selos pequenos.</summary>
		public const int Small = 6;

		/// <summary>8 px: caixas rebaixadas, abas, opções de lista.</summary>
		public const int Medium = 8;

		/// <summary>10 px: botões de texto.</summary>
		public const int Button = 10;

		/// <summary>14 px: cartões de destino.</summary>
		public const int Tile = 14;
	}

	/// <summary>
	/// Os tamanhos de letra fora dos papéis do tema (<see cref="GameTheme.Title"/>, <see cref="GameTheme.Heading"/>,
	/// <see cref="GameTheme.Number"/>...). Quem tem um papel usa o papel; estes são para o texto de dentro dos
	/// componentes.
	/// </summary>
	public static class FontSize
	{
		/// <summary>12 px: legendas e valores secundários (<see cref="GameTheme.SmallSize"/>).</summary>
		public const int Small = GameTheme.SmallSize;

		/// <summary>14 px: o detalhe embaixo de um nome (aba, cartão).</summary>
		public const int Detail = 14;

		/// <summary>16 px: o texto corrido (<see cref="GameTheme.BodySize"/>) e o custo de um botão.</summary>
		public const int Body = GameTheme.BodySize;

		/// <summary>17 px: o texto de um botão baixo e o número de uma cápsula.</summary>
		public const int Compact = 17;

		/// <summary>18 px: o nome de uma aba.</summary>
		public const int Tab = 18;

		/// <summary>20 px: o texto de um botão da altura de toque.</summary>
		public const int Button = 20;

		/// <summary>22 px: um número ou nome em destaque fora do cabeçalho.</summary>
		public const int Large = 22;
	}

	/// <summary>
	/// O quanto um conteúdo apaga. Os botões desligados mostram o conteúdo com <see cref="Disabled"/> de
	/// opacidade: legível, mas claramente fora de uso.
	/// </summary>
	public static class Fade
	{
		/// <summary>O conteúdo de um botão desligado.</summary>
		public const float Disabled = 0.45f;

		/// <summary>O que flutua por cima de qualquer tela (o balão do chat, o aviso da Batalha automática): deixa ver o que está embaixo.</summary>
		public const float Floating = 0.8f;
	}
}
