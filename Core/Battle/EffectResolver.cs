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
		/// <param name="joint">É a básica de um aliado chamado por um ataque conjunto: não chama outros.</param>
		/// <returns>A habilidade resolvida: o que ela fez (quem derrubou, a recarga a menos).</returns>
		public Cast Resolve(BattleUnit caster, IReadOnlyList<EffectDefinition> effects, BattleUnit? chosen, bool counter = false, bool joint = false)
		{
			var allies = _session.SideOf(caster.Side);
			var opponents = _session.SideOf(caster.Side == Side.Allies ? Side.Enemies : Side.Allies);
			var cast = new Cast(this, caster, Targeting.PickMain(caster, chosen, opponents), allies, opponents, counter, joint);

			foreach (var effect in effects)
			{
				// Quem lança pode cair no meio da própria habilidade (os espinhos dos Druidas): ela para ali.
				if (!caster.IsAlive)
					break;

				if (effect.OnKill && !cast.Killed)
					continue;

				SkillEffects.Of(effect.Kind).Apply(cast, effect);
				foreach (var rule in caster.Rules())
					rule.Behavior.AfterEffect(rule, cast, effect);
			}

			foreach (var rule in caster.Rules())
				rule.Behavior.AfterSkill(rule, cast);

			if (counter)
				return cast;

			foreach (var target in cast.Dealt.Keys.ToList())
			{
				foreach (var rule in target.Rules())
					rule.Behavior.AfterStruck(rule, cast);
			}

			return cast;
		}

		private bool _triggering;

		/// <summary>
		/// Os efeitos de uma Passiva genérica, como se <paramref name="owner"/> os lançasse mirando em
		/// <paramref name="main"/> (o "Target" deles; nulo: "Target" não acerta ninguém). Não é habilidade: as
		/// regras de depois do efeito, da habilidade e de quem apanhou não são avisadas. O que acontece dentro
		/// dela não dispara outra Passiva genérica, para uma não alimentar a outra sem fim;
		/// <paramref name="force"/> (a queda, que acontece uma vez só) dispara mesmo assim.
		/// </summary>
		public void Trigger(BattleUnit owner, IReadOnlyList<EffectDefinition> effects, BattleUnit? main, bool force = false)
		{
			if (effects.Count == 0 || (_triggering && !force))
				return;

			var outer = _triggering;
			_triggering = true;
			try
			{
				var allies = _session.SideOf(owner.Side);
				var opponents = _session.SideOf(owner.Side == Side.Allies ? Side.Enemies : Side.Allies);
				var cast = new Cast(this, owner, main, allies, opponents, counter: false);
				foreach (var effect in effects)
				{
					if (effect.OnKill && !cast.Killed)
						continue;

					SkillEffects.Of(effect.Kind).Apply(cast, effect);
				}
			}
			finally
			{
				_triggering = outer;
			}
		}

		/// <summary>
		/// Um golpe, na ordem: quem ataca pode errar; quem apanha pode esquivar ou anular; o crítico; o
		/// dano; o escudo; o dreno; a queda; e, se o alvo ficou de pé, o que o golpe dispara nos dois.
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
				if (strike.Missed)
				{
					Emit(new Missed(target));
					return;
				}

				if (strike.Blocked)
				{
					Emit(new Protected(target));
					return;
				}
			}

			var crit = strike.Crit ?? Random.NextDouble() < attacker.Current(Stat.Crit) * target.CritTaken();
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
				KnockOut(target);
				// Quem voltou na hora (Reviver) não conta como derrubado.
				strike.Cast.Killed |= !target.IsAlive;
				return;
			}

			foreach (var rule in target.Rules())
				rule.Behavior.AfterHurt(rule, strike);

			// O que o alvo devolveu pode ter derrubado quem atacou: aí o golpe dele não dispara mais nada.
			if (!attacker.IsAlive)
				return;
			foreach (var rule in attacker.Rules())
				rule.Behavior.AfterHit(rule, strike);
		}

		/// <summary>O contra-ataque: a básica de <paramref name="unit"/> em quem a atingiu.</summary>
		public void Counterattack(BattleUnit unit, BattleUnit attacker)
		{
			Emit(new Counterattack(unit));
			Resolve(unit, unit.Skill(0).Effects, attacker, counter: true);
		}

		/// <summary>O ataque conjunto: a básica de <paramref name="ally"/> em <paramref name="target"/>, com o dano inteiro.</summary>
		public Cast JointAttack(BattleUnit ally, BattleUnit? target)
		{
			Emit(new JointAttack(ally));
			return Resolve(ally, ally.Skill(0).Effects, target, joint: true);
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

		/// <summary>Cura, até a Vida máxima. Quem está com Ferida não recebe nada.</summary>
		public void Heal(BattleUnit target, double amount)
		{
			if (!target.IsAlive || target.Any(behavior => behavior.BlocksHealing))
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
			{
				rule.Behavior.OnDeath(rule, this);
				// Voltou na hora (Reviver): as outras regras não viram a queda.
				if (unit.IsAlive)
					break;
			}
		}

		/// <summary>
		/// Nivela a Vida: sobe pela cura (a Ferida barra) ou desce direto, sem passar por dano nem escudo.
		/// Nunca derruba: fica pelo menos 1.
		/// </summary>
		public void SetHealth(BattleUnit target, double health)
		{
			if (!target.IsAlive)
				return;

			health = Math.Clamp(Math.Round(health), 1, target.MaxHealth);
			if (health > target.Health)
			{
				Heal(target, health - target.Health);
				return;
			}

			var lost = target.Health - health;
			if (lost <= 0)
				return;
			target.Health = health;
			Emit(new HealthLeveled(target, (int)lost));
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

		/// <summary>
		/// Soma <paramref name="turns"/> à duração do efeito (negativo encurta). O que chega a 0 sai; o
		/// recebido no turno do dono continua contando o turno de agora como dele.
		/// </summary>
		public void ChangeDuration(StatusEffect status, int turns)
		{
			if (turns == 0 || !status.Owner.Holds(status))
				return;

			status.Turns += turns;
			if (status.Turns <= 0)
				Remove(status);
			else
				Emit(new DurationChanged(status.Owner, status.Kind, turns));
		}

		/// <summary>
		/// Rouba o efeito: ele sai de quem tinha e entra em <paramref name="thief"/> com os turnos e o valor
		/// que sobravam (o Karma de quem rouba barra, e o efeito se perde).
		/// </summary>
		public void Steal(StatusEffect status, BattleUnit thief)
		{
			if (!thief.IsAlive || !status.Owner.Holds(status))
				return;

			Remove(status);
			Attach(thief, thief, status.Kind, status.Turns, status.Value);
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
		/// (<see cref="StatusBehavior.MaxStacks"/>), ou já está com o limite de efeitos
		/// (<see cref="BattleRules.MaxStatuses"/>) e tem este, renova a primeira: fica a maior duração e o maior
		/// valor. Cheio e sem este efeito, o efeito novo não pega.
		/// </summary>
		private void Attach(BattleUnit? source, BattleUnit target, StatusKind status, int turns, double value)
		{
			if (!StatusBehaviors.Of(status).Harmful && target.Any(behavior => behavior.BlocksBeneficial))
			{
				Emit(new StatusBlocked(target, status));
				return;
			}

			var full = target.Statuses.Count >= BattleRules.MaxStatuses;
			var existing = !full && target.Count(status) < StatusBehaviors.Of(status).MaxStacks ? null : target.Find(status);
			if (existing == null && full)
			{
				Emit(new StatusBlocked(target, status));
				return;
			}

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

			// "Para cada efeito posto / recebido": as Passivas de quem pôs e de quem recebeu.
			if (source != null)
			{
				foreach (var rule in source.Rules().ToList())
					rule.Behavior.OnStatusGiven(rule, this, target, status);
			}

			foreach (var rule in target.Rules().ToList())
				rule.Behavior.OnStatusReceived(rule, this, source, status);
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
			if (target.Any(behavior => behavior.BlocksImpeto))
				return;

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
