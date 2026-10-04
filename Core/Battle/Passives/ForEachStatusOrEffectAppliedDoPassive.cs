using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Para cada efeito que conta que o dono põe (novo ou renovado, em qualquer um): "para cada efeito
	/// negativo posto, Ímpeto para a equipe". "Target" é quem recebeu.
	/// </summary>
	internal sealed class ForEachStatusOrEffectAppliedDoPassive : EffectPassive
	{
		public ForEachStatusOrEffectAppliedDoPassive(PassiveDefinition passive) : base(passive) { }

		public override void OnStatusGiven(UnitRule rule, EffectResolver resolver, BattleUnit target, StatusKind status)
		{
			if (Counts(status))
				Fire(rule, resolver, target);
		}
	}
}
