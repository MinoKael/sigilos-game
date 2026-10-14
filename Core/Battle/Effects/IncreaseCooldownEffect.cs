using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Aumentar recarga: com <see cref="EffectDefinition.Chance"/>, cada habilidade de cada alvo inimigo, em
	/// recarga ou pronta, ganha <see cref="EffectDefinition.Turns"/> turnos a mais (1 se o efeito não diz): uma
	/// pronta passa de 0 para 1 e só volta a ficar pronta depois deles. A básica não tem recarga. Conta como
	/// efeito negativo: Imunidade barra, a Resistência
	/// do alvo contra a Precisão de quem lança pode resistir, e o chefe é imune (como ao Atordoamento).
	/// </summary>
	internal sealed class IncreaseCooldownEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var resolver = cast.Resolver;
			var turns = effect.Turns > 0 ? effect.Turns : 1;
			foreach (var target in cast.Targets(effect))
			{
				if (!target.IsAlive || target.Side == cast.Caster.Side || target.IsBoss)
					continue;

				if (resolver.Random.NextDouble() >= effect.Chance)
					continue;

				if (target.Any(behavior => behavior.BlocksHarmful) || resolver.Random.NextDouble() < BattleRules.ResistChance(target.Stats, cast.Caster.Stats))
					continue;

				for (var i = 1; i < target.Skills.Count; i++)
				{
					if (!target.Skills[i].IsPassive)
						target.SetCooldown(i, target.Cooldown(i) + turns);
				}
			}
		}
	}
}
