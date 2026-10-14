using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Mais dano (o número da Passiva) para cada efeito que conta no alvo (<see cref="PassiveDefinition.Scope"/>
	/// e <see cref="PassiveDefinition.Statuses"/>): 0,1 = +10% por efeito. Com <see cref="PassiveDefinition.Partner"/>
	/// vivo no time, cada efeito negativo que o dono põe dura <see cref="PassiveDefinition.PartnerTurns"/> turnos a mais.
	/// </summary>
	internal sealed class BonusPerStatusOrEffectPassive : EffectPassive
	{
		public BonusPerStatusOrEffectPassive(PassiveDefinition passive) : base(passive) { }

		public override double DamageDealt(UnitRule rule, BattleUnit target) =>
			1 + rule.Value * target.Statuses.Count(status => Counts(status.Kind));

		public override void OnStatusGiven(UnitRule rule, EffectResolver resolver, BattleUnit target, StatusKind status)
		{
			if (Passive.PartnerTurns == 0 || Partner(rule.Owner) == null || !BattleRules.IsNegative(status) || target.Side == rule.Owner.Side)
				return;

			// Reaplicar renova até o maior valor (Attach): o turno do parceiro entra uma vez, e a duração nunca passa do
			// que o efeito tinha ao ser posto mais o do parceiro (sem crescer a cada reaplicação).
			if (target.Find(status) is { } applied && !rule.Owner.Extended.Contains(applied))
			{
				rule.Owner.Extended.Add(applied);
				resolver.ChangeDuration(applied, Passive.PartnerTurns);
			}
		}
	}
}
