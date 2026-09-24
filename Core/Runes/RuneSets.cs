using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Os 16 conjuntos (GDD, seção 10). Cada Glifo empresta o desenho a
	/// dois conjuntos que combinam com a escola dele. Com 6 espaços cabem um de 4 e um de 2, ou três de
	/// 2; três conjuntos iguais de 2 peças valem três vezes. Atributo absoluto (Vida, Ataque, Defesa,
	/// Velocidade) cresce em fração do atributo de base.
	/// </summary>
	public static class RuneSets
	{
		/// <summary>Turnos do escudo do conjunto Escudo.</summary>
		public const int ShieldTurns = 3;

		/// <summary>Violento: cada turno extra seguido multiplica a chance do próximo por isto (22%, 12,1%, 6,7%...).</summary>
		public const double ExtraTurnDecay = 0.55;

		/// <summary>Vingança: o contra-ataque causa esta fração do dano do básico.</summary>
		public const double CounterDamage = 0.75;

		/// <summary>Nêmesis: a cada tanta fração da Vida máxima perdida num golpe, o Ímpeto sobe.</summary>
		public const double NemesisStep = 0.07;

		/// <summary>Destruição: fração do dano causado que vira Vida máxima perdida pelo alvo.</summary>
		public const double DestroyShare = 0.30;

		/// <summary>Destruição: a Vida máxima do alvo nunca cai mais do que isto no total.</summary>
		public const double DestroyLimit = 0.60;

		public static readonly IReadOnlyList<RuneSetDefinition> All = new[]
		{
			new RuneSetDefinition(RuneSet.Energy, Glyph.Spiral, 2, Stat.Health, 0.15, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Swift, Glyph.Spiral, 4, Stat.Speed, 0.25, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Guard, Glyph.Wall, 2, Stat.Defense, 0.15, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Shield, Glyph.Wall, 2, null, 0.15, RuneSetEffect.AllyShield),
			new RuneSetDefinition(RuneSet.Focus, Glyph.Eye, 2, Stat.Accuracy, 0.20, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Blade, Glyph.Eye, 2, Stat.Crit, 0.12, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Violent, Glyph.Door, 4, null, 0.30, RuneSetEffect.ExtraTurn),
			new RuneSetDefinition(RuneSet.Revenge, Glyph.Door, 2, null, 0.15, RuneSetEffect.Counter),
			new RuneSetDefinition(RuneSet.Despair, Glyph.Bond, 4, null, 0.25, RuneSetEffect.Stun),
			new RuneSetDefinition(RuneSet.Will, Glyph.Bond, 2, null, 1, RuneSetEffect.Immunity),
			new RuneSetDefinition(RuneSet.Rage, Glyph.Shard, 4, Stat.CritDamage, 0.40, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Fatal, Glyph.Shard, 4, Stat.Attack, 0.35, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Endure, Glyph.Veil, 2, Stat.Resistance, 0.20, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Nemesis, Glyph.Veil, 2, null, 0.04, RuneSetEffect.Nemesis),
			new RuneSetDefinition(RuneSet.Vampire, Glyph.Bone, 4, null, 0.35, RuneSetEffect.Drain),
			new RuneSetDefinition(RuneSet.Destroy, Glyph.Bone, 2, null, 0.04, RuneSetEffect.Destroy),
		};

		public static RuneSetDefinition For(RuneSet set) => All.First(s => s.Set == set);

		public static IEnumerable<RuneSetDefinition> Of(Glyph glyph) => All.Where(s => s.Glyph == glyph);
	}
}
