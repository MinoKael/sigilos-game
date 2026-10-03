namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>Resistir Crítico: os golpes que o dono recebe têm a chance de Crítico multiplicada por <see cref="BattleRules.CritResistFactor"/>.</summary>
	internal sealed class CritResistStatus : StatusBehavior
	{
		public override double CritTaken(UnitRule rule) => BattleRules.CritResistFactor;
	}
}
