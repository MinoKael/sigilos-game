using System;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Escudo: absorve dano até o valor guardado (<see cref="UnitRule.Value"/>) e some quando ele acaba.</summary>
	internal sealed class ShieldStatus : StatusBehavior
	{
		public override double Absorb(UnitRule rule, double amount)
		{
			var absorbed = Math.Min(rule.Value, amount);
			rule.Value -= absorbed;
			if (rule.Value <= 0)
				rule.Owner.Remove(rule);
			return absorbed;
		}
	}
}
