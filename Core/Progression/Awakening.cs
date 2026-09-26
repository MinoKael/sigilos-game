using System;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O Despertar (GDD, seção 10): paga Essência e a invocação ganha nome próprio, desenho novo,
	/// atributos maiores e o bônus da variante (Data/summons, campo "awakening"): um atributo, uma
	/// habilidade nova ou uma habilidade melhorada. É para sempre e vale em qualquer estrela.
	///
	/// A Vida é o que mais cresce (+20%; Ataque e Defesa +7%). O bônus de atributo é um de quatro:
	/// +15 de Velocidade, +15% de Crítico, +25% de Resistência ou +25% de Precisão. O preço sobe com as
	/// estrelas naturais.
	/// </summary>
	public static class Awakening
	{
		public const double HealthBonus = 0.20;
		public const double AttackDefenseBonus = 0.07;

		public static int Cost(int rarity) => rarity switch
		{
			>= 5 => 75_000,
			4 => 50_000,
			_ => 25_000,
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

		public static bool CanAwaken(PlayerState player, OwnedSummon monster, SummonDefinition summon) =>
			!monster.Awakened && player.Essence >= Cost(summon.Rarity);

		public static bool Awaken(PlayerState player, OwnedSummon monster, SummonDefinition summon)
		{
			if (!CanAwaken(player, monster, summon))
				return false;

			player.Essence -= Cost(summon.Rarity);
			monster.Awakened = true;
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
			return awakening.Stat is { } stat ? grown.With(stat, grown.Get(stat) + Bonus(stat)) : grown;
		}
	}
}
