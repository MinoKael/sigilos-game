using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Mais dano (o número da Passiva) em quem tem algum efeito que conta: os Goblins, contra efeito negativo.</summary>
	internal sealed class BonusVsStatusOrEffectPassive : EffectPassive
	{
		public BonusVsStatusOrEffectPassive(PassiveDefinition passive) : base(passive) { }

		public override double DamageDealt(UnitRule rule, BattleUnit target) => HasAny(target) ? 1 + rule.Value : 1;
	}
}
