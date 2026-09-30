using System.Collections.Generic;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle.Sets
{
	/// <summary>
	/// As estratégias dos conjuntos de runas que fazem algo em combate (<see cref="RuneSetEffect"/>).
	/// Cada conjunto completo vira uma regra que vale a luta inteira, com o número já somado em
	/// <see cref="UnitRule.Value"/> (três conjuntos iguais de 2 peças valem três vezes).
	/// </summary>
	internal static class SetBehaviors
	{
		private static readonly UnitBehavior Drain = new DrainSet();
		private static readonly UnitBehavior Stun = new StunSet();
		private static readonly UnitBehavior ExtraTurn = new ExtraTurnSet();
		private static readonly UnitBehavior Immunity = new ImmunitySet();
		private static readonly UnitBehavior AllyShield = new AllyShieldSet();
		private static readonly UnitBehavior Counter = new CounterSet();
		private static readonly UnitBehavior Bane = new BaneSet();
		private static readonly UnitBehavior Oblivion = new OblivionSet();

		/// <summary>As regras dos conjuntos completos de uma unidade. A ordem é a em que elas são avisadas.</summary>
		public static IEnumerable<UnitRule> RulesFor(RuneSetEffects effects)
		{
			if (effects.Drain > 0)
				yield return new UnitRule(Drain, effects.Drain);
			if (effects.StunChance > 0)
				yield return new UnitRule(Stun, effects.StunChance);
			if (effects.ExtraTurnChance > 0)
				yield return new UnitRule(ExtraTurn, effects.ExtraTurnChance);
			if (effects.ImmunityTurns > 0)
				yield return new UnitRule(Immunity, effects.ImmunityTurns);
			if (effects.AllyShield > 0)
				yield return new UnitRule(AllyShield, effects.AllyShield);
			if (effects.CounterChance > 0)
				yield return new UnitRule(Counter, effects.CounterChance);
			if (effects.BaneGauge > 0)
				yield return new UnitRule(Bane, effects.BaneGauge);
			if (effects.DestroyCap > 0)
				yield return new UnitRule(Oblivion, effects.DestroyCap);
		}
	}
}
