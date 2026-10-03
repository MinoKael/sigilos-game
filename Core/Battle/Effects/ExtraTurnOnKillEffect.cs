using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Effects
{
	/// <summary>
	/// Turno extra ao derrubar: se a habilidade derrubou alguém até aqui (o efeito vem depois do dano na
	/// lista), quem lança age de novo em seguida e a recarga desta habilidade fica
	/// <see cref="EffectDefinition.Turns"/> turnos menor. Uma vez por habilidade, mesmo com várias quedas
	/// ou o efeito repetido; nem contra-ataque nem a básica de um ataque conjunto ganham turno.
	/// </summary>
	internal sealed class ExtraTurnOnKillEffect : SkillEffect
	{
		public override void Apply(Cast cast, EffectDefinition effect)
		{
			if (!cast.Killed || cast.ExtraTurnGranted || cast.IsCounter || cast.IsJoint || !cast.Caster.IsAlive)
				return;

			cast.ExtraTurnGranted = true;
			cast.CooldownReduction += effect.Turns;
			cast.Resolver.GrantExtraTurn(cast.Caster);
		}
	}
}
