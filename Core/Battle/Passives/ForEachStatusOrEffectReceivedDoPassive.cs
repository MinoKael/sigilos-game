using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>Para cada efeito que conta que o dono recebe (novo ou renovado). "Target" é quem pôs (nulo no escudo).</summary>
	internal sealed class ForEachStatusOrEffectReceivedDoPassive : EffectPassive
	{
		public ForEachStatusOrEffectReceivedDoPassive(PassiveDefinition passive) : base(passive) { }

		public override void OnStatusReceived(UnitRule rule, EffectResolver resolver, BattleUnit? source, StatusKind status)
		{
			if (Counts(status))
				Fire(rule, resolver, source);
		}
	}
}
