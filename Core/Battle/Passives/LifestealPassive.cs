namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Vampiros: uma fração de todo dano que causam volta como Vida, somada ao dreno da própria habilidade.</summary>
	internal sealed class LifestealPassive : UnitBehavior
	{
		public override void OnAttack(UnitRule rule, Strike strike) => strike.Drain += rule.Value;
	}
}
