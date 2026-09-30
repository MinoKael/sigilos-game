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
	/// Os atributos maiores não são uma porcentagem: cada variante traz os atributos despertos prontos
	/// (Data/summons, "awakened_stats"), calculados com o orçamento desperto das estrelas naturais dela
	/// (Core/Content/StatModel.cs). O bônus de atributo é um de quatro, somado por cima: +15 de
	/// Velocidade, +15% de Crítico, +25% de Resistência ou +25% de Precisão. O preço sobe com as estrelas
	/// naturais.
	/// </summary>
	public static class Awakening
	{
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

		/// <summary>Soma o bônus de atributo da variante aos atributos despertos dela.</summary>
		public static StatBlock Apply(StatBlock stats, AwakeningDefinition awakening) =>
			awakening.Stat is { } stat ? stats.With(stat, stats.Get(stat) + Bonus(stat)) : stats;
	}
}
