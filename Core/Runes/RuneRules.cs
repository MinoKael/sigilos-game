using System;
using System.Collections.Generic;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Os números das runas num lugar só (GDD, seção 10), no molde de Summoners War com menos sorteio:
	/// melhora até +9 em vez de +15, 3 subatributos em vez de 4, e nenhuma melhora falha.
	///
	/// - Principal: cresce de 25% a 100% do máximo entre +0 e +9, escalado pelas estrelas.
	/// - Subatributo: sorteado entre 50% e 100% do máximo da estrela. Em +3, +6 e +9 um deles, ao
	///   acaso, ganha mais um sorteio.
	/// </summary>
	public static class RuneRules
	{
		public const int Slots = 6;
		public const int MaxLevel = 9;
		public const int MaxGrade = 5;
		public const int SubstatCount = 3;

		public static bool IsMilestone(int level) => level is 3 or 6 or 9;

		/// <summary>Principais possíveis por espaço: 1, 3 e 5 fixos; 2, 4 e 6 variam.</summary>
		public static IReadOnlyList<RuneStat> MainOptions(int slot) => slot switch
		{
			1 => new[] { RuneStat.AttackFlat },
			2 => new[] { RuneStat.Speed, RuneStat.AttackPercent, RuneStat.DefensePercent, RuneStat.HealthPercent },
			3 => new[] { RuneStat.DefenseFlat },
			4 => new[] { RuneStat.Crit, RuneStat.CritDamage, RuneStat.AttackPercent, RuneStat.DefensePercent, RuneStat.HealthPercent },
			5 => new[] { RuneStat.HealthFlat },
			_ => new[] { RuneStat.Resistance, RuneStat.Focus, RuneStat.AttackPercent, RuneStat.DefensePercent, RuneStat.HealthPercent },
		};

		/// <summary>Quanto vale cada estrela em relação à runa de 5 estrelas.</summary>
		public static double GradeScale(int grade) => grade switch
		{
			1 => 0.55,
			2 => 0.65,
			3 => 0.75,
			4 => 0.87,
			_ => 1.0,
		};

		public static double MainValue(RuneStat stat, int grade, int level)
		{
			var value = MainMax(stat) * GradeScale(grade) * (0.25 + 0.75 * Math.Clamp(level, 0, MaxLevel) / MaxLevel);
			return Round(stat, value);
		}

		public static double RollSubstat(Random random, RuneStat stat, int grade) =>
			Round(stat, SubstatMax(stat) * GradeScale(grade) * (0.5 + 0.5 * random.NextDouble()));

		/// <summary>Pó de Sigilo para ir do nível atual ao próximo.</summary>
		public static int UpgradeCost(Rune rune) => 15 * rune.Grade * (rune.Level + 1);

		/// <summary>Pó de Sigilo para refazer um subatributo.</summary>
		public static int RerollCost(Rune rune) => 50 * rune.Grade;

		/// <summary>Pó de Sigilo que a runa rende ao ser desfeita.</summary>
		public static int SellValue(Rune rune) => 20 * rune.Grade + 10 * rune.Level;

		/// <summary>Somada como número (verdadeiro) ou como fração (falso).</summary>
		public static bool IsFlat(RuneStat stat) => stat is RuneStat.HealthFlat or RuneStat.AttackFlat or RuneStat.DefenseFlat or RuneStat.Speed;

		private static double MainMax(RuneStat stat) => stat switch
		{
			RuneStat.HealthFlat => 1650,
			RuneStat.AttackFlat => 110,
			RuneStat.DefenseFlat => 110,
			RuneStat.Speed => 25,
			RuneStat.Crit => 0.30,
			RuneStat.CritDamage => 0.45,
			RuneStat.Resistance => 0.35,
			RuneStat.Focus => 0.35,
			_ => 0.40,
		};

		private static double SubstatMax(RuneStat stat) => stat switch
		{
			RuneStat.HealthFlat => 300,
			RuneStat.AttackFlat => 20,
			RuneStat.DefenseFlat => 20,
			RuneStat.Speed => 6,
			RuneStat.Crit => 0.06,
			RuneStat.CritDamage => 0.07,
			_ => 0.08,
		};

		private static double Round(RuneStat stat, double value) => IsFlat(stat) ? Math.Max(1, Math.Round(value)) : Math.Round(value, 3);
	}
}
