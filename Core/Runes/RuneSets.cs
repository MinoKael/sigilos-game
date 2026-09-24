using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Os oito conjuntos, um por Glifo (GDD, seção 10). Com 6 espaços cabem um de 4 e um de 2, ou três
	/// de 2. Atributo absoluto (Defesa, Velocidade) cresce em fração do atributo de base.
	/// </summary>
	public static class RuneSets
	{
		public static readonly IReadOnlyList<RuneSetDefinition> All = new[]
		{
			new RuneSetDefinition(Glyph.Spiral, 4, Stat.Speed, 0.25, RuneSetEffect.None),
			new RuneSetDefinition(Glyph.Door, 4, null, 0.20, RuneSetEffect.ExtraTurn),
			new RuneSetDefinition(Glyph.Bond, 4, null, 0.25, RuneSetEffect.StunOnHit),
			new RuneSetDefinition(Glyph.Shard, 4, Stat.CritDamage, 0.40, RuneSetEffect.None),
			new RuneSetDefinition(Glyph.Bone, 4, null, 0.35, RuneSetEffect.Drain),
			new RuneSetDefinition(Glyph.Wall, 2, Stat.Defense, 0.15, RuneSetEffect.None),
			new RuneSetDefinition(Glyph.Eye, 2, Stat.Focus, 0.20, RuneSetEffect.None),
			new RuneSetDefinition(Glyph.Veil, 2, Stat.Resistance, 0.20, RuneSetEffect.None),
		};

		public static RuneSetDefinition For(Glyph set) => All.First(s => s.Set == set);
	}
}
