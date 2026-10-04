using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Passives
{
	/// <summary>
	/// A base das Passivas genéricas: num momento da luta, os efeitos da habilidade passiva
	/// (<see cref="PassiveDefinition.Effects"/>, os mesmos tipos de efeito das habilidades) acontecem como se
	/// o dono os lançasse (<see cref="EffectResolver.Trigger"/>). O número da Passiva é a chance de
	/// disparar (0 = sempre). As que olham efeitos de status contam só os de
	/// <see cref="PassiveDefinition.Statuses"/> (vazio: todos do <see cref="PassiveDefinition.Scope"/>).
	///
	/// Uma instância por Passiva de unidade: guarda só a definição, que não muda.
	/// </summary>
	internal abstract class EffectPassive : UnitBehavior
	{
		protected EffectPassive(PassiveDefinition passive) => Passive = passive;

		protected PassiveDefinition Passive { get; }

		/// <summary>Dispara os efeitos, mirando em <paramref name="main"/> (o "Target" deles; nulo: nenhum).</summary>
		protected void Fire(UnitRule rule, EffectResolver resolver, BattleUnit? main, bool force = false)
		{
			if (rule.Value > 0 && resolver.Random.NextDouble() >= rule.Value)
				return;

			resolver.Trigger(rule.Owner, Passive.Effects, main, force);
		}

		/// <summary>O efeito de status conta para esta Passiva.</summary>
		protected bool Counts(StatusKind status) => Passive.Statuses.Count > 0
			? Passive.Statuses.Contains(status)
			: Passive.Scope switch
			{
				StatusScope.Buffs => !BattleRules.IsNegative(status),
				StatusScope.Debuffs => BattleRules.IsNegative(status),
				_ => true,
			};

		/// <summary>A unidade tem algum efeito que conta.</summary>
		protected bool HasAny(BattleUnit unit) => unit.Statuses.Any(status => Counts(status.Kind));
	}
}
