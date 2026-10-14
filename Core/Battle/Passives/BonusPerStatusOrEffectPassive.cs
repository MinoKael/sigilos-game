using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Mais dano (o número da Passiva) para cada efeito que conta no alvo (<see cref="PassiveDefinition.Scope"/>
	/// e <see cref="PassiveDefinition.Statuses"/>): 0,1 = +10% por efeito. Com <see cref="PassiveDefinition.Partner"/>
	/// vivo no time, os efeitos que o dono põe duram mais (<see cref="PassiveDefinition.PartnerTurns"/>, na base).
	/// </summary>
	internal sealed class BonusPerStatusOrEffectPassive : EffectPassive
	{
		public BonusPerStatusOrEffectPassive(PassiveDefinition passive) : base(passive) { }

		public override double DamageDealt(UnitRule rule, BattleUnit target) =>
			!Holds(rule, target) ? 1 : 1 + rule.Value * target.Statuses.Count(status => Counts(status.Kind));
	}
}
