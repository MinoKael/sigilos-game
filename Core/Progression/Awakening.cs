using System;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O Despertar (GDD, seção 10): paga Essência e a invocação ganha nome próprio, desenho novo,
	/// Assinatura melhorada, atributos maiores e o bônus da variante (Data/summons, campo "awakening").
	/// É para sempre.
	///
	/// Os números seguem: a Vida é o que mais cresce (o Diabrete de Fogo ganha 20% de
	/// Vida e 7% de Ataque e Defesa) e o bônus é um de quatro: +15 de Velocidade, +15% de Crítico,
	/// +25% de Resistência ou +25% de Precisão.
	/// </summary>
	public static class Awakening
	{
		public const double HealthBonus = 0.20;
		public const double AttackDefenseBonus = 0.07;

		public static int Cost(int rarity) => rarity switch
		{
			>= 5 => 6000,
			4 => 3000,
			_ => 1500,
		};

		/// <summary>O bônus da variante: Velocidade em número, os outros em fração.</summary>
		public static double Bonus(Stat stat) => stat switch
		{
			Stat.Speed => 15,
			Stat.Crit => 0.15,
			Stat.Resistance => 0.25,
			Stat.Accuracy => 0.25,
			_ => 0,
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
		public static StatBlock Apply(StatBlock stats, AwakeningDefinition awakening)
		{
			var grown = stats with
			{
				Health = Math.Round(stats.Health * (1 + HealthBonus)),
				Attack = Math.Round(stats.Attack * (1 + AttackDefenseBonus)),
				Defense = Math.Round(stats.Defense * (1 + AttackDefenseBonus)),
			};
			return grown.With(awakening.Stat, grown.Get(awakening.Stat) + Bonus(awakening.Stat));
		}
	}
}
