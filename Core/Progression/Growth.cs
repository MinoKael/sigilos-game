using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Como os atributos crescem com nível, raridade e Ecos (GDD, seção 15).
	///
	/// - Nível: Vida, Ataque e Defesa começam em 25% no nível 1 e crescem em linha reta até 100% no 60.
	///   Velocidade é fixa desde o nível 1, para o ajuste fino continuar importando.
	/// - Raridade: 5★ usa 100% dos valores de base, 4★ 92%, 3★ 85%. Inimigos de 1★ e 2★ seguem a escada.
	/// - Ecos: cada Eco reforça as habilidades; o quinto soma 10% aos atributos.
	/// </summary>
	public static class Growth
	{
		public const int MaxLevel = 60;
		public const double StartFraction = 0.25;
		public const int MaxEchoes = 5;

		/// <summary>Quanto cada Eco soma ao multiplicador das habilidades (dano, cura, escudo).</summary>
		public const double SkillPowerPerEcho = 0.05;

		/// <summary>Bônus de atributos com os 5 Ecos.</summary>
		public const double FullEchoStatBonus = 0.10;

		public static double LevelFactor(int level)
		{
			var clamped = Math.Clamp(level, 1, MaxLevel);
			return StartFraction + (1 - StartFraction) * (clamped - 1) / (MaxLevel - 1);
		}

		public static double RarityFactor(int rarity) => rarity switch
		{
			5 => 1.00,
			4 => 0.92,
			3 => 0.85,
			2 => 0.78,
			_ => 0.70,
		};

		/// <summary>Atributos de uma unidade: base do papel, escalada por raridade, nível e Ecos.</summary>
		public static StatBlock Stats(StatBlock roleBase, int rarity, int level, int echoes = 0)
		{
			var rarityFactor = RarityFactor(rarity);
			var levelFactor = LevelFactor(level);
			var echoFactor = echoes >= MaxEchoes ? 1 + FullEchoStatBonus : 1;
			return roleBase with
			{
				Health = roleBase.Health * rarityFactor * levelFactor * echoFactor,
				Attack = roleBase.Attack * rarityFactor * levelFactor * echoFactor,
				Defense = roleBase.Defense * rarityFactor * levelFactor * echoFactor,
				Speed = roleBase.Speed,
			};
		}

		public static double SkillPower(int echoes) => 1 + SkillPowerPerEcho * Math.Clamp(echoes, 0, MaxEchoes);

		/// <summary>O Poder do Conjurador cresce com o nível de conta como o Ataque de uma 5★.</summary>
		public static double ConjurerPower(double powerAtMaxLevel, int level) => powerAtMaxLevel * LevelFactor(level);
	}
}
