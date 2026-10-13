using System;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Núcleo Rúnico (Golem Rúnico): a cada rodada nova a Defesa do dono sobe uma fração da base
	/// (<see cref="UnitRule.Value"/> por passo, até <see cref="BattleRules.RunicCoreMaxSteps"/>). Quem conta é o
	/// momento "começo de rodada", que acontece uma vez por rodada: turno extra ou vários eventos no mesmo turno
	/// não acumulam. O Esquecimento cala a Passiva e a Defesa volta à base.
	/// </summary>
	internal sealed class RunicCorePassive : UnitBehavior
	{
		public override double Modify(UnitRule rule, Stat stat, double value) =>
			stat == Stat.Defense ? value * (1 + rule.Value * rule.Stacks) : value;

		public override void OnRoundStart(UnitRule rule, EffectResolver resolver, int round)
		{
			if (rule.Stacks >= BattleRules.RunicCoreMaxSteps)
				return;

			rule.Stacks = Math.Min(BattleRules.RunicCoreMaxSteps, rule.Stacks + 1);
			resolver.Emit(new PassiveStacked(rule.Owner, Stat.Defense, rule.Stacks));
		}
	}
}
