using System;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Ataque conjunto: quem lança também ataca o alvo com a básica, com o dano inteiro (<see cref="EffectDefinition.OthersOnly"/>
	/// o deixa de fora, para a habilidade que já tem o ataque dele), e junto vão <see cref="EffectDefinition.Count"/> aliados de pé entre os alvos do efeito (0 =
	/// todos; o alvo escolhe entre os aliados sem quem lança: LowestAlly, HighestAlly, RandomAlly), sorteados, atacam com a básica o alvo da habilidade, com o dano inteiro; quem perde o turno
	/// (atordoado, dormindo) não vem. A básica de quem foi chamado não chama outros, e um contra-ataque não
	/// chama ninguém. O que eles derrubam conta para a habilidade (o turno extra ao derrubar, os efeitos
	/// "ao derrubar").
	/// </summary>
	internal sealed class JointAttackEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			if (cast.IsJoint || cast.IsCounter)
				return;

			var resolver = cast.Resolver;
			if (!effect.OthersOnly && cast.Caster.IsAlive && cast.Main is { IsAlive: true } main)
				cast.Killed |= resolver.JointAttack(cast.Caster, main).Killed;

			var ranked = effect.Target is TargetKind.LowestAlly or TargetKind.HighestAlly;

			// Mais forte ou mais fraco: com count, os tantos primeiros do ranking (sem sorteio); nos outros alvos, sorteados.
			var pool = ranked ? cast.Targets(TargetKind.AllAllies, effect.By, exceptCaster: true) : cast.Targets(effect.Target, effect.By, exceptCaster: true);
			var able = pool
				.Where(ally => ally != cast.Caster && ally.IsAlive && ally.Side == cast.Caster.Side && !ally.Any(behavior => behavior.SkipsTurn));
			var callers = (ranked
				? effect.Target == TargetKind.HighestAlly ? able.OrderByDescending(ally => Targeting.Rank(ally, effect.By)) : able.OrderBy(ally => Targeting.Rank(ally, effect.By))
				: able.OrderBy(_ => resolver.Random.Next())).ToList();
			if (effect.Count > 0 || ranked)
				callers = callers.Take(Math.Max(1, effect.Count)).ToList();

			foreach (var ally in callers)
			{
				if (!cast.Caster.IsAlive)
					return;

				var joint = resolver.JointAttack(ally, cast.Main);
				cast.Killed |= joint.Killed;
			}
		}
	}
}
