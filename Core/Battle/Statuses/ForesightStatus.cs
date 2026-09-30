using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Statuses
{
	/// <summary>
	/// Presságio: o próximo dano do dono é crítico. Vale para todos os golpes do efeito de dano (os
	/// vários golpes e os vários alvos) e some quando ele acaba.
	/// </summary>
	internal sealed class ForesightStatus : StatusBehavior
	{
		public override void OnAttack(UnitRule rule, Strike strike) => strike.Crit = true;

		public override void AfterEffect(UnitRule rule, Cast cast, EffectDefinition effect)
		{
			if (effect.Kind == EffectKind.Damage)
				cast.Resolver.Remove(rule);
		}
	}
}
