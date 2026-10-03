using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Ataque conjunto: <see cref="EffectDefinition.Count"/> aliados de pé entre os alvos do efeito (0 =
	/// todos), sorteados, atacam com a básica o alvo da habilidade, com o dano inteiro; quem perde o turno
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
			var callers = cast.Targets(effect.Target)
				.Where(ally => ally != cast.Caster && ally.IsAlive && ally.Side == cast.Caster.Side && !ally.Any(behavior => behavior.SkipsTurn))
				.OrderBy(_ => resolver.Random.Next())
				.ToList();
			if (effect.Count > 0)
				callers = callers.Take(effect.Count).ToList();

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
