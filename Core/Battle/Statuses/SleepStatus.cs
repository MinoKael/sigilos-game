namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Sono: o dono perde os turnos até a duração acabar ou até levar um golpe, que o acorda.</summary>
	internal sealed class SleepStatus : StatusBehavior
	{
		public override bool Harmful => true;

		public override bool SkipsTurn => true;

		public override void AfterHurt(UnitRule rule, Strike strike) => strike.Resolver.Remove(rule);
	}
}
