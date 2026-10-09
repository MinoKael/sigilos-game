using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// A conta dos efeitos que têm valor (docs/COMBATE.md, "Em que atributo a habilidade escala"): power ×
	/// o atributo do efeito (o de sempre do tipo, ou <see cref="EffectDefinition.Stat"/>), mais cada termo de
	/// <see cref="EffectDefinition.Plus"/>, vezes <see cref="EffectDefinition.Factor"/> e
	/// <see cref="EffectDefinition.Speed"/>. A conta na luta é a de Battle/EffectAmount; aqui fica o que
	/// cada tipo lê, para a validação, os filtros e o texto da habilidade.
	/// </summary>
	public static class EffectScaling
	{
		/// <summary>O atributo de sempre de cada tipo que tem conta: o dano no Ataque, a cura na Vida do alvo, o escudo na Vida de quem lança.</summary>
		private static readonly Dictionary<EffectKind, ScaleStat> Usual = new()
		{
			[EffectKind.Damage] = ScaleStat.Attack,
			[EffectKind.Heal] = ScaleStat.TargetMaxHealth,
			[EffectKind.HealTeam] = ScaleStat.TargetMaxHealth,
			[EffectKind.Shield] = ScaleStat.MaxHealth,
			[EffectKind.Revive] = ScaleStat.TargetMaxHealth,
		};

		/// <summary>O tipo tem conta: lê stat, plus, factor e speed.</summary>
		public static bool Scales(EffectKind kind) => Usual.ContainsKey(kind);

		/// <summary>O atributo do power: o do campo ou o de sempre do tipo.</summary>
		public static ScaleStat MainStat(EffectDefinition effect) => effect.Stat ?? Usual[effect.Kind];

		/// <summary>Os atributos que a conta do efeito lê (o do power e os de plus); nenhum, se o tipo não tem conta.</summary>
		public static IEnumerable<ScaleStat> StatsOf(EffectDefinition effect) => Scales(effect.Kind)
			? effect.Plus.Select(term => term.Stat).Prepend(MainStat(effect)).Distinct()
			: Enumerable.Empty<ScaleStat>();

		/// <summary>A conta vai além do de sempre (outro atributo, um termo ou um fator): o texto mostra a conta inteira.</summary>
		public static bool IsCustom(EffectDefinition effect) =>
			effect.Stat != null || effect.Plus.Count > 0 || effect.Factor != null || effect.Speed != null;
	}
}
