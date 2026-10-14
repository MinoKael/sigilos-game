using System.Linq;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Sono: o dono perde os turnos até a duração acabar ou até levar um golpe, que o acorda.</summary>
	internal sealed class SleepStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool SkipsTurn => true;

		public override void AfterHurt(UnitRule rule, Strike strike)
		{
			// Passiva que mantém o sono (com a condição valendo, ou com o parceiro vivo): o golpe de quem a tem não acorda.
			if (strike.Attacker.Rules().Any(other => other.Behavior.KeepsSleepers(other, rule.Owner)))
				return;

			strike.Resolver.Remove(rule);
		}
	}
}
