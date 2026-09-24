using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O Despertar (GDD, seção 10): paga Essência e a invocação ganha nome próprio, desenho novo,
	/// Assinatura melhorada, +15% de Vida, Ataque e Defesa e o atributo extra da variante
	/// (Data/summons, campo "awakening"). É para sempre.
	/// </summary>
	public static class Awakening
	{
		public const double StatBonus = 0.15;

		public static int Cost(int rarity) => rarity switch
		{
			>= 5 => 6000,
			4 => 3000,
			_ => 1500,
		};

		public static bool CanAwaken(PlayerState player, SummonDefinition summon) =>
			player.Owns(summon.Id) && !player.Summon(summon.Id).Awakened && player.Essence >= Cost(summon.Rarity);

		public static bool Awaken(PlayerState player, SummonDefinition summon)
		{
			if (!CanAwaken(player, summon))
				return false;

			player.Essence -= Cost(summon.Rarity);
			player.Summon(summon.Id).Awakened = true;
			return true;
		}

		/// <summary>Os atributos de base depois do Despertar.</summary>
		public static StatBlock Apply(StatBlock stats, AwakeningDefinition awakening) => stats
			.WithBonus(Stat.Health, StatBonus)
			.WithBonus(Stat.Attack, StatBonus)
			.WithBonus(Stat.Defense, StatBonus)
			.WithBonus(awakening.Stat, awakening.Value);
	}
}
