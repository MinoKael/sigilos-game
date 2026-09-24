using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Faz cada <see cref="EffectDefinition"/> acontecer: dano, cura, escudo, efeitos, Ímpeto. Serve
	/// igual para habilidades e páginas — o <see cref="Caster"/> esconde a diferença.
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

		public void Resolve(Caster caster, IReadOnlyList<EffectDefinition> effects, BattleUnit? chosen)
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
							var basis = caster.Unit?.MaxHealth ?? target.MaxHealth;
							GiveShield(target, effect.Power * basis * caster.SkillPower, effect.Turns);
							break;
						case EffectKind.Status:
							ApplyStatus(caster, target, effect);
							break;
						case EffectKind.Impeto:
							PushImpeto(caster, target, effect);
							break;
						case EffectKind.Cleanse:
							Cleanse(target);
							break;
						case EffectKind.Revive:
							Revive(target, effect.Power);
							break;
					}
				}

				if (effect.Kind == EffectKind.Damage && caster.Unit?.Find(StatusKind.Foresight) is { } foresight)
				{
					caster.Unit.RemoveStatus(foresight);
					_session.Emit(new StatusRemoved(caster.Unit, StatusKind.Foresight));
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
		private bool Damage(Caster caster, BattleUnit target, EffectDefinition effect)
		{
			var element = caster.Element is { } attacker ? ElementChart.Multiplier(attacker, target.Element) : 1;

			for (var hit = 0; hit < effect.Hits && target.IsAlive; hit++)
			{
				if (caster.Unit?.Has(StatusKind.Blind) == true && _session.Random.NextDouble() < BattleRules.BlindMissChance)
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

				var crit = caster.Unit?.Has(StatusKind.Foresight) == true || _session.Random.NextDouble() < caster.Crit;
				var amount = DamageFormula.Compute(caster, target, effect.Power, effect.IgnoreDefense, crit);
				var absorbed = Absorb(target, amount);
				var dealt = amount - absorbed;
				target.Health = Math.Max(0, target.Health - dealt);
				_session.Emit(new Damaged(target, (int)dealt, (int)absorbed, crit, element));

				if (effect.Drain > 0)
					Drain(caster, effect.Drain * amount);

				if (!target.IsAlive)
				{
					_session.KnockOut(target);
					return true;
				}
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

		/// <summary>Quem drena recupera Vida. O Conjurador não tem Vida: o dreno das páginas cura o aliado mais ferido.</summary>
		private void Drain(Caster caster, double amount)
		{
			var receiver = caster.Unit ?? _session.SideOf(Side.Allies).Where(u => u.IsAlive).MinBy(u => u.HealthFraction);
			if (receiver != null)
				Heal(receiver, amount);
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

		private void ApplyStatus(Caster caster, BattleUnit target, EffectDefinition effect)
		{
			if (!target.IsAlive)
				return;

			var roll = _session.Random.NextDouble();
			if (roll >= effect.Chance)
				return;

			if (BattleRules.IsNegative(effect.Status) && target.Side != caster.Side)
			{
				var resisted = Math.Max(0, target.Stats.Resistance - caster.Focus);
				if (roll >= effect.Chance * (1 - resisted))
				{
					_session.Emit(new Resisted(target));
					return;
				}
			}

			// A provocação aponta para quem provocou. Página não tem corpo: aponta para a Líder.
			BattleUnit? source = null;
			if (effect.Status == StatusKind.Taunt)
			{
				source = caster.Unit ?? _session.SideOf(caster.Side).FirstOrDefault(u => u.IsAlive);
				if (source == null)
					return;
			}

			var existing = target.Find(effect.Status);
			if (effect.Status == StatusKind.Burn && target.Count(StatusKind.Burn) < BattleRules.MaxBurnStacks)
				existing = null;

			if (existing == null)
			{
				target.AddStatus(new StatusEffect(effect.Status, effect.Turns, 0, source) { Fresh = IsActing(target) });
			}
			else
			{
				existing.Turns = Math.Max(existing.Turns, effect.Turns);
				existing.Source = source ?? existing.Source;
			}

			_session.Emit(new StatusApplied(target, effect.Status, effect.Turns));
		}

		private void PushImpeto(Caster caster, BattleUnit target, EffectDefinition effect)
		{
			if (!target.IsAlive)
				return;

			// Atrasar inimigo passa pela Resistência, como qualquer efeito negativo.
			if (effect.Power < 0 && target.Side != caster.Side)
			{
				var chance = effect.Chance * (1 - Math.Max(0, target.Stats.Resistance - caster.Focus));
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

		private void Revive(BattleUnit target, double fraction)
		{
			if (target.IsAlive || target.PendingRebirth)
				return;

			target.Health = Math.Max(1, Math.Round(target.MaxHealth * fraction));
			target.Impeto = 0;
			target.ClearStatuses();
			_session.Emit(new Revived(target));
		}

		private bool IsActing(BattleUnit unit) => ReferenceEquals(_session.Current, unit);
	}
}
