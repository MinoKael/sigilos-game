using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// Depois da habilidade do turno do dono (não do contra-ataque nem do ataque conjunto), no alvo dela:
	/// "Target" é o inimigo em quem ele mirou.
	/// </summary>
	internal sealed class StatusOrEffectOnTargetEachTurnPassive : EffectPassive
	{
		public StatusOrEffectOnTargetEachTurnPassive(PassiveDefinition passive) : base(passive) { }

		public override void AfterSkill(UnitRule rule, Cast cast)
		{
			if (!cast.IsCounter && !cast.IsJoint)
				Fire(rule, cast.Resolver, cast.Main);
		}
	}
}
