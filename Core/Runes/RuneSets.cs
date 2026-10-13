using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Os 16 conjuntos (GDD, seção 10), cada um com o seu planeta. Com 6 espaços cabem um de 4 e um de 2,
	/// ou três de 2; três conjuntos iguais de 2 peças valem três vezes. Atributo absoluto (Vida, Ataque,
	/// Defesa, Velocidade) cresce em fração do atributo de base.
	/// </summary>
	public static class RuneSets
	{
		/// <summary>Turnos do escudo do conjunto Baluarte.</summary>
		public const int ShieldTurns = 3;

		/// <summary>Contragolpe: o contra-ataque causa esta fração do dano do básico.</summary>
		public const double CounterDamage = 0.75;

		/// <summary>Perdição: a cada tanta fração da Vida máxima perdida num golpe, o Ímpeto sobe.</summary>
		public const double BaneStep = 0.07;

		/// <summary>Oblívio: fração do dano causado que vira Vida máxima perdida pelo alvo.</summary>
		public const double DestroyShare = 0.30;

		/// <summary>Oblívio: a Vida máxima do alvo nunca cai mais do que isto no total.</summary>
		public const double DestroyLimit = 0.60;

		public static readonly IReadOnlyList<RuneSetDefinition> All = new[]
		{
			new RuneSetDefinition(RuneSet.Vigor,	Planet.Earth,		2, Stat.Health,		0.15, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Haste,	Planet.Mercury,	4, Stat.Speed,		0.25, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Ward,		null,	2, Stat.Defense,	0.15, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Bulwark,	null,	2, null,			0.15, RuneSetEffect.AllyShield),
			new RuneSetDefinition(RuneSet.Finesse,	Planet.Saturn,	2, Stat.Accuracy,	0.20, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Strike,	Planet.Neptune,	2, Stat.Crit,		0.12, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Frenzy,	null,	4, null,			0.30, RuneSetEffect.ExtraTurn),
			new RuneSetDefinition(RuneSet.Counter,	Planet.Venus, 2, null,			0.15, RuneSetEffect.Counter),
			new RuneSetDefinition(RuneSet.Torment,	null,		4, null,			0.25, RuneSetEffect.Stun),
			new RuneSetDefinition(RuneSet.Tenacity,	null,	2, null,			1,	  RuneSetEffect.Immunity),
			new RuneSetDefinition(RuneSet.Wrath,	Planet.Jupiter,	4, Stat.CritDamage, 0.40, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Lethal,	Planet.Mars,	4, Stat.Attack,		0.35, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Sustain,	Planet.Uranus,		2, Stat.Resistance, 0.20, RuneSetEffect.None),
			new RuneSetDefinition(RuneSet.Bane,		null,	2, null,			0.04, RuneSetEffect.Bane),
			new RuneSetDefinition(RuneSet.Siphon,	null,	4, null,			0.35, RuneSetEffect.Drain),
			new RuneSetDefinition(RuneSet.Oblivion,	null,	2, null,			0.04, RuneSetEffect.Oblivion),
		};

		/// <summary>
		/// Os conjuntos da versão 0.1: Vida, Velocidade, Precisão, Crítico, Contragolpe, Dano Crítico, Ataque e
		/// Resistência (Vigor, Pressa, Sutileza, Agredir, Contragolpe, Fúria, Letal e Amparo). Os outros oito
		/// (Baluarte, Vigia, Tormento, Tenacidade, Frenesi, Perdição, Sifão, Oblívio) seguem em <see cref="All"/>
		/// e nos dados, mas saem do jogo: não caem, não aparecem nas telas e não somam bônus. Para trazer um
		/// de volta, é só pôr o conjunto aqui.
		/// </summary>
		public static readonly IReadOnlyList<RuneSet> ActiveSets = new[]
		{
			RuneSet.Vigor, RuneSet.Haste, RuneSet.Finesse, RuneSet.Strike,
			RuneSet.Counter, RuneSet.Wrath, RuneSet.Lethal, RuneSet.Sustain,
		};

		/// <summary>As definições dos conjuntos ativos, na ordem de <see cref="All"/>.</summary>
		public static IReadOnlyList<RuneSetDefinition> Active { get; } = All.Where(s => ActiveSets.Contains(s.Set)).ToList();

		public static bool IsActive(RuneSet set) => ActiveSets.Contains(set);

		/// <summary>
		/// O planeta do Sigilo de cada conjunto em jogo: Vida/Terra, Velocidade/Mercúrio, Precisão/Saturno,
		/// Crítico/Netuno, Contragolpe/Vênus, Dano Crítico/Júpiter, Ataque/Marte, Resistência/Urano. Conjunto
		/// fora do jogo não tem planeta (nulo).
		/// </summary>
		public static Planet? PlanetOf(RuneSet set) => For(set).Planet;

		/// <summary>
		/// O planeta de um atributo: o mesmo do conjunto que o aumenta; a Defesa, que não tem conjunto na
		/// versão 0.1, leva o Sol.
		/// </summary>
		public static Planet PlanetOf(Stat stat) => stat switch
		{
			Stat.Health => Planet.Earth,
			Stat.Speed => Planet.Mercury,
			Stat.Accuracy => Planet.Saturn,
			Stat.Crit => Planet.Neptune,
			Stat.CritDamage => Planet.Jupiter,
			Stat.Attack => Planet.Mars,
			Stat.Resistance => Planet.Uranus,
			_ => Planet.Sun,
		};

		public static RuneSetDefinition For(RuneSet set) => All.First(s => s.Set == set);

	}
}
