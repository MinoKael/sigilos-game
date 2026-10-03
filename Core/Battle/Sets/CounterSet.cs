namespace Sigilos.Core.Battle.Sets
{
	/// <summary>
	/// Contragolpe: quem foi atingido por uma habilidade inimiga e ficou de pé tem chance de revidar com
	/// a básica, a <see cref="Runes.RuneSets.CounterDamage"/> do dano. Quem perde o turno (atordoado)
	/// não revida.
	/// </summary>
	internal sealed class CounterSet : UnitBehavior
	{
		public override void AfterStruck(UnitRule rule, Cast cast) => Retaliate(rule.Owner, cast, rule.Value);

		/// <summary>
		/// O revide com a <paramref name="chance"/>: só de pé, contra quem é do outro lado e ainda vive, e não
		/// para quem perde o turno. É o do conjunto e o do efeito Contragolpe.
		/// </summary>
		public static void Retaliate(BattleUnit owner, Cast cast, double chance)
		{
			var attacker = cast.Caster;
			if (!attacker.IsAlive || !owner.IsAlive || owner.Side == attacker.Side || owner.Any(behavior => behavior.SkipsTurn))
				return;

			if (chance >= 1 || cast.Resolver.Random.NextDouble() < chance)
				cast.Resolver.Counterattack(owner, attacker);
		}
	}
}
