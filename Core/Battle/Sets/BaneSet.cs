using System;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle.Sets
{
	/// <summary>Perdição: Ímpeto a cada <see cref="RuneSets.BaneStep"/> da Vida máxima perdida num golpe.</summary>
	internal sealed class BaneSet : UnitBehavior
	{
		public override void AfterHurt(UnitRule rule, Strike strike)
		{
			var owner = rule.Owner;
			if (strike.Dealt <= 0)
				return;

			var steps = Math.Floor(strike.Dealt / (RuneSets.BaneStep * owner.MaxHealth));
			if (steps > 0)
				strike.Resolver.GainImpeto(owner, steps * rule.Value * BattleRules.FullImpeto);
		}
	}
}
