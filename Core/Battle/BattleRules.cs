using System;
using System.Collections.Generic;
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

		/// <summary>Aflição: fração da Vida máxima que cada cópia tira no começo do turno do dono.</summary>
		public const double AfflictionFraction = 0.05;

		/// <summary>
		/// No chefe, cada cópia da Aflição tira só esta parte do que tiraria: a Vida dele vale a de várias
		/// unidades, e dano em fração da Vida máxima derreteria qualquer chefe, passando por cima da mecânica dele.
		/// </summary>
		public const double BossAfflictionShare = 0.3;

		/// <summary>
		/// Quantos efeitos de status cabem num monstro, somando todos (positivos, negativos, escudo e cada cópia
		/// da Aflição). Cheio, um efeito novo não pega; o que ele já tem ainda se renova.
		/// </summary>
		public const int MaxStatuses = 10;
        public const double StigmaBonus = 0.25;
		public const double BlindMissChance = 0.5;

		/// <summary>
		/// Ataque e Defesa: o buff vale menos que o debuff equivalente (+50% contra −70%), para o melhor
		/// efeito do jogador nunca compensar por inteiro o do inimigo.
		/// </summary>
		public const double AttackUpBonus = 0.5;
		public const double AttackDownPenalty = 0.7;
		public const double DefenseUpBonus = 0.5;
		public const double DefenseDownPenalty = 0.7;
		public const double IgnoreDefense = 1.0;
        public const double SpeedUpBonus = 0.3;
		public const double BombDamageMultiplier = 2.5;

		/// <summary>
		/// Os efeitos a que todo chefe (<see cref="BattleUnit.IsBoss"/>) é imune. A lista é checada num ponto só,
		/// quando o efeito vai pegar (<see cref="EffectResolver"/>), então vale para toda fonte: habilidade,
		/// Passiva, conjunto de runas, Influência ou roubo. A Aflição não está aqui: continua pegando em chefe.
		/// </summary>
		public static readonly IReadOnlySet<StatusKind> BossImmunities = new HashSet<StatusKind>
		{
			StatusKind.Stun,
			StatusKind.Sleep,
			StatusKind.Oblivion,
			StatusKind.Silence,
			StatusKind.Bomb,
		};

		/// <summary>
		/// Afogamento (Santuário): fração da Vida máxima que cada unidade afetada perde a cada rodada.
		/// 10% é o valor da versão 0.1; fica aqui para o ajuste de balanço.
		/// </summary>
		public const double DrowningFraction = 0.10;

		/// <summary>
		/// Sacrifício que destrói (<see cref="Content.EffectDefinition.Destroy"/>): a Vida máxima de quem lança nunca cai
		/// mais do que isto no total, somando o Oblívio e os Sacrifícios (o Oblívio das runas para em 60%).
		/// </summary>
		public const double SacrificeDestroyLimit = 0.70;

		/// <summary>Núcleo Rúnico (Golem Rúnico): bônus de Defesa que ele ganha a cada rodada nova (0,15 = +15% da base).</summary>
		public const double RunicCoreStep = 0.15;

		/// <summary>Núcleo Rúnico: o bônus nunca passa de tantos passos, para a luta não virar eterna.</summary>
		public const int RunicCoreMaxSteps = 8;

		/// <summary>Tesouro da Serpe: dano a mais que ela causa num alvo, por buff que ele tenha.</summary>
		public const double SerpentTreasureBonus = 0.15;

		/// <summary>Tesouro da Serpe: quantos buffs do alvo contam para o dano a mais.</summary>
		public const int SerpentTreasureMaxBuffs = 5;

		/// <summary>A Bomba que explode atordoa o alvo por estes turnos.</summary>
		public const int BombStunTurns = 1;

		public const double SpeedDownPenalty = 0.3;

		/// <summary>Crítico+: pontos somados à chance de Crítico (0,3 = +30%).</summary>
		public const double CritUpBonus = 0.3;

		/// <summary>Resistir Crítico: a chance de Crítico de quem bate no dono é multiplicada por isto.</summary>
		public const double CritResistFactor = 0.5;

		/// <summary>Bênção: fração da Vida máxima recuperada no começo de cada turno.</summary>
		public const double BlessingFraction = 0.15;

		/// <summary>Reviver: com que fração da Vida máxima o dono volta.</summary>
		public const double ReviveHealth = 0.3;

		/// <summary>Abaixo desta fração da Vida máxima o alvo conta como ferido (Passiva dos Lobos).</summary>
		public const double WoundedFraction = 0.5;

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
