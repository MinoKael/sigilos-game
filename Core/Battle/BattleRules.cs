using System;
using Sigilos.Core.Battle.Statuses;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Os números do combate num lugar só (GDD, seções 7 e 15). O simulador
	/// dos testes (<c>dotnet run --project Tests -- --simular</c>) existe para mudá-los com base em dados.
	/// </summary>
	public static class BattleRules
	{
		public const double FullImpeto = 100;

		/// <summary>Perde quem não vencer em 99 rodadas. Uma rodada é o tempo que Velocidade 100 leva
		/// para encher a barra.</summary>
		public const int RoundLimit = 99;

		/// <summary>
		/// Dano: D = ATQ × M × K / (K + DEF). É a curva de defesa, 1000 / (1140 + 3,5 × DEF),
		/// com Defesa 0 valendo o golpe cheio: K = 1140 / 3,5 ≈ 326, e Defesa 326 corta o dano pela metade.
		/// </summary>
		public const double DefenseConstant = 1140 / 3.5;

		public const double AdvantageMultiplier = 1.25;
		public const double DisadvantageMultiplier = 0.75;

		/// <summary>Resistência efetiva nunca fica abaixo de 5%.</summary>
		public const double MinResistChance = 0.05;

		public const double BurnFraction = 0.05;
		public const double PoisonFraction = 0.03;
        public const int MaxBurnStacks = 0;
		public const int MaxPoisonStacks = 7;
        public const double CurseBonus = 0.25;
		public const double BlindMissChance = 0.5;
		public const double AttackUpBonus = 0.5;
		public const double AttackDownPenalty = 0.5;
		public const double DefenseUpBonus = 0.7;
		public const double DefenseDownPenalty = 0.7;
		public const double IgnoreDefense = 1.0;
        public const double SpeedUpBonus = 0.3;
		public const double BombDamageMultiplier = 2.5;

		/// <summary>Duração do escudo que a Passiva dos Cavaleiros dá ao cair.</summary>
		public const int DeathShieldTurns = 2;

		/// <summary>Abaixo desta fração da Vida máxima o alvo conta como ferido (Passiva dos Lobos).</summary>
		public const double WoundedFraction = 0.5;

		/// <summary>Duração da Queimadura que a Passiva dos Dragões põe.</summary>
		public const int BurnOnHitTurns = 2;

		/// <summary>Duração da Maldição que a Passiva dos Corvos põe.</summary>
		public const int CurseOnHitTurns = 2;

		/// <summary>Duração do Atordoamento que a Passiva das Gárgulas põe em quem as atinge.</summary>
		public const int StunAttackerTurns = 1;

		/// <summary>
		/// Efeitos negativos: a Resistência do alvo pode barrar, a Imunidade barra sempre e a Purificação
		/// remove. Quem diz é a estratégia de cada efeito (Statuses/StatusBehaviors).
		/// </summary>
		public static bool IsNegative(StatusKind status) => StatusBehaviors.Of(status).Harmful;

		/// <summary>
		/// Chance de barrar um efeito negativo: Resistência do alvo (até 100%) menos a
		/// Precisão de quem lança, nunca abaixo de 5%.
		/// </summary>
		public static double ResistChance(StatBlock target, StatBlock caster) =>
			Math.Max(MinResistChance, Math.Min(1, target.Resistance) - caster.Accuracy);
	}
}
