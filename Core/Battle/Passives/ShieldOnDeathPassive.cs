using System.Linq;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Cavaleiros: ao cair, dão a todos os aliados um escudo de uma fração da própria Vida máxima.</summary>
	internal sealed class ShieldOnDeathPassive : UnitBehavior
	{
		public override void OnDeath(UnitRule rule, EffectResolver resolver)
		{
			var owner = rule.Owner;
			foreach (var ally in owner.Team.Where(u => u.IsAlive))
				resolver.GiveShield(ally, rule.Value * owner.MaxHealth, BattleRules.DeathShieldTurns);
		}
	}
}
