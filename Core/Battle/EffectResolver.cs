using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Faz cada <see cref="EffectDefinition"/> acontecer: dano, cura, escudo, efeitos, Ímpeto. Aplica
	/// também o que os conjuntos de runas fazem a cada habilidade: Vampiro e Nêmesis a cada golpe;
	/// Desespero uma vez por alvo; Destruição e Vingança no fim da habilidade.
	///
	/// Quem escolhe <b>quando</b> resolver é o <see cref="BattleSession"/>; esta classe só resolve e
	/// avisa a sessão do que aconteceu (eventos, quedas, Éter).
	/// </summary>
	internal sealed class EffectResolver
	{
		private readonly BattleSession _session;

		public EffectResolver(BattleSession session)
		{
			_session = session;
		}

		/// <param name="counter">Contra-ataque da Vingança: dano reduzido e não provoca outro contra-ataque.</param>
		public void Resolve(BattleUnit caster, IReadOnlyList<EffectDefinition> effects, BattleUnit? chosen, bool counter = false)
		{
			var allies = _session.SideOf(caster.Side);
			var opponents = _session.SideOf(caster.Side == Side.Allies ? Side.Enemies : Side.Allies);

			// O alvo é escolhido uma vez para a habilidade inteira: se ele cai no primeiro golpe, a
			// Queimadura que vinha depois não pula para outro inimigo.
			var main = Targeting.PickMain(caster, chosen, opponents);
			var hit = new Hit(counter ? RuneSets.CounterDamage : 1);

			foreach (var effect in effects)
			{
				if (effect.OnKill && !hit.Killed)
					continue;

				foreach (var target in Targeting.Resolve(effect.Target, caster, main, allies, opponents))
				{
					switch (effect.Kind)
					{
						case EffectKind.Damage:
							Damage(caster, target, effect, hit);
							break;
						case EffectKind.Heal:
							Heal(target, effect.Power * target.MaxHealth * caster.SkillPower);
							break;
						case EffectKind.Shield:
							GiveShield(target, effect.Power * caster.MaxHealth * caster.SkillPower, effect.Turns);
							break;
						case EffectKind.Status:
							ApplyStatus(caster, target, effect.Status, effect.Chance, effect.Turns);
							break;
						case EffectKind.Impeto:
							PushImpeto(caster, target, effect);
							break;
						case EffectKind.Cleanse:
							Cleanse(target);
							break;
					}
				}

				if (effect.Kind == EffectKind.Damage && caster.Find(StatusKind.Foresight) is { } foresight)
				{
					caster.RemoveStatus(foresight);
					_session.Emit(new StatusRemoved(caster, StatusKind.Foresight));
				}
			}

			Destroy(caster, hit);
            ExtraTurnAvailability(caster);
			if (!counter)
				Counterattacks(caster, hit);
		}

		/// <summary>Escudos não somam: fica o maior valor e a maior duração.</summary>
		public void GiveShield(BattleUnit target, double value, int turns)
		{
			if (!target.IsAlive || value <= 0)
				return;

			var shield = target.Find(StatusKind.Shield);
			if (shield == null)
			{
				target.AddStatus(new StatusEffect(StatusKind.Shield, turns, value) { Fresh = IsActing(target) });
			}
			else
			{
				shield.Value = Math.Max(shield.Value, value);
				shield.Turns = Math.Max(shield.Turns, turns);
			}

			_session.Emit(new StatusApplied(target, StatusKind.Shield, turns));
		}

		/// <summary>Um efeito positivo que a unidade recebe sem sorteio (Imunidade da Vontade).</summary>
		public void GiveStatus(BattleUnit target, StatusKind status, int turns) => ApplyStatus(target, target, status, 1, turns);

		private void Damage(BattleUnit caster, BattleUnit target, EffectDefinition effect, Hit hit)
		{
			var element = ElementChart.Multiplier(caster.Element, target.Element);

			for (var i = 0; i < effect.Hits && target.IsAlive; i++)
			{
				if (caster.Has(StatusKind.Blind) && _session.Random.NextDouble() < BattleRules.BlindMissChance)
				{
					_session.Emit(new Missed(target));
					continue;
				}

				if (target.Find(StatusKind.Ward) is { } ward)
				{
					target.RemoveStatus(ward);
					_session.Emit(new Warded(target));
					continue;
				}

				var crit = caster.Has(StatusKind.Foresight) || _session.Random.NextDouble() < caster.Stats.Crit;
				var amount = DamageFormula.Compute(caster, target, effect.Power * hit.Scale, effect.IgnoreDefense, crit);
				var absorbed = Absorb(target, amount);
				var dealt = amount - absorbed;
				target.Health = Math.Max(0, target.Health - dealt);
				hit.Dealt[target] = hit.Dealt.GetValueOrDefault(target) + dealt;
				_session.Emit(new Damaged(target, (int)dealt, (int)absorbed, crit, element));

				var drain = effect.Drain + caster.RuneEffects.Drain;
				if (drain > 0)
					Heal(caster, drain * amount);

				if (!target.IsAlive)
				{
					hit.Killed = true;
					_session.KnockOut(target);
					return;
				}

				Nemesis(target, dealt);

				// Desespero: um sorteio por alvo a cada habilidade, que só a Imunidade barra.
				if (caster.RuneEffects.StunChance > 0 && hit.DespairRolled.Add(target))
					ApplyStatus(caster, target, StatusKind.Stun, caster.RuneEffects.StunChance, 1, resistible: false);
			}
		}

		/// <summary>Nêmesis: Ímpeto a cada 7% da Vida máxima perdida neste golpe.</summary>
		private void Nemesis(BattleUnit target, double dealt)
		{
			if (target.RuneEffects.NemesisGauge <= 0 || dealt <= 0)
				return;

			var steps = Math.Floor(dealt / (RuneSets.NemesisStep * target.MaxHealth));
			if (steps > 0)
				GainImpeto(target, steps * target.RuneEffects.NemesisGauge * BattleRules.FullImpeto);
		}
		private static void ExtraTurnAvailability(BattleUnit caster)
		{
			if (caster.RuneEffects.ExtraTurnChance <= 0)
				return;
			caster.ExtraTurnAvailable = true;
		}

		/// <summary>Destruição: 30% do dano de cada alvo vira Vida máxima perdida, até o teto por habilidade e o limite total.</summary>
		private void Destroy(BattleUnit caster, Hit hit)
		{
			if (caster.RuneEffects.DestroyCap <= 0)
				return;

			foreach (var (target, dealt) in hit.Dealt)
			{
				if (!target.IsAlive)
					continue;

				var room = RuneSets.DestroyLimit * target.Stats.Health - target.HealthDestroyed;
				var amount = Math.Round(Math.Min(Math.Min(RuneSets.DestroyShare * dealt, caster.RuneEffects.DestroyCap * target.Stats.Health), room));
				if (amount <= 0)
					continue;

				target.HealthDestroyed += amount;
				target.Health = Math.Min(target.Health, target.MaxHealth);
				_session.Emit(new MaxHealthReduced(target, (int)amount));
			}
		}

		/// <summary>Vingança: cada alvo atingido que sobreviveu pode revidar com o básico, a 75% do dano.</summary>
		private void Counterattacks(BattleUnit attacker, Hit hit)
		{
			foreach (var target in hit.Dealt.Keys.ToList())
			{
				if (!attacker.IsAlive || !target.IsAlive || target.Side == attacker.Side || target.Has(StatusKind.Stun))
					continue;
				if (target.RuneEffects.CounterChance <= 0 || _session.Random.NextDouble() >= target.RuneEffects.CounterChance)
					continue;

				_session.Emit(new Counterattack(target));
				Resolve(target, target.Basic.Effects, attacker, counter: true);
			}
		}

		private static double Absorb(BattleUnit target, double amount)
		{
			var shield = target.Find(StatusKind.Shield);
			if (shield == null)
				return 0;

			var absorbed = Math.Min(shield.Value, amount);
			shield.Value -= absorbed;
			if (shield.Value <= 0)
				target.RemoveStatus(shield);
			return absorbed;
		}

		private void Heal(BattleUnit target, double amount)
		{
			if (!target.IsAlive)
				return;

			var healed = Math.Min(target.MaxHealth - target.Health, Math.Round(amount));
			if (healed <= 0)
				return;

			target.Health += healed;
			_session.Emit(new Healed(target, (int)healed));
		}

		private void ApplyStatus(BattleUnit caster, BattleUnit target, StatusKind status, double chance, int turns, bool resistible = true)
		{
			if (!target.IsAlive)
				return;

			var roll = _session.Random.NextDouble();
			if (roll >= chance)
				return;

			if (BattleRules.IsNegative(status) && target.Side != caster.Side)
			{
				if (target.Has(StatusKind.Immunity))
				{
					_session.Emit(new Immune(target));
					return;
				}

				if (resistible && _session.Random.NextDouble() < BattleRules.ResistChance(target.Stats, caster.Stats))
				{
					_session.Emit(new Resisted(target));
					return;
				}
			}

			var source = status == StatusKind.Taunt ? caster : null;
			var existing = target.Find(status);
			if (status == StatusKind.Burn && target.Count(StatusKind.Burn) < BattleRules.MaxBurnStacks)
				existing = null;

			if (existing == null)
			{
				target.AddStatus(new StatusEffect(status, turns, 0, source) { Fresh = IsActing(target) });
			}
			else
			{
				existing.Turns = Math.Max(existing.Turns, turns);
				existing.Source = source ?? existing.Source;
			}

			_session.Emit(new StatusApplied(target, status, turns));
		}

		private void PushImpeto(BattleUnit caster, BattleUnit target, EffectDefinition effect)
		{
			if (!target.IsAlive)
				return;

			// Atrasar inimigo passa pela Resistência, como qualquer efeito negativo (a Imunidade não barra).
			if (effect.Power < 0 && target.Side != caster.Side)
			{
				if (_session.Random.NextDouble() >= effect.Chance || _session.Random.NextDouble() < BattleRules.ResistChance(target.Stats, caster.Stats))
				{
					_session.Emit(new Resisted(target));
					return;
				}
			}

			GainImpeto(target, effect.Power);
		}

		private void GainImpeto(BattleUnit target, double amount)
		{
			var before = target.Impeto;
			target.Impeto = Math.Clamp(target.Impeto + amount, 0, BattleRules.FullImpeto);
			if (target.Impeto != before)
				_session.Emit(new ImpetoChanged(target, target.Impeto - before));
		}

		private void Cleanse(BattleUnit target)
		{
			var negative = target.Statuses.FirstOrDefault(s => BattleRules.IsNegative(s.Kind));
			if (negative == null)
				return;

			target.RemoveStatus(negative);
			_session.Emit(new StatusRemoved(target, negative.Kind));
		}

		private bool IsActing(BattleUnit unit) => ReferenceEquals(_session.Current, unit);

		/// <summary>O que uma habilidade fez até agora: dano por alvo, quedas e sorteios do Desespero.</summary>
		private sealed class Hit
		{
			public Hit(double scale)
			{
				Scale = scale;
			}

			/// <summary>Multiplica o dano: 0,75 no contra-ataque da Vingança.</summary>
			public double Scale { get; }

			public bool Killed { get; set; }
			public Dictionary<BattleUnit, double> Dealt { get; } = new();
			public HashSet<BattleUnit> DespairRolled { get; } = new();
		}
	}
}
