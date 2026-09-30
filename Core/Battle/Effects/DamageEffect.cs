using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Dano: <see cref="EffectDefinition.Hits"/> golpes de <see cref="EffectDefinition.Power"/> do Ataque.
	/// Os golpes vão em rodadas — cada um acerta todos os alvos antes do próximo —, então um golpe em
	/// área com 2 golpes é "todos, depois todos" (e a tela mostra cada rodada de uma vez).
	///
	/// O alvo de cada efeito é fechado antes do primeiro golpe: quem cai no meio só deixa de apanhar.
	/// </summary>
	internal sealed class DamageEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			var targets = cast.Targets(effect.Target).ToList();
			for (var round = 0; round < effect.Hits; round++)
			{
				foreach (var target in targets.Where(t => t.IsAlive))
				{
					cast.Resolver.Land(new Strike(cast, target, effect.Power * cast.Scale)
					{
						IgnoreDefense = effect.IgnoreDefense,
						Drain = effect.Drain,
					});
				}
			}
		}
	}
}
