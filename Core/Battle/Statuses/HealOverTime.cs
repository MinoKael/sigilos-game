namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Cura a cada turno (Bênção): no começo do turno do dono, recupera uma fração da Vida máxima dele,
	/// pela cura de sempre (a Ferida barra).
	/// </summary>
	internal sealed class HealOverTime : StatusBehavior
	{
		private readonly double _fraction;

		public HealOverTime(double fraction)
		{
			_fraction = fraction;
		}

		public override void OnTurnStart(UnitRule rule, EffectResolver resolver) =>
			resolver.Heal(rule.Owner, rule.Owner.MaxHealth * _fraction);
	}
}
