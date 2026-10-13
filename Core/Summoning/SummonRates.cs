using Sigilos.Core.Content;

namespace Sigilos.Core.Summoning
{
	/// <summary>
	/// Os números do gacha (GDD, seção 9). Visíveis, mas a 5★ é rara e Luz e Trevas são o orgulho da conta:
	/// só saem do pergaminho delas, que vem dos marcos do jogo.
	/// </summary>
	public static class SummonRates
	{
		/// <summary>As chances de 5★ e de 4★ de cada pergaminho; o resto é 3★ (o Lendário não tem 3★).</summary>
		public static (double FiveStar, double FourStar) Of(ScrollKind kind) => kind switch
		{
			ScrollKind.LightDark => (0.01, 0.07),
			ScrollKind.Legendary => (0.07, 0.93),
			_ => (0.01, 0.09),
		};

		/// <summary>
		/// Transmutação Alquímica (Pergaminho Místico): a chance, no total, de sair um homúnculo de Luz ou Trevas
		/// (0,001 = 0,1%). Sai da fatia das 3★: com 90% de 3★, 89,9% ficam comuns e 0,1% viram LD. Ela se reparte
		/// por igual entre os LD elegíveis (<see cref="MysticLightDarkEligible"/>): com N elegíveis, cada um tem
		/// 0,1% / N. É o único lugar com esse número; a tela de Transmutação lê daqui.
		/// </summary>
		public const double MysticLightDark = 0.001;

		/// <summary>Os LD que o Místico pode dar.</summary>
		public static bool MysticLightDarkEligible(SummonDefinition summon) =>
			summon.Element is Element.Light or Element.Dark;

		/// <summary>A chance de 3★ do pergaminho (o que sobra de 5★ e 4★), antes do LD do Místico.</summary>
		public static double ThreeStar(ScrollKind kind)
		{
			var (five, four) = Of(kind);
			return 1 - five - four;
		}

		/// <summary>O pergaminho tira esta variante: o de Luz e Trevas, só Luz e Trevas; os outros, só Fogo, Água e Vento.</summary>
		public static bool Allows(ScrollKind kind, Element element) =>
			kind == ScrollKind.LightDark ? element is Element.Light or Element.Dark : element is not (Element.Light or Element.Dark);

		/// <summary>
		/// Pergaminho Místico: uma 5★ garantida na 150ª invocação seguida sem nenhuma. Os outros não têm
		/// garantia. Mais curta que isso, a garantia viraria a fonte principal de 5★ (com 60, a chance real
		/// passaria de 1% para 2,2%).
		/// </summary>
		public const int Pity = 150;

		/// <summary>No drop de 2★ da Campanha, Luz e Trevas têm um terço da chance das outras variantes.</summary>
		public const double LightDarkWeight = 0.33;

		/// <summary>
		/// A primeira invocação da conta traz sempre esta variante; a segunda, uma 5★. As duas cabem na
		/// primeira ×10 de Pergaminhos Místicos, a que o tutorial ensina.
		/// </summary>
		public const string FirstSummon = "miner_fire";

		public const int SingleCost = 1;
		public const int TenCost = 10;
	}
}
