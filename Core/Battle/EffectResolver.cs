using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle.Effects;
using Sigilos.Core.Battle.Statuses;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Faz as coisas acontecerem na luta. Tem duas partes:
	///
	/// - A ordem de uma habilidade (<see cref="Resolve"/>) e de um golpe (<see cref="Land"/>). Nenhuma
	///   das duas conhece um efeito, um status ou uma Passiva em particular: cada efeito da habilidade
	///   vai para a estratégia do tipo dele (Effects/SkillEffects), e a cada passo as regras em vigor nas
	///   unidades são avisadas (<see cref="UnitBehavior"/>).
	/// - As ações que as estratégias usam: ferir, curar, dar escudo, pôr e tirar efeito, mexer no Ímpeto,
	///   derrubar, contra-atacar. Toda ação avisa a sessão do que aconteceu (<see cref="BattleEvent"/>).
	///
	/// Quem decide <b>quando</b> é o <see cref="BattleSession"/>; esta classe só resolve.
	/// </summary>
	internal sealed class EffectResolver
	{
		private readonly BattleSession _session;

		public EffectResolver(BattleSession session)
		{
			_session = session;
		}

		/// <summary>O sorteio da luta: a mesma semente dá a mesma luta.</summary>
		public Random Random => _session.Random;

		/// <summary>O turno de agora é um turno extra.</summary>
		public bool IsExtraTurn => _session.IsExtraTurn;

		public void Emit(BattleEvent battleEvent) => _session.Emit(battleEvent);

		// Habilidade e golpe --------------------------------------------------------------------------

		/// <summary>
		/// Uma habilidade: os efeitos na ordem da lista, cada um pela estratégia do tipo dele; depois, o
		/// que a habilidade inteira dispara em quem lançou e em quem apanhou.
		/// </summary>
		/// <param name="counter">É um contra-ataque: dano reduzido e não provoca outro contra-ataque.</param>
		public void Resolve(BattleUnit caster, IReadOnlyList<EffectDefinition> effects, BattleUnit? chosen, bool counter = false)
		{
			var allies = _session.SideOf(caster.Side);
			var opponents = _session.SideOf(caster.Side == Side.Allies ? Side.Enemies : Side.Allies);
			var cast = new Cast(this, caster, Targeting.PickMain(caster, chosen, opponents), allies, opponents, counter);

			foreach (var effect in effects)
			{
				if (effect.OnKill && !cast.Killed)
					continue;

				SkillEffects.Of(effect.Kind).Apply(cast, effect);
				foreach (var rule in caster.Rules())
					rule.Behavior.AfterEffect(rule, cast, effect);
			}

			foreach (var rule in caster.Rules())
				rule.Behavior.AfterSkill(rule, cast);

			if (counter)
				return;

			foreach (var target in cast.Dealt.Keys.ToList())
			{
				foreach (var rule in target.Rules())
					rule.Behavior.AfterStruck(rule, cast);
			}
		}

		/// <summary>
		/// Um golpe, na ordem: quem ataca pode errar; quem apanha pode anular; o crítico; o dano; o
		/// escudo; o dreno; a queda; e, se o alvo ficou de pé, o que o golpe dispara nos dois.
		/// </summary>
		public void Land(Strike strike)
		{
			var attacker = strike.Attacker;
			var target = strike.Target;

			foreach (var rule in attacker.Rules())
			{
				rule.Behavior.OnAttack(rule, strike);
				if (strike.Missed)
				{
					Emit(new Missed(target));
					return;
				}
			}

			foreach (var rule in target.Rules())
			{
				rule.Behavior.OnDefend(rule, strike);
				if (strike.Blocked)
				{
					Emit(new Protected(target));
					return;
				}
			}

			var crit = strike.Crit ?? Random.NextDouble() < attacker.Stats.Crit;
			strike.Crit = crit;
			strike.Amount = DamageFormula.Compute(attacker, target, strike.Power, strike.IgnoreDefense, crit);
			strike.Absorbed = Absorb(target, strike.Amount);

			var dealt = strike.Dealt;
			target.Health = Math.Max(0, target.Health - dealt);
			strike.Cast.Dealt[target] = strike.Cast.Dealt.GetValueOrDefault(target) + dealt;
			Emit(new Damaged(target, (int)dealt, (int)strike.Absorbed, crit, ElementChart.Multiplier(attacker.Element, target.Element)));

			if (strike.Drain > 0)
				Heal(attacker, strike.Drain * strike.Amount);

			if (!target.IsAlive)
			{
				strike.Cast.Killed = true;
				KnockOut(target);
				return;
			}

			foreach (var rule in target.Rules())
				rule.Behavior.AfterHurt(rule, strike);
			foreach (var rule in attacker.Rules())
				rule.Behavior.AfterHit(rule, strike);
		}

		/// <summary>O contra-ataque: a básica de <paramref name="unit"/> em quem a atingiu.</summary>
		public void Counterattack(BattleUnit unit, BattleUnit attacker)
		{
			Emit(new Counterattack(unit));
			Resolve(unit, unit.Skill(0).Effects, attacker, counter: true);
		}

		// Vida ----------------------------------------------------------------------------------------

		/// <summary>
		/// Dano que não é golpe (Queimadura, Veneno, Bomba): não passa por Defesa, elemento nem crítico e
		/// não dispara o que um golpe dispara. Com <paramref name="shielded"/>, o escudo absorve antes.
		/// </summary>
		public void Wound(BattleUnit target, double amount, bool shielded = false)
		{
			if (!target.IsAlive)
				return;

			amount = Math.Max(1, Math.Round(amount));
			var absorbed = shielded ? Absorb(target, amount) : 0;
			var dealt = amount - absorbed;
			target.Health = Math.Max(0, target.Health - dealt);
			Emit(new Damaged(target, (int)dealt, (int)absorbed, false, 1));

			if (!target.IsAlive)
				KnockOut(target);
		}

		public void Heal(BattleUnit target, double amount)
		{
			if (!target.IsAlive)
				return;

			var healed = Math.Min(target.MaxHealth - target.Health, Math.Round(amount));
			if (healed <= 0)
				return;

			target.Health += healed;
			Emit(new Healed(target, (int)healed));
		}

		/// <summary>Uma unidade caiu: perde o Ímpeto e os efeitos, e as regras dela são avisadas.</summary>
		public void KnockOut(BattleUnit unit)
		{
			// As regras de antes da queda: os efeitos saem agora, mas ainda podem reagir a ela.
			var rules = unit.Rules().ToList();
			unit.Health = 0;
			unit.Impeto = 0;
			unit.ClearStatuses();
			Emit(new Died(unit));

			foreach (var rule in rules)
				rule.Behavior.OnDeath(rule, this);
		}

		/// <summary>A unidade caída que estava para voltar (<see cref="BattleUnit.RevivalHealth"/>) volta agora.</summary>
		public void Revive(BattleUnit unit)
		{
			unit.Health = Math.Round(unit.MaxHealth * unit.RevivalHealth);
			unit.RevivalHealth = 0;
			Emit(new Revived(unit));
		}

		// Efeitos de status ---------------------------------------------------------------------------

		/// <summary>
		/// Tenta pôr um efeito: sorteia a chance e, se o efeito é negativo e vem do outro lado, passa pela
		/// Imunidade e pela Resistência do alvo (<paramref name="resistible"/> falso pula a Resistência).
		/// </summary>
		public void ApplyStatus(BattleUnit caster, BattleUnit target, StatusKind status, double chance, int turns, bool resistible = true)
		{
			if (!target.IsAlive)
				return;

			if (Random.NextDouble() >= chance)
				return;

			if (StatusBehaviors.Of(status).Harmful && target.Side != caster.Side)
			{
				if (target.Any(behavior => behavior.BlocksHarmful))
				{
					Emit(new Immune(target));
					return;
				}

				if (resistible && Random.NextDouble() < BattleRules.ResistChance(target.Stats, caster.Stats))
				{
					Emit(new Resisted(target));
					return;
				}
			}

			Attach(caster, target, status, turns, 0);
		}

		/// <summary>Um efeito positivo que a unidade recebe de si mesma (a Imunidade do conjunto Tenacidade).</summary>
		public void GiveStatus(BattleUnit target, StatusKind status, int turns) => ApplyStatus(target, target, status, 1, turns);

		/// <summary>Escudos não somam: fica o maior valor e a maior duração.</summary>
		public void GiveShield(BattleUnit target, double value, int turns)
		{
			if (!target.IsAlive || value <= 0)
				return;

			Attach(null, target, StatusKind.Shield, turns, value);
		}

		/// <summary>Tira uma regra do dono; se é efeito de status, a tela é avisada.</summary>
		public void Remove(UnitRule rule)
		{
			var owner = rule.Owner;
			owner.Remove(rule);
			if (rule is StatusEffect status)
				Emit(new StatusRemoved(owner, status.Kind));
		}

		/// <summary>Remove um efeito negativo do alvo: o mais antigo.</summary>
		public void Cleanse(BattleUnit target)
		{
			var harmful = target.Statuses.FirstOrDefault(s => StatusBehaviors.Of(s.Kind).Harmful);
			if (harmful != null)
				Remove(harmful);
		}

		/// <summary>
		/// Põe o efeito, sem sorteio. Se o alvo já tem todas as cópias que cabem
		/// (<see cref="StatusBehavior.MaxStacks"/>), renova a primeira: fica a maior duração e o maior valor.
		/// </summary>
		private void Attach(BattleUnit? source, BattleUnit target, StatusKind status, int turns, double value)
		{
			var existing = target.Count(status) < StatusBehaviors.Of(status).MaxStacks ? null : target.Find(status);
			if (existing == null)
			{
				target.AddStatus(new StatusEffect(status, turns, value, source) { Fresh = IsActing(target) });
			}
			else
			{
				existing.Turns = Math.Max(existing.Turns, turns);
				existing.Value = Math.Max(existing.Value, value);
				existing.Source = source ?? existing.Source;
			}

			Emit(new StatusApplied(target, status, turns));
		}

		/// <summary>O que as regras do alvo seguram deste dano (o escudo).</summary>
		private static double Absorb(BattleUnit target, double amount)
		{
			double absorbed = 0;
			foreach (var rule in target.Rules())
				absorbed += rule.Behavior.Absorb(rule, amount - absorbed);
			return absorbed;
		}

		private bool IsActing(BattleUnit unit) => ReferenceEquals(_session.Current, unit);

		// Ímpeto e turno ------------------------------------------------------------------------------

		public void GainImpeto(BattleUnit target, double amount)
		{
			var before = target.Impeto;
			target.Impeto = Math.Clamp(target.Impeto + amount, 0, BattleRules.FullImpeto);
			if (target.Impeto != before)
				Emit(new ImpetoChanged(target, target.Impeto - before));
		}

		/// <summary>A unidade age de novo em seguida.</summary>
		public void GrantExtraTurn(BattleUnit unit)
		{
			unit.ExtraTurnPending = true;
			unit.Impeto = BattleRules.FullImpeto;
			Emit(new ExtraTurn(unit));
		}
	}
}
