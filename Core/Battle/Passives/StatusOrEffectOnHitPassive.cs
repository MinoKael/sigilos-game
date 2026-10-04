using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Em cada alvo atingido que ficou de pé, uma vez por habilidade: a Aflição dos Dragões e a Maldição
	/// dos Corvos ("Target" é o atingido).
	/// </summary>
	internal sealed class StatusOrEffectOnHitPassive : EffectPassive
	{
		public StatusOrEffectOnHitPassive(PassiveDefinition passive) : base(passive) { }

		public override void AfterHit(UnitRule rule, Strike strike)
		{
			if (strike.Cast.FirstTime(rule, strike.Target))
				Fire(rule, strike.Resolver, strike.Target);
		}
	}
}
