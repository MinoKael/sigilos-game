using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// D = ATQ × M × K / (K + DEF) × E × C (GDD, seção 15), com K = <see cref="BattleRules.DefenseConstant"/>
	/// M já vem com o bônus de Ecos; C é 1 + Dano crítico no crítico; a
	/// Maldição no alvo soma 25% no fim, e as Assinaturas de dano (Goblins, Lobos, Limos) multiplicam.
	/// </summary>
	public static class DamageFormula
	{
		public static double Compute(BattleUnit attacker, BattleUnit target, double power, double ignoreDefense, bool crit)
		{
			var defense = target.Defense * (1 - Math.Clamp(ignoreDefense, 0, 1));
			var mitigation = BattleRules.DefenseConstant / (BattleRules.DefenseConstant + defense);
			var element = ElementChart.Multiplier(attacker.Element, target.Element);
			var critical = crit ? 1 + attacker.Stats.CritDamage : 1;
			var curse = target.Has(StatusKind.Curse) ? 1 + BattleRules.CurseBonus : 1;

			var damage = attacker.Attack * power * attacker.SkillPower * mitigation * element * critical * curse * Signatures(attacker, target);
			return Math.Max(1, Math.Round(damage));
		}

		/// <summary>Assinaturas que mexem no dano: bônus de quem ataca e redução de quem recebe.</summary>
		private static double Signatures(BattleUnit attacker, BattleUnit target)
		{
			var bonus = attacker.Passive?.Kind switch
			{
				PassiveKind.BonusVsDebuffed when target.Statuses.Any(s => BattleRules.IsNegative(s.Kind)) => 1 + attacker.PassiveValue,
				PassiveKind.BonusVsWounded when target.HealthFraction < BattleRules.WoundedFraction => 1 + attacker.PassiveValue,
				_ => 1.0,
			};
			var reduction = target.Passive?.Kind == PassiveKind.DamageReduction ? 1 - target.PassiveValue : 1;
			return bonus * reduction;
		}
	}
}
