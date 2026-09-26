namespace Sigilos.Core.Summoning
{
	/// <summary>Os números do gacha (GDD, seção 9). Generosos e visíveis: não há ninguém para vender nada.</summary>
	public static class SummonRates
	{
		public const double FiveStar = 0.07;
		public const double FourStar = 0.28;

		/// <summary>Uma 5★ garantida na 60ª invocação seguida sem nenhuma.</summary>
		public const int Pity = 60;

		/// <summary>Luz e Trevas têm metade da chance das outras variantes da mesma raridade.</summary>
		public const double LightDarkWeight = 0.5;

		public const int SingleCost = 1;
		public const int TenCost = 10;
	}
}
