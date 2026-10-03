namespace Sigilos.Core.Summoning
{
	/// <summary>Os números do gacha (GDD, seção 9). Generosos e visíveis: não há ninguém para vender nada.</summary>
	public static class SummonRates
	{
		public const double FiveStar = 0.035;
		public const double FourStar = 0.14;

		/// <summary>Uma 5★ garantida na 60ª invocação seguida sem nenhuma.</summary>
		public const int Pity = 60;

		/// <summary>Luz e Trevas têm um terço da chance das outras variantes da mesma raridade.</summary>
		public const double LightDarkWeight = 0.33;

		/// <summary>
		/// A primeira invocação da conta traz sempre esta variante; a segunda, uma 5★. As duas cabem na
		/// primeira ×10, a que o tutorial ensina.
		/// </summary>
		public const string FirstSummon = "knight_fire";

		public const int SingleCost = 1;
		public const int TenCost = 10;
	}
}
