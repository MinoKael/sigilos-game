using System;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Bandidos: dos dois lados, cada onda começa com pelo menos esta fração do Ímpeto.</summary>
	internal sealed class ImpetoAtWaveStartPassive : UnitBehavior
	{
		public override void OnWaveStart(UnitRule rule, EffectResolver resolver) =>
			resolver.GainImpeto(rule.Owner, Math.Max(0, rule.Value * BattleRules.FullImpeto - rule.Owner.Impeto));
	}
}
