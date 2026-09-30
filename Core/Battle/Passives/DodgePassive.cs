namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Pássaros: chance de esquivar de cada golpe. O golpe esquivado erra, como o de quem está cego.</summary>
	internal sealed class DodgePassive : UnitBehavior
	{
		public override void OnDefend(UnitRule rule, Strike strike)
		{
			if (strike.Resolver.Random.NextDouble() < rule.Value)
				strike.Missed = true;
		}
	}
}
