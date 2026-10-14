using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Avalia a <see cref="PassiveCondition"/> de uma Passiva: o dono contra "o outro lado" do momento
	/// (<c>other</c>: quem escolhe o alvo, quem atacou, o alvo do golpe). Sem condição, vale sempre. Sem outro lado
	/// (começo de turno), a condição que o usa não vale.
	/// </summary>
	public static class ConditionRules
	{
		private const double Tolerance = 1e-6;

		public static bool Holds(PassiveCondition? condition, BattleUnit owner, BattleUnit? other)
		{
			if (condition == null)
				return true;

			var measured = condition.Of == ConditionSide.Self ? owner : other;
			if (measured == null)
				return false;

			double left;
			double right;
			if (condition.Stat is { } stat)
			{
				left = Read(measured, stat, condition.Against == ConditionAgainst.Value);
				if (condition.Against == ConditionAgainst.Value)
				{
					right = condition.Value;
				}
				else
				{
					var counterpart = condition.Of == ConditionSide.Self ? other : owner;
					if (counterpart == null)
						return false;
					right = Read(counterpart, stat, false);
				}
			}
			else if (condition.Status is { } status)
			{
				left = measured.Statuses.Count(s => s.Kind == status);
				right = condition.Value;
			}
			else if (condition.Scope is { } scope)
			{
				left = measured.Statuses.Count(s => Matches(scope, s.Kind));
				right = condition.Value;
			}
			else
			{
				return true;
			}

			return condition.Compare switch
			{
				Comparison.Greater => left > right + Tolerance,
				Comparison.Less => left < right - Tolerance,
				_ => Math.Abs(left - right) <= Tolerance,
			};
		}

		/// <summary>Vida contra um número é a fração da máxima; o resto é o valor de agora.</summary>
		private static double Read(BattleUnit unit, Stat stat, bool asFraction) =>
			asFraction && stat == Stat.Health ? unit.HealthFraction : unit.Current(stat);

		private static bool Matches(StatusScope scope, StatusKind status) => scope switch
		{
			StatusScope.Buffs => !BattleRules.IsNegative(status),
			StatusScope.Debuffs => BattleRules.IsNegative(status),
			_ => true,
		};
	}
}
