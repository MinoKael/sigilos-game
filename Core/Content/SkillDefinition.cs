using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma habilidade de invocação ou de inimigo. Ativa (<see cref="Effects"/>, com <see cref="Cooldown"/>
	/// a partir da segunda) ou passiva (<see cref="Passive"/>, age sozinha e nunca é escolhida).
	///
	/// Cada habilidade sobe de nível com cópias da mesma invocação (<see cref="Levels"/>: o nível 2 é o
	/// primeiro item). Algumas mudam no Despertar: <see cref="AwakenedEffects"/> troca a lista de efeitos
	/// inteira, não soma.
	/// </summary>
	public sealed record SkillDefinition
	{
		public string Name { get; init; } = "";

		/// <summary>Turnos de recarga. A primeira habilidade tem 0.</summary>
		public int Cooldown { get; init; }

		public IReadOnlyList<EffectDefinition> Effects { get; init; } = new List<EffectDefinition>();

		/// <summary>Os efeitos depois do Despertar; vazio quando o Despertar não muda a habilidade.</summary>
		public IReadOnlyList<EffectDefinition> AwakenedEffects { get; init; } = new List<EffectDefinition>();

		public IReadOnlyList<SkillLevelUp> Levels { get; init; } = new List<SkillLevelUp>();

		/// <summary>Só nas passivas: a regra que age sozinha.</summary>
		public PassiveDefinition? Passive { get; init; }

		public bool IsPassive => Passive != null;

		public int MaxLevel => 1 + Levels.Count;

		/// <summary>O Despertar muda esta habilidade (efeitos novos ou passiva mais forte).</summary>
		public bool ChangesOnAwakening => AwakenedEffects.Count > 0 || Passive is { AwakenedValue: > 0 };

		/// <summary>A habilidade pede que se escolha um inimigo.</summary>
		public bool NeedsTarget => Effects.Any(e => e.Target == TargetKind.Target);

		/// <summary>Soma dos bônus de um tipo até o nível pedido.</summary>
		public double Bonus(SkillLevelKind kind, int level) =>
			Levels.Take(Math.Clamp(level, 1, MaxLevel) - 1).Where(l => l.Kind == kind).Sum(l => l.Value);

		/// <summary>
		/// A habilidade como ela luta: no nível pedido, desperta ou não. Dano, cura e chance já vêm com
		/// os bônus de nível; a recarga já vem descontada.
		/// </summary>
		public SkillDefinition At(int level, bool awakened)
		{
			var effects = awakened && AwakenedEffects.Count > 0 ? AwakenedEffects : Effects;
			var damage = 1 + Bonus(SkillLevelKind.Damage, level);
			var recovery = 1 + Bonus(SkillLevelKind.Recovery, level);
			var rate = Bonus(SkillLevelKind.EffectRate, level);
			var cooldown = Cooldown > 0 ? Math.Max(1, Cooldown - (int)Math.Round(Bonus(SkillLevelKind.Cooldown, level))) : 0;

			return this with
			{
				Cooldown = cooldown,
				Effects = effects.Select(e => e.Kind switch
				{
					EffectKind.Damage => e with { Power = e.Power * damage },
					EffectKind.Heal or EffectKind.Shield => e with { Power = e.Power * recovery },
					EffectKind.Status when e.Chance < 1 => e with { Chance = Math.Min(1, e.Chance + rate) },
					_ => e,
				}).ToList(),
				AwakenedEffects = new List<EffectDefinition>(),
			};
		}
	}
}
