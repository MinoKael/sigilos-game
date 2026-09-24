using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// D = ATQ × M × K / (K + DEF) × E × C (GDD, seção 15), com K = <see cref="BattleRules.DefenseConstant"/>.
	/// M já vem com o bônus de Ecos; a Maldição no alvo soma 25% no fim.
	/// </summary>
	public static class DamageFormula
	{
		public static double Compute(Caster caster, BattleUnit target, double power, double ignoreDefense, bool crit)
		{
			var defense = target.Defense * (1 - Math.Clamp(ignoreDefense, 0, 1));
			var mitigation = BattleRules.DefenseConstant / (BattleRules.DefenseConstant + defense);
			var element = caster.Element is { } attackerElement ? ElementChart.Multiplier(attackerElement, target.Element) : 1;
			var critical = crit ? BattleRules.CritMultiplier + caster.CritDamage : 1;
			var curse = target.Has(StatusKind.Curse) ? 1 + BattleRules.CurseBonus : 1;

			var damage = caster.Attack * power * caster.SkillPower * mitigation * element * critical * curse;
			return Math.Max(1, Math.Round(damage));
		}
	}
}
