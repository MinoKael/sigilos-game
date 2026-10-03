using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Roubar efeito: com <see cref="EffectDefinition.Chance"/>, tira de cada alvo um efeito positivo — o
	/// mais novo; só o <see cref="EffectDefinition.OnlyStatus"/>, se há — e o põe em quem lança, com os
	/// turnos que sobravam. Sem nenhum para roubar e com <see cref="EffectDefinition.Fallback"/>, rouba
	/// <see cref="EffectDefinition.Power"/> pontos de Ímpeto (0 = todo o Ímpeto do alvo).
	/// </summary>
	internal sealed class StealBuffEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var resolver = cast.Resolver;
			var thief = cast.Caster;
			foreach (var target in cast.Targets(effect.Target))
			{
				if (!target.IsAlive || target == thief || resolver.Random.NextDouble() >= effect.Chance)
					continue;

				var buff = StatusFilter.Of(target, effect, StatusScope.Buffs).LastOrDefault();
				if (buff != null)
				{
					resolver.Steal(buff, thief);
					continue;
				}

				if (!effect.Fallback || target.Impeto <= 0)
					continue;

				var taken = effect.Power > 0 ? Math.Min(effect.Power, target.Impeto) : target.Impeto;
				resolver.GainImpeto(target, -taken);
				resolver.GainImpeto(thief, taken);
			}
		}
	}
}
