using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Em quem cada <see cref="EffectKind"/> faz sentido. Um tipo fora da tabela aceita qualquer alvo (os
	/// de sempre: dano, cura, escudo, efeito, Ímpeto, Purificação). O Family Builder
	/// (docs/summon_family_builder.html, EFFECT_DOCS) repete esta tabela: mudou aqui, muda lá.
	/// </summary>
	public static class EffectTargets
	{
		private static readonly Dictionary<EffectKind, TargetKind[]> Table = new()
		{
			[EffectKind.StealBuff] = new[] { TargetKind.Target, TargetKind.AllEnemies },
			[EffectKind.EqualizeHealth] = new[] { TargetKind.AllAllies },
			[EffectKind.HealTeam] = new[] { TargetKind.AllAllies, TargetKind.LowestAlly, TargetKind.Self },
			[EffectKind.JointAttack] = new[] { TargetKind.AllAllies },
			[EffectKind.ExtraTurnOnKill] = new[] { TargetKind.Self },
		};

		public static bool Allows(EffectKind kind, TargetKind target) =>
			!Table.TryGetValue(kind, out var allowed) || System.Array.IndexOf(allowed, target) >= 0;
	}
}
