using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Em quem atinge o dono (e ele sobrevive), uma vez por habilidade: o atordoamento das Gárgulas ("Target" é quem atacou).</summary>
	internal sealed class StatusOrEffectOnAttackerPassive : EffectPassive
	{
		public StatusOrEffectOnAttackerPassive(PassiveDefinition passive) : base(passive) { }

		public override void AfterHurt(UnitRule rule, Strike strike)
		{
			if (strike.Cast.FirstTime(rule, strike.Attacker))
				Fire(rule, strike.Resolver, strike.Attacker);
		}
	}
}
