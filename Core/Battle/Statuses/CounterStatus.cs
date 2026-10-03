using Sigilos.Core.Battle.Sets;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Contragolpe: toda habilidade inimiga que atinge o dono e o deixa de pé é revidada com a básica, a
	/// <see cref="Runes.RuneSets.CounterDamage"/> do dano (o revide do conjunto Contragolpe, sem sorteio).
	/// </summary>
	internal sealed class CounterStatus : StatusBehavior
	{
		public override void AfterStruck(UnitRule rule, Cast cast) => CounterSet.Retaliate(rule.Owner, cast, 1);
	}
}
