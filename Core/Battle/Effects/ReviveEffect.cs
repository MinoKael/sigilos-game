using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Revive: os aliados caídos voltam na hora, com a conta do efeito (a de sempre: <see cref="EffectDefinition.Power"/>
	/// da Vida máxima de cada um), mais o bônus de cura da habilidade. Com <see cref="EffectDefinition.Count"/>,
	/// só os tantos sorteados. Quem já ia voltar sozinho no turno dele (uma Passiva) fica de fora.
	/// </summary>
	internal sealed class ReviveEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var resolver = cast.Resolver;
			var fallen = cast.Caster.Team
				.Where(unit => !unit.IsAlive && !unit.Reviving)
				.OrderBy(_ => resolver.Random.Next())
				.ToList();
			if (effect.Count > 0)
				fallen = fallen.Take(effect.Count).ToList();

			var bonus = 1 + cast.HealBonus;
			foreach (var unit in fallen)
				resolver.Revive(unit, EffectAmount.Of(effect, effect.Power * bonus, bonus, cast.Caster, unit));
		}
	}
}
