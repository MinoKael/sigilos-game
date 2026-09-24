using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Tudo o que as runas equipadas dão: atributos (já em números, para somar à base), efeitos de
	/// combate e os conjuntos completos, para a tela mostrar.
	/// </summary>
	public sealed record RuneBonus(StatBlock Stats, RuneSetEffects Effects, IReadOnlyList<RuneSetDefinition> ActiveSets)
	{
		public static readonly RuneBonus None = new(new StatBlock(), RuneSetEffects.None, new List<RuneSetDefinition>());
	}
}
