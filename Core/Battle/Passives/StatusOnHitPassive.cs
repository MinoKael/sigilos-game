using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Chance de pôr um efeito em cada alvo atingido, um sorteio por alvo a cada habilidade: a
	/// Queimadura dos Dragões e a Maldição dos Corvos. A chance é o número da Passiva.
	/// </summary>
	internal sealed class StatusOnHitPassive : UnitBehavior
	{
		private readonly StatusKind _status;
		private readonly int _turns;

		public StatusOnHitPassive(StatusKind status, int turns)
		{
			_status = status;
			_turns = turns;
		}

		public override void AfterHit(UnitRule rule, Strike strike)
		{
			if (strike.Cast.FirstTime(rule, strike.Target))
				strike.Resolver.ApplyStatus(strike.Attacker, strike.Target, _status, rule.Value, _turns);
		}
	}
}
