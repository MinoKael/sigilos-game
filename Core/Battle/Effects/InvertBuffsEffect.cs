using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Inverter efeitos: com <see cref="EffectDefinition.Chance"/>, cada efeito positivo removível do alvo vira o equivalente
	/// negativo (ou Estigma, sem equivalente), com os mesmos turnos. Sem nenhum efeito positivo e com
	/// <see cref="EffectDefinition.Fallback"/>, tira <see cref="EffectDefinition.Power"/> pontos de Ímpeto (0 = todo).
	/// </summary>
	internal sealed class InvertBuffsEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var resolver = cast.Resolver;
			foreach (var target in cast.Targets(effect))
			{
				if (!target.IsAlive || resolver.Random.NextDouble() >= effect.Chance)
					continue;

				var buffs = StatusFilter.Of(target, effect, StatusScope.Buffs).Where(status => !status.Unremovable).ToList();
				resolver.Invert(cast.Caster, target, buffs);

				if (buffs.Count > 0 || !effect.Fallback || target.Impeto <= 0)
					continue;

				resolver.GainImpeto(target, -(effect.Power > 0 ? Math.Min(effect.Power, target.Impeto) : target.Impeto));
			}
		}
	}
}
