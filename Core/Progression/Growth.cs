using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Como os atributos de base crescem com nível, raridade e Ecos. Os valores de Data/roles.json
	/// são de uma 5★ no nível 40, sem Despertar e sem runas.
	///
	/// - Nível: Vida, Ataque e Defesa começam em 25% no nível 1 e crescem em linha reta até 100% no 40.
	///   Velocidade é fixa desde o nível 1, para o ajuste fino continuar importando (GDD, seção 15).
	/// - Raridade: 5★ usa 100% dos valores, 4★ 92%, 3★ 85%. Inimigos de 1★ e 2★ seguem a escada.
	/// - Ecos: cada Eco reforça as habilidades; o quinto soma 10% aos atributos.
	/// </summary>
	public static class Growth
	{
		public const int MaxLevel = 40;
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

		/// <summary>Atributos de base: papel escalado por raridade, nível e Ecos. Sem Despertar e sem runas.</summary>
		public static StatBlock Stats(StatBlock roleBase, int rarity, int level, int echoes = 0)
		{
			var factor = RarityFactor(rarity) * LevelFactor(level) * (echoes >= MaxEchoes ? 1 + FullEchoStatBonus : 1);
			return roleBase with
			{
				Health = roleBase.Health * factor,
				Attack = roleBase.Attack * factor,
				Defense = roleBase.Defense * factor,
			};
		}

		public static double SkillPower(int echoes) => 1 + SkillPowerPerEcho * Math.Clamp(echoes, 0, MaxEchoes);
	}
}
