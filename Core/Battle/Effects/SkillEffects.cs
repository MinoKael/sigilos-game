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
		};

		public static SkillEffect Of(EffectKind kind) => Table[kind];
	}
}
