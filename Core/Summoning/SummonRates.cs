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

		/// <summary>Direcionar com 1 Glifo: metade dos resultados vem dele.</summary>
		public const double OneGlyphShare = 0.5;

		/// <summary>Direcionar com 2 Glifos: 30% de cada um; o resto é livre.</summary>
		public const double TwoGlyphShare = 0.3;

		public const int MaxDirectedGlyphs = 2;

		public const int SingleCost = 1;
		public const int TenCost = 10;

		/// <summary>Duplicata além dos 5 Ecos vira Fragmentos.</summary>
		public static int FragmentsFor(int rarity) => rarity switch
		{
			5 => 20,
			4 => 10,
			_ => 5,
		};
	}
}
