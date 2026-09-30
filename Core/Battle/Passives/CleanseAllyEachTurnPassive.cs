using System.Linq;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Pixies: no começo de cada turno, chance de tirar um efeito negativo de um aliado — o mais ferido
	/// entre os que têm algum (pode ser a própria, e aí ela pode até sair do atordoamento). Sem ninguém
	/// para limpar, nem sorteia.
	/// </summary>
	internal sealed class CleanseAllyEachTurnPassive : UnitBehavior
	{
		public override void OnTurnStart(UnitRule rule, EffectResolver resolver)
		{
			var ally = rule.Owner.Team
				.Where(u => u.IsAlive && u.Statuses.Any(status => BattleRules.IsNegative(status.Kind)))
				.OrderBy(u => u.HealthFraction)
				.FirstOrDefault();

			if (ally != null && resolver.Random.NextDouble() < rule.Value)
				resolver.Cleanse(ally);
		}
	}
}
