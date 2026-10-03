namespace Sigilos.Core.Content
{
	/// <summary>O que um efeito faz. Toda habilidade é uma lista destes (Data/summons, Data/enemies).</summary>
	public enum EffectKind
	{
		Damage,
		Heal,
		Shield,
		Status,
		Impeto,
		Cleanse,

		/// <summary>Rouba um efeito positivo do alvo; sem nenhum, rouba Ímpeto se <see cref="EffectDefinition.Fallback"/>.</summary>
		StealBuff,

		/// <summary>Um bônus para cada efeito contado em <see cref="EffectDefinition.From"/>.</summary>
		BonusPerStatus,

		/// <summary>Aumenta ou diminui a duração dos efeitos do alvo.</summary>
		ChangeDuration,

		/// <summary>Nivela a Vida dos alvos: todos ficam com a mesma fração da Vida máxima.</summary>
		EqualizeHealth,

		/// <summary>Cura uma fração da Vida máxima dos <see cref="EffectDefinition.Count"/> alvos mais feridos.</summary>
		HealTeam,

		/// <summary>Aliados atacam junto, cada um com a básica, no alvo da habilidade.</summary>
		JointAttack,

		/// <summary>Se a habilidade derrubou alguém: turno extra e menos recarga nela.</summary>
		ExtraTurnOnKill,
	}
}
