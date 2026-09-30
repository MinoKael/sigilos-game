namespace Sigilos.Core.Battle.Sets
{
	/// <summary>
	/// Contragolpe: quem foi atingido por uma habilidade inimiga e ficou de pé tem chance de revidar com
	/// a básica, a <see cref="Runes.RuneSets.CounterDamage"/> do dano. Quem perde o turno (atordoado)
	/// não revida.
	/// </summary>
	internal sealed class CounterSet : UnitBehavior
	{
		public override void AfterStruck(UnitRule rule, Cast cast)
		{
			var owner = rule.Owner;
			var attacker = cast.Caster;
			if (!attacker.IsAlive || !owner.IsAlive || owner.Side == attacker.Side || owner.Any(behavior => behavior.SkipsTurn))
				return;

			if (cast.Resolver.Random.NextDouble() < rule.Value)
				cast.Resolver.Counterattack(owner, attacker);
		}
	}
}
