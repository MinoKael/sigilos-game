using System.Linq;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Paladinos: no começo de cada turno, curam o aliado mais ferido (pode ser o próprio) numa fração da
	/// Vida máxima dele.
	/// </summary>
	internal sealed class HealAllyEachTurnPassive : UnitBehavior
	{
		public override void OnTurnStart(UnitRule rule, EffectResolver resolver)
		{
			var ally = rule.Owner.Team.Where(u => u.IsAlive).OrderBy(u => u.HealthFraction).FirstOrDefault();
			if (ally != null)
				resolver.Heal(ally, rule.Value * ally.MaxHealth);
		}
	}
}
