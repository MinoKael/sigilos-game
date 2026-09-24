using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Como os atributos de base crescem com nível, raridade e Ecos. Os valores de Data/roles.json
	/// são de uma 5★ no nível 40 sem Despertar e sem runas — o equivalente a uma 5★ natural de Summoners
	/// War em 6★ nível 40.
	///
	/// - Nível: Vida, Ataque e Defesa crescem em linha reta do nível 1 ao 40. O nível 1 vale o que a
	///   estrela natural vale no nível 1 comparada ao 6★ nível 40 (Diabrete 3★: 22%,
	///   4★: 32%; Fênix 5★: 43%). Sem evolução de estrelas, o nível 40 é o 6★ máximo.
	///   Velocidade é fixa desde o nível 1.
	/// - Raridade: no nível 40, 5★ usa 100% dos valores, 4★ 92%, 3★ 85%.
	/// - Ecos: cada Eco reforça as habilidades; o quinto soma 10% aos atributos.
	/// </summary>
	public static class Growth
	{
		public const int MaxLevel = 40;
		public const int MaxEchoes = 5;

		/// <summary>Quanto cada Eco soma ao multiplicador das habilidades (dano, cura, escudo).</summary>
		public const double SkillPowerPerEcho = 0.05;

		/// <summary>Bônus de atributos com os 5 Ecos.</summary>
		public const double FullEchoStatBonus = 0.10;

		/// <summary>
		/// Fração dos atributos do nível 40 que a raridade tem no nível 1. Abaixo de 3★ só há inimigos,
		/// que crescem como 3★: assim a distância entre eles e o time não muda com o nível.
		/// </summary>
		public static double StartFraction(int rarity) => rarity switch
		{
			>= 5 => 0.43,
			4 => 0.32,
			_ => 0.22,
		};

		public static double LevelFactor(int rarity, int level)
		{
			var clamped = Math.Clamp(level, 1, MaxLevel);
			var start = StartFraction(rarity);
			return start + (1 - start) * (clamped - 1) / (MaxLevel - 1);
		}

		public static double RarityFactor(int rarity) => rarity switch
		{
			>= 5 => 1.00,
			4 => 0.92,
			3 => 0.85,
			2 => 0.78,
			_ => 0.70,
		};

		/// <summary>Atributos de base: papel escalado por raridade, nível e Ecos. Sem Despertar e sem runas.</summary>
		public static StatBlock Stats(StatBlock roleBase, int rarity, int level, int echoes = 0)
		{
			var factor = RarityFactor(rarity) * LevelFactor(rarity, level) * (echoes >= MaxEchoes ? 1 + FullEchoStatBonus : 1);
			return roleBase with
			{
				Health = Math.Round(roleBase.Health * factor),
				Attack = Math.Round(roleBase.Attack * factor),
				Defense = Math.Round(roleBase.Defense * factor),
			};
		}

		public static double SkillPower(int echoes) => 1 + SkillPowerPerEcho * Math.Clamp(echoes, 0, MaxEchoes);
	}
}
