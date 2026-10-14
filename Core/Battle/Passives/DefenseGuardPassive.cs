using Sigilos.Core.Battle.Sets;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Guarda: os inimigos de Defesa menor que a do dono são obrigados a mirá-lo (<see cref="Forces"/>). Com o
	/// <see cref="PassiveDefinition.Partner"/> vivo, quando uma habilidade inimiga atinge o dono ele e o parceiro
	/// revidam juntos com a básica, sem sorteio. O contra-ataque não dispara <see cref="AfterStruck"/>, então o
	/// revide conjunto não chama outro.
	/// </summary>
	internal sealed class DefenseGuardPassive : EffectPassive
	{
		public DefenseGuardPassive(PassiveDefinition passive) : base(passive) { }

		public override bool Forces(UnitRule rule, BattleUnit attacker) =>
			rule.Owner.IsAlive && attacker.Side != rule.Owner.Side && rule.Owner.Defense > attacker.Defense;

		public override void AfterStruck(UnitRule rule, Cast cast)
		{
			var partner = Partner(rule.Owner);
			if (partner == null)
				return;

			CounterSet.Retaliate(rule.Owner, cast, 1);
			CounterSet.Retaliate(partner, cast, 1);
		}
	}
}
