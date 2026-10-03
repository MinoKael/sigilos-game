using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Que efeitos de status um efeito de habilidade olha: os de <see cref="EffectDefinition.Scope"/>
	/// (positivos, negativos ou todos) e, se há, só o <see cref="EffectDefinition.OnlyStatus"/>.
	/// </summary>
	internal static class StatusFilter
	{
		public static bool Matches(StatusEffect status, EffectDefinition effect, StatusScope? scope = null)
		{
			if (effect.OnlyStatus is { } only && status.Kind != only)
				return false;

			var harmful = BattleRules.IsNegative(status.Kind);
			return (scope ?? effect.Scope) switch
			{
				StatusScope.Buffs => !harmful,
				StatusScope.Debuffs => harmful,
				_ => true,
			};
		}

		/// <summary>Os efeitos da unidade que contam, numa cópia (quem usa pode tirar ou mexer neles).</summary>
		public static List<StatusEffect> Of(BattleUnit unit, EffectDefinition effect, StatusScope? scope = null) =>
			unit.Statuses.Where(status => Matches(status, effect, scope)).ToList();
	}
}
