using System.Linq;
using Sigilos.Core.Battle.Sets;
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
	///
	/// Todo tipo de Passiva herda daqui o que é genérico:
	/// - a condição (<see cref="PassiveDefinition.Condition"/>), que vale contra o "outro lado" do momento;
	/// - as ações (<see cref="PassiveDefinition.Actions"/>): obrigar a mirar, revidar, não acordar quem dorme;
	/// - a camada do parceiro vivo: ações a mais, efeitos a mais e turnos a mais nos efeitos postos.
	/// </summary>
	internal abstract class EffectPassive : UnitBehavior
	{
		protected EffectPassive(PassiveDefinition passive) => Passive = passive;

		protected PassiveDefinition Passive { get; }

		/// <summary>Dispara os efeitos, mirando em <paramref name="main"/> (o "Target" deles; nulo: nenhum).</summary>
		protected void Fire(UnitRule rule, EffectResolver resolver, BattleUnit? main, bool force = false)
		{
			if (!Holds(rule, main))
				return;

			if (rule.Value > 0 && resolver.Random.NextDouble() >= rule.Value)
				return;

			resolver.Trigger(rule.Owner, Passive.Effects, main, force);

			// Com o parceiro vivo na hora do disparo, os efeitos dele vêm junto.
			if (Passive.PartnerEffects.Count > 0 && Partner(rule.Owner) != null)
				resolver.Trigger(rule.Owner, Passive.PartnerEffects, main, force);
		}

		/// <summary>O parceiro da Passiva (<see cref="PassiveDefinition.Partner"/>) vivo no time do dono; nulo se não tem ou caiu.</summary>
		protected BattleUnit? Partner(BattleUnit owner) => Passive.Partner.Length == 0
			? null
			: owner.Team.FirstOrDefault(unit => unit != owner && unit.IsAlive && unit.DefinitionId == Passive.Partner);

		/// <summary>A condição vale para o dono contra <paramref name="other"/> (sem condição, sempre).</summary>
		protected bool Holds(UnitRule rule, BattleUnit? other) => ConditionRules.Holds(Passive.Condition, rule.Owner, other);

		public override bool KeepsSleepers(UnitRule rule, BattleUnit sleeper) =>
			(Passive.Actions.Contains(PassiveAction.KeepAsleep) && Holds(rule, sleeper))
			|| (Passive.PartnerActions.Contains(PassiveAction.KeepAsleep) && Partner(rule.Owner) != null);

		/// <summary>Obriga a mirar o dono: só os inimigos para quem a condição vale, e só com o dono de pé.</summary>
		public override bool Forces(UnitRule rule, BattleUnit attacker) =>
			Passive.Actions.Contains(PassiveAction.Force)
			&& rule.Owner.IsAlive && attacker.Side != rule.Owner.Side && Holds(rule, attacker);

		/// <summary>
		/// Revide: o do dono, com a condição valendo contra quem o atingiu; com o parceiro vivo e Counter nas ações do
		/// parceiro, o dono e o parceiro revidam juntos, sempre. O revide não dispara este momento de novo.
		/// </summary>
		public override void AfterStruck(UnitRule rule, Cast cast)
		{
			var own = Passive.Actions.Contains(PassiveAction.Counter) && Holds(rule, cast.Caster);
			var partner = Passive.PartnerActions.Contains(PassiveAction.Counter) ? Partner(rule.Owner) : null;
			if (own || partner != null)
				CounterSet.Retaliate(rule.Owner, cast, 1);
			if (partner != null)
				CounterSet.Retaliate(partner, cast, 1);
		}

		/// <summary>
		/// Com o parceiro vivo, os efeitos que o dono põe duram <see cref="PassiveDefinition.PartnerTurns"/> a mais:
		/// os negativos em inimigos (padrão), os positivos nele e nos aliados, ou os dois (<see cref="PassiveDefinition.PartnerScope"/>).
		/// Reaplicar renova até o maior valor: o turno do parceiro entra uma vez por efeito, e a duração não cresce a
		/// cada reaplicação.
		/// </summary>
		public override void OnStatusGiven(UnitRule rule, EffectResolver resolver, BattleUnit target, StatusKind status)
		{
			if (Passive.PartnerTurns == 0 || Partner(rule.Owner) == null)
				return;

			var negative = BattleRules.IsNegative(status);
			var scope = Passive.PartnerScope ?? StatusScope.Debuffs;
			var extends = negative
				? scope != StatusScope.Buffs && target.Side != rule.Owner.Side
				: scope != StatusScope.Debuffs && target.Side == rule.Owner.Side;
			if (!extends)
				return;

			if (target.Find(status) is { } applied && !rule.Owner.Extended.Contains(applied))
			{
				rule.Owner.Extended.Add(applied);
				resolver.ChangeDuration(applied, Passive.PartnerTurns);
			}
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
