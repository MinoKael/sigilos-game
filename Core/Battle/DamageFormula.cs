using System;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// D = ATQ × M × K / (K + DEF) × E × C (GDD, seção 15), com K = <see cref="BattleRules.DefenseConstant"/>.
	/// M já vem com o bônus de nível da habilidade; C é 1 + Dano crítico no crítico. No fim entram as
	/// regras em vigor nas duas unidades: o que aumenta ou corta o dano recebido pelo alvo (Maldição,
	/// a Passiva dos Limos) e o que aumenta o dano de quem ataca (as Passivas dos Goblins e dos Lobos).
	///
	/// ATQ × M é a conta do efeito (<see cref="EffectAmount"/>), que pode ler outros atributos. O dano fixo
	/// pula a Defesa, o elemento, o crítico e o dano de quem ataca: só o dano recebido pelo alvo conta.
	/// </summary>
	public static class DamageFormula
	{
		/// <summary>O golpe de sempre: <paramref name="power"/> × o Ataque de quem ataca.</summary>
		public static double Compute(BattleUnit attacker, BattleUnit target, double power, double ignoreDefense, bool crit) =>
			Apply(attacker, target, attacker.Attack * power, ignoreDefense, crit, false);

		/// <param name="amount">A conta do efeito antes da Defesa (ATQ × M).</param>
		public static double Apply(BattleUnit attacker, BattleUnit target, double amount, double ignoreDefense, bool crit, bool fixedDamage)
		{
			if (fixedDamage)
				return Math.Max(1, Math.Round(amount * target.DamageTaken()));

			var defense = target.Defense * (1 - Math.Clamp(ignoreDefense, 0, 1));
			var mitigation = BattleRules.DefenseConstant / (BattleRules.DefenseConstant + defense);
			var element = ElementChart.Multiplier(attacker.Element, target.Element);
			var critical = crit ? 1 + attacker.Stats.CritDamage : 1;

			var damage = amount * mitigation * element * critical * target.DamageTaken() * attacker.DamageDealt(target);
			return Math.Max(1, Math.Round(damage));
		}
	}
}
