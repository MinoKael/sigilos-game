namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Liches: todo golpe drena <see cref="UnitRule.Value"/> do dano como Vida (somado ao dreno da habilidade) e, uma
	/// vez por habilidade em cada alvo que ficou de pé, o dono rouba o mesmo tanto do Ímpeto dele (0,15 = 15 pontos).
	/// </summary>
	internal sealed class SoulDrainPassive : UnitBehavior
	{
		public override void OnAttack(UnitRule rule, Strike strike) => strike.Drain += rule.Value;

		public override void AfterHit(UnitRule rule, Strike strike)
		{
			var target = strike.Target;
			if (!strike.Cast.FirstTime(rule, target) || target.Impeto <= 0)
				return;
			var taken = System.Math.Min(rule.Value * BattleRules.FullImpeto, target.Impeto);
			strike.Resolver.GainImpeto(target, -taken);
			strike.Resolver.GainImpeto(rule.Owner, taken);
		}
	}
}
