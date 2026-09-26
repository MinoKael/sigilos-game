using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Content
{
	/// <summary>
	/// Uma habilidade de invocação ou de inimigo. O aprimoramento é a versão inteira da habilidade
	/// quando se paga <see cref="EnhanceCost"/> de Éter: a lista de efeitos troca, não soma. Assim o
	/// arquivo de dados diz exatamente o que acontece nos dois casos. Só a habilidade especial tem
	/// aprimoramento: o básico nunca gasta Éter.
	/// </summary>
	public sealed record SkillDefinition
	{
		public string Name { get; init; } = "";

		/// <summary>Turnos de recarga. O básico tem 0.</summary>
		public int Cooldown { get; init; }

		public IReadOnlyList<EffectDefinition> Effects { get; init; } = new List<EffectDefinition>();

		/// <summary>0 quando a habilidade não tem aprimoramento.</summary>
		public int EnhanceCost { get; init; }

		public IReadOnlyList<EffectDefinition> EnhancedEffects { get; init; } = new List<EffectDefinition>();

		public bool CanEnhance => EnhanceCost > 0 && EnhancedEffects.Count > 0;

		/// <summary>A habilidade pede que se escolha um inimigo.</summary>
		public bool NeedsTarget => Effects.Any(e => e.Target == TargetKind.Target);

		public IReadOnlyList<EffectDefinition> EffectsFor(bool enhanced) => enhanced && CanEnhance ? EnhancedEffects : Effects;
	}
}
