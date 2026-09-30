using System.Linq;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle.Sets
{
	/// <summary>
	/// Baluarte: no começo de cada onda, todos os aliados vivos ganham um escudo. Os escudos dos donos
	/// do conjunto somam num só — e, como escudo não acumula, quem entrega o total é o primeiro dono
	/// vivo do time.
	/// </summary>
	internal sealed class AllyShieldSet : UnitBehavior
	{
		public override void OnWaveStart(UnitRule rule, EffectResolver resolver)
		{
			var living = rule.Owner.Team.Where(u => u.IsAlive).ToList();
			var owners = living.Select(u => u.Innate(this)).Where(r => r != null).ToList();
			if (owners.Count == 0 || owners[0] != rule)
				return;

			var total = owners.Sum(r => r!.Value);
			foreach (var ally in living)
				resolver.GiveShield(ally, total, RuneSets.ShieldTurns);
		}
	}
}
