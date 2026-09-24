using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Os números do combate num lugar só (GDD, seções 7 e 15). São valores de partida: o simulador
	/// dos testes (<c>dotnet run --project Tests -- --simular</c>) existe para mudá-los com base em dados.
	/// </summary>
	public static class BattleRules
	{
		public const double FullImpeto = 100;

		/// <summary>Perde quem não vencer em 30 rodadas. Uma rodada é o tempo que Velocidade 100 leva
		/// para encher a barra.</summary>
		public const int RoundLimit = 30;

		// Éter: recurso único do time, de 0 a 10, só para aprimorar habilidades no manual.
		// Básico não gera Éter; Glifo e inimigo derrubado geram 1. Todo aprimoramento custa pelo menos
		// MinEnhanceCost, mais do que um turno rende: não existe ciclo de usa-e-ganha.
		public const int MaxEther = 10;
		public const int BasicEtherGain = 0;
		public const int GlyphEtherGain = 1;
		public const int KillEtherGain = 1;
		public const int MinEnhanceCost = 2;

		// Dano: D = ATQ × M × K / (K + DEF) × E × C.
		public const double DefenseConstant = 600;
		public const double CritMultiplier = 1.5;
		public const double AdvantageMultiplier = 1.25;
		public const double DisadvantageMultiplier = 0.75;

		// Efeitos.
		public const double BurnFraction = 0.06;
		public const int MaxBurnStacks = 3;
		public const double CurseBonus = 0.25;
		public const double BlindMissChance = 0.5;
		public const double AttackUpBonus = 0.3;
		public const double AttackDownPenalty = 0.3;
		public const double DefenseUpBonus = 0.5;
		public const double SpeedUpBonus = 0.3;

		/// <summary>Duração do escudo que a Assinatura dos Cavaleiros dá ao cair.</summary>
		public const int DeathShieldTurns = 2;

		/// <summary>Efeitos negativos: a Resistência do alvo pode barrar, e a Purificação remove.</summary>
		public static bool IsNegative(StatusKind status) => status is
			StatusKind.Burn or
			StatusKind.Stun or
			StatusKind.Taunt or
			StatusKind.Curse or
			StatusKind.Blind or
			StatusKind.AttackDown;
	}
}
