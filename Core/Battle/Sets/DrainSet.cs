namespace Sigilos.Core.Battle.Sets
{
	/// <summary>Sifão: uma fração do dano de cada golpe volta como Vida, somada ao dreno da própria habilidade.</summary>
	internal sealed class DrainSet : UnitBehavior
	{
		public override void OnAttack(UnitRule rule, Strike strike) => strike.Drain += rule.Value;
	}
}
