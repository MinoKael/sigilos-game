using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Diabretes: mais Velocidade enquanto for o aliado com menos Vida.</summary>
	internal sealed class SpeedWhenLowestPassive : UnitBehavior
	{
		public override double Modify(UnitRule rule, Stat stat, double value) =>
			stat == Stat.Speed && IsLowestInTeam(rule.Owner) ? value * (1 + rule.Value) : value;

		/// <summary>Estritamente a menos Vida: empate (todos cheios no começo da luta) não conta.</summary>
		public static bool IsLowestInTeam(BattleUnit unit)
		{
			var others = unit.Team.Where(u => u.IsAlive && u != unit).ToList();
			return others.Count > 0 && others.All(u => u.HealthFraction > unit.HealthFraction);
		}
	}
}
