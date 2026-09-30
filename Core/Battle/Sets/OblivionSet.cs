using System;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle.Sets
{
	/// <summary>
	/// Oblívio: no fim da habilidade, <see cref="RuneSets.DestroyShare"/> do dano de cada alvo vira Vida
	/// máxima perdida, até o teto do conjunto por habilidade e <see cref="RuneSets.DestroyLimit"/> no total.
	/// </summary>
	internal sealed class OblivionSet : UnitBehavior
	{
		public override void AfterSkill(UnitRule rule, Cast cast)
		{
			foreach (var (target, dealt) in cast.Dealt)
			{
				if (!target.IsAlive)
					continue;

				var room = RuneSets.DestroyLimit * target.Stats.Health - target.HealthDestroyed;
				var amount = Math.Round(Math.Min(Math.Min(RuneSets.DestroyShare * dealt, rule.Value * target.Stats.Health), room));
				if (amount <= 0)
					continue;

				target.HealthDestroyed += amount;
				target.Health = Math.Min(target.Health, target.MaxHealth);
				cast.Resolver.Emit(new MaxHealthReduced(target, (int)amount));
			}
		}
	}
}
