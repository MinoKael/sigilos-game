using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>A estratégia de cada <see cref="EffectKind"/>.</summary>
	internal static class SkillEffects
	{
		private static readonly Dictionary<EffectKind, SkillEffect> Table = new()
		{
			[EffectKind.Damage] = new DamageEffect(),
			[EffectKind.Heal] = new HealEffect(),
			[EffectKind.Shield] = new ShieldEffect(),
			[EffectKind.Status] = new InflictEffect(),
			[EffectKind.Impeto] = new ImpetoEffect(),
			[EffectKind.Cleanse] = new CleanseEffect(),
			[EffectKind.StealBuff] = new StealBuffEffect(),
			[EffectKind.BonusPerStatus] = new BonusPerStatusEffect(),
			[EffectKind.ChangeDuration] = new ChangeDurationEffect(),
			[EffectKind.EqualizeHealth] = new EqualizeHealthEffect(),
			[EffectKind.HealTeam] = new HealEffect(),
			[EffectKind.JointAttack] = new JointAttackEffect(),
			[EffectKind.ExtraTurnOnKill] = new ExtraTurnOnKillEffect(),
		};

		public static SkillEffect Of(EffectKind kind) => Table[kind];
	}
}
