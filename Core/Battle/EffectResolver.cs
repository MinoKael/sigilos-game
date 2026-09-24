using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Faz cada <see cref="EffectDefinition"/> acontecer: dano, cura, escudo, efeitos, Ímpeto. Aplica
	/// também o que os conjuntos de runas fazem a cada golpe (dreno da Ossada, atordoar do Laço).
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

		public void Resolve(BattleUnit caster, IReadOnlyList<EffectDefinition> effects, BattleUnit? chosen)
		{
			var allies = _session.SideOf(caster.Side);
			var opponents = _session.SideOf(caster.Side == Side.Allies ? Side.Enemies : Side.Allies);

			// O alvo é escolhido uma vez para a habilidade inteira: se ele cai no primeiro golpe, a
			// Queimadura que vinha depois não pula para outro inimigo.
			var main = Targeting.PickMain(caster, chosen, opponents);
			var killed = false;

			foreach (var effect in effects)
			{
				if (effect.OnKill && !killed)
					continue;

				foreach (var target in Targeting.Resolve(effect.Target, caster, main, allies, opponents))
				{
					switch (effect.Kind)
					{
						case EffectKind.Damage:
							killed |= Damage(caster, target, effect);
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

		/// <summary>Devolve verdadeiro se o alvo caiu.</summary>
		private bool Damage(BattleUnit caster, BattleUnit target, EffectDefinition effect)
		{
			var element = ElementChart.Multiplier(caster.Element, target.Element);

			for (var hit = 0; hit < effect.Hits && target.IsAlive; hit++)
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
				var amount = DamageFormula.Compute(caster, target, effect.Power, effect.IgnoreDefense, crit);
				var absorbed = Absorb(target, amount);
				var dealt = amount - absorbed;
				target.Health = Math.Max(0, target.Health - dealt);
				_session.Emit(new Damaged(target, (int)dealt, (int)absorbed, crit, element));

				var drain = effect.Drain + caster.RuneEffects.Drain;
				if (drain > 0)
					Heal(caster, drain * amount);

				if (!target.IsAlive)
				{
					_session.KnockOut(target);
					return true;
				}

				if (caster.RuneEffects.StunOnHit > 0)
					ApplyStatus(caster, target, StatusKind.Stun, caster.RuneEffects.StunOnHit, 1);
			}

			return false;
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

		private void ApplyStatus(BattleUnit caster, BattleUnit target, StatusKind status, double chance, int turns)
		{
			if (!target.IsAlive)
				return;

			var roll = _session.Random.NextDouble();
			if (roll >= chance)
				return;

			// Resistência do alvo menos o Foco de quem lança: a parte que sobra é a chance de barrar.
			if (BattleRules.IsNegative(status) && target.Side != caster.Side)
			{
				var resisted = Math.Max(0, target.Stats.Resistance - caster.Stats.Focus);
				if (roll >= chance * (1 - resisted))
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

			// Atrasar inimigo passa pela Resistência, como qualquer efeito negativo.
			if (effect.Power < 0 && target.Side != caster.Side)
			{
				var chance = effect.Chance * (1 - Math.Max(0, target.Stats.Resistance - caster.Stats.Focus));
				if (_session.Random.NextDouble() >= chance)
				{
					_session.Emit(new Resisted(target));
					return;
				}
			}

			var before = target.Impeto;
			target.Impeto = Math.Clamp(target.Impeto + effect.Power, 0, BattleRules.FullImpeto);
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
	}
}
