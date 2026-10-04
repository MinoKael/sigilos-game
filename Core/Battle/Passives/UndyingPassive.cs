using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Rei Ossudo: enquanto ele está em campo, ninguém ganha nem perde Ímpeto; depois de cada ação, os
	/// efeitos da Passiva (o escudo dele); ao cair, volta no turno seguinte com <see cref="UnitRule.Value"/>
	/// da Vida, toda vez. O Esquecimento cala a regra inteira: o Ímpeto volta a mexer, o escudo para e quem
	/// cai esquecido não volta (a queda só avisa as regras em vigor).
	/// </summary>
	internal sealed class UndyingPassive : EffectPassive
	{
		public UndyingPassive(PassiveDefinition passive) : base(passive) { }

		public override bool BlocksImpeto => true;

		public override void AfterAction(UnitRule rule, EffectResolver resolver) => resolver.Trigger(rule.Owner, Passive.Effects, null);

		public override void OnDeath(UnitRule rule, EffectResolver resolver) => rule.Owner.RevivalHealth = rule.Value;
	}
}
