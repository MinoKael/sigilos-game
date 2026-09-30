using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Sets
{
	/// <summary>Tenacidade: Imunidade no começo de cada onda, pelos turnos do conjunto.</summary>
	internal sealed class ImmunitySet : UnitBehavior
	{
		public override void OnWaveStart(UnitRule rule, EffectResolver resolver) =>
			resolver.GiveStatus(rule.Owner, StatusKind.Immunity, (int)rule.Value);
	}
}
