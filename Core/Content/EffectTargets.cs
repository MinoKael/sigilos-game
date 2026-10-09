using System.Collections.Generic;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Em quem cada <see cref="EffectKind"/> faz sentido. Um tipo fora da tabela aceita qualquer alvo
	/// (Ímpeto, Alterar duração). O Family Builder
	/// (docs/summon_family_builder.html, EFFECT_DOCS) repete esta tabela: mudou aqui, muda lá.
	/// </summary>
	public static class EffectTargets
	{
		/// <summary>Dano, cura, escudo, efeito e Purificação: os alvos de sempre, menos o aliado ao acaso.</summary>
		private static readonly TargetKind[] Usual =
		{
			TargetKind.Target, TargetKind.AllEnemies, TargetKind.Self, TargetKind.LowestAlly, TargetKind.AllAllies,
			TargetKind.HighestAlly, TargetKind.RandomEnemy,
		};

		private static readonly Dictionary<EffectKind, TargetKind[]> Table = new()
		{
			[EffectKind.Damage] = Usual,
			[EffectKind.Heal] = Usual,
			[EffectKind.Shield] = Usual,
			[EffectKind.Status] = Usual,
			[EffectKind.Cleanse] = Usual,
			[EffectKind.StealBuff] = new[] { TargetKind.Target, TargetKind.AllEnemies, TargetKind.RandomEnemy },
			[EffectKind.BonusPerStatus] = new[] { TargetKind.Target, TargetKind.AllEnemies, TargetKind.Self, TargetKind.LowestAlly, TargetKind.AllAllies, TargetKind.HighestAlly },
			[EffectKind.EqualizeHealth] = new[] { TargetKind.AllAllies },
			[EffectKind.HealTeam] = new[] { TargetKind.AllAllies, TargetKind.LowestAlly, TargetKind.Self },
			[EffectKind.JointAttack] = new[] { TargetKind.AllAllies, TargetKind.LowestAlly, TargetKind.HighestAlly, TargetKind.RandomAlly },
			[EffectKind.ExtraTurnOnKill] = new[] { TargetKind.Self },
			[EffectKind.Revive] = new[] { TargetKind.AllAllies },
		};

		public static bool Allows(EffectKind kind, TargetKind target) =>
			!Table.TryGetValue(kind, out var allowed) || System.Array.IndexOf(allowed, target) >= 0;
	}
}
