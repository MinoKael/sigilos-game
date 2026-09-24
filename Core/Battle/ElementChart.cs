using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>Vantagem elemental: +25% de dano com vantagem, −25% com desvantagem.</summary>
	public static class ElementChart
	{
		public static bool HasAdvantage(Element attacker, Element defender) => (attacker, defender) switch
		{
			(Element.Fire, Element.Wind) => true,
			(Element.Wind, Element.Water) => true,
			(Element.Water, Element.Fire) => true,
			(Element.Light, Element.Dark) => true,
			(Element.Dark, Element.Light) => true,
			_ => false,
		};

		public static double Multiplier(Element attacker, Element defender)
		{
			// Luz contra Trevas cai sempre no primeiro caso: as duas têm vantagem uma sobre a outra.
			if (HasAdvantage(attacker, defender))
				return BattleRules.AdvantageMultiplier;

			if (HasAdvantage(defender, attacker))
				return BattleRules.DisadvantageMultiplier;

			return 1;
		}
	}
}
