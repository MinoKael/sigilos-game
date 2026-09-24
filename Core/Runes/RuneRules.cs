using System;
using System.Collections.Generic;

namespace Sigilos.Core.Runes
{
	/// <summary>
	/// Os números das runas num lugar só (GDD, seção 10). As tabelas têm uma
	/// linha por estrela, de 1★ a 6★; porcentagens estão em pontos (8 = 8%).
	///
	/// A única regra diferente: a melhora nunca falha. Em troca, cada nível custa o que seria
	/// cobrado <b>em média</b> contando as falhas (custo ÷ chance de sucesso), o que deixa o Pó de Sigilo
	/// tão escasso quanto a sorte deixaria o Mana.
	/// </summary>
	public static class RuneRules
	{
		public const int Slots = 6;
		public const int MaxGrade = 6;
		public const int MaxLevel = 15;
		public const int MaxSubstats = 4;

		/// <summary>Chance de a runa sair com atributo nativo.</summary>
		public const double InnateChance = 0.3;

		/// <summary>Quanto Mana vale 1 Pó de Sigilo.</summary>
		public const int ManaPerDust = 100;

		/// <summary>Chance de cada raridade no drop: quantos subatributos a runa traz.</summary>
		private static readonly double[] RarityWeights = { 0.30, 0.30, 0.22, 0.13, 0.05 };

		/// <summary>+3, +6, +9 e +12 trazem um subatributo novo ou fazem um crescer. +15 só reforça o principal.</summary>
		public static bool IsMilestone(int level) => level is 3 or 6 or 9 or 12;

		/// <summary>Principais possíveis por espaço: 1, 3 e 5 fixos; 2, 4 e 6 variam.</summary>
		public static IReadOnlyList<RuneStat> MainOptions(int slot) => slot switch
		{
			1 => new[] { RuneStat.AttackFlat },
			2 => new[] { RuneStat.AttackFlat, RuneStat.AttackPercent, RuneStat.DefenseFlat, RuneStat.DefensePercent, RuneStat.HealthFlat, RuneStat.HealthPercent, RuneStat.Speed },
			3 => new[] { RuneStat.DefenseFlat },
			4 => new[] { RuneStat.AttackFlat, RuneStat.AttackPercent, RuneStat.DefenseFlat, RuneStat.DefensePercent, RuneStat.HealthFlat, RuneStat.HealthPercent, RuneStat.Crit, RuneStat.CritDamage },
			5 => new[] { RuneStat.HealthFlat },
			_ => new[] { RuneStat.AttackFlat, RuneStat.AttackPercent, RuneStat.DefenseFlat, RuneStat.DefensePercent, RuneStat.HealthFlat, RuneStat.HealthPercent, RuneStat.Resistance, RuneStat.Accuracy },
		};

		/// <summary>
		/// Se o atributo pode ser subatributo (ou nativo) numa runa deste espaço: nunca igual ao principal;
		/// o espaço 1 não aceita Defesa e o 3 não aceita Ataque.
		/// </summary>
		public static bool CanBeSubstat(int slot, RuneStat main, RuneStat stat) =>
			stat != main &&
			!(slot == 1 && stat is RuneStat.DefenseFlat or RuneStat.DefensePercent) &&
			!(slot == 3 && stat is RuneStat.AttackFlat or RuneStat.AttackPercent);

		/// <summary>Somada como número (verdadeiro) ou como fração (falso).</summary>
		public static bool IsFlat(RuneStat stat) => stat is RuneStat.HealthFlat or RuneStat.AttackFlat or RuneStat.DefenseFlat or RuneStat.Speed;

		/// <summary>A Pedra de Afiar só serve em Vida, Ataque, Defesa e Velocidade.</summary>
		public static bool IsGrindable(RuneStat stat) => stat is
			RuneStat.HealthFlat or RuneStat.HealthPercent or
			RuneStat.AttackFlat or RuneStat.AttackPercent or
			RuneStat.DefenseFlat or RuneStat.DefensePercent or
			RuneStat.Speed;

		public static RuneRarity RollRarity(Random random)
		{
			var roll = random.NextDouble();
			for (var i = 0; i < RarityWeights.Length; i++)
			{
				roll -= RarityWeights[i];
				if (roll < 0)
					return (RuneRarity)i;
			}

			return RuneRarity.Normal;
		}

		/// <summary>O principal: base da estrela mais o passo por nível, arredondado para baixo; +15 dá o salto final.</summary>
		public static double MainValue(RuneStat stat, int grade, int level)
		{
			var (start, step, max) = MainTable(stat)[GradeIndex(grade)];
			var points = level >= MaxLevel ? max : Math.Floor(start + step * Math.Max(0, level) + 1e-9);
			return FromPoints(stat, points);
		}

		/// <summary>Faixa de um sorteio de subatributo (no drop e em cada melhora), já em frações ou números.</summary>
		public static (double Min, double Max) SubstatRange(RuneStat stat, int grade) => Scaled(stat, SubstatTable(stat)[GradeIndex(grade)]);

		public static double RollSubstat(Random random, RuneStat stat, int grade) => Roll(random, stat, SubstatTable(stat)[GradeIndex(grade)]);

		/// <summary>Bônus da Pedra de Afiar por grau. Nulo se a pedra não serve no atributo.</summary>
		public static (double Min, double Max)? GrindRange(RuneStat stat, RuneRarity grade) =>
			IsGrindable(stat) ? Scaled(stat, GrindTable(stat)[ToolIndex(grade)]) : null;

		public static double RollGrind(Random random, RuneStat stat, RuneRarity grade) => Roll(random, stat, GrindTable(stat)[ToolIndex(grade)]);

		/// <summary>Valor do subatributo que a Gema Encantada põe, por grau.</summary>
		public static (double Min, double Max) GemRange(RuneStat stat, RuneRarity grade) => Scaled(stat, GemTable(stat)[ToolIndex(grade)]);

		public static double RollGem(Random random, RuneStat stat, RuneRarity grade) => Roll(random, stat, GemTable(stat)[ToolIndex(grade)]);

		/// <summary>Chance de sucesso da melhora para chegar a <paramref name="level"/>.</summary>
		public static double SummonersWarChance(int level) => level switch
		{
			<= 3 => 1.00,
			4 => 0.85,
			5 => 0.70,
			6 => 0.60,
			7 => 0.50,
			8 => 0.40,
			9 => 0.30,
			10 => 0.25,
			11 => 0.20,
			12 => 0.15,
			13 => 0.10,
			14 => 0.08,
			_ => 0.05,
		};

		/// <summary>Pó de Sigilo para ir do nível atual ao próximo.</summary>
		public static int UpgradeCost(Rune rune) => UpgradeCost(rune.Grade, rune.Level);

		public static int UpgradeCost(int grade, int level)
		{
			if (level >= MaxLevel)
				return 0;

			var target = level + 1;
			var mana = ManaCost[target - 1][GradeIndex(grade)];
			return (int)Math.Ceiling(mana / SummonersWarChance(target) / ManaPerDust);
		}

		/// <summary>Pó de Sigilo para ir do nível atual até <paramref name="target"/>.</summary>
		public static int UpgradeCost(Rune rune, int target)
		{
			var total = 0;
			for (var level = rune.Level; level < Math.Min(target, MaxLevel); level++)
				total += UpgradeCost(rune.Grade, level);
			return total;
		}

		/// <summary>Próximo marco (+3, +6, +9, +12 ou +15) acima do nível atual.</summary>
		public static int NextMilestone(int level) => Math.Min(MaxLevel, (level / 3 + 1) * 3);

		/// <summary>Pó de Sigilo que a runa rende ao ser desfeita: pelas estrelas e pela raridade, nunca pela melhora.</summary>
		public static int SellValue(Rune rune)
		{
			var byGrade = 3 << (GradeIndex(rune.Grade));
			return (int)Math.Round(byGrade * (1 + 0.25 * rune.Substats.Count));
		}

		// Principal: (base em +0, passo por nível, valor em +15), de 1★ a 6★.
		private static readonly (double, double, double)[] HealthFlatMain = { (40, 45, 804), (70, 60, 1092), (100, 75, 1380), (160, 90, 1704), (270, 105, 2088), (360, 120, 2448) };
		private static readonly (double, double, double)[] FlatMain = { (3, 3, 54), (5, 4, 73), (7, 5, 92), (10, 6, 112), (15, 7, 135), (22, 8, 160) };
		private static readonly (double, double, double)[] PercentMain = { (1, 1, 18), (2, 1, 19), (4, 2, 38), (5, 2.15, 43), (8, 2.45, 51), (11, 3, 63) };
		private static readonly (double, double, double)[] SpeedMain = { (1, 1, 18), (2, 1, 19), (3, 4.0 / 3, 25), (4, 1.5, 30), (5, 2, 39), (7, 2, 42) };
		private static readonly (double, double, double)[] CritMain = { (1, 1, 18), (2, 1, 19), (3, 2, 37), (4, 2.15, 42), (5, 2.45, 47), (7, 3, 58) };
		private static readonly (double, double, double)[] CritDamageMain = { (2, 1, 19), (3, 2, 37), (4, 2.25, 43), (6, 3, 57), (8, 10.0 / 3, 65), (11, 4, 80) };
		private static readonly (double, double, double)[] ResistanceMain = { (1, 1, 18), (2, 1, 19), (4, 2, 38), (6, 2.15, 44), (9, 2.45, 51), (12, 3, 64) };

		// Subatributo: (mínimo, máximo) de cada sorteio, de 1★ a 6★.
		private static readonly (int, int)[] HealthFlatSub = { (15, 60), (30, 105), (45, 165), (60, 225), (90, 300), (135, 375) };
		private static readonly (int, int)[] FlatSub = { (1, 4), (2, 5), (3, 8), (4, 10), (8, 15), (10, 20) };
		private static readonly (int, int)[] PercentSub = { (1, 2), (1, 3), (2, 5), (3, 6), (4, 7), (5, 8) };
		private static readonly (int, int)[] SpeedSub = { (1, 1), (1, 2), (1, 3), (2, 4), (3, 5), (4, 6) };
		private static readonly (int, int)[] CritSub = { (1, 2), (1, 3), (1, 3), (2, 4), (3, 5), (4, 6) };
		private static readonly (int, int)[] CritDamageSub = { (1, 2), (1, 3), (2, 4), (2, 5), (3, 5), (4, 7) };
		private static readonly (int, int)[] ResistanceSub = { (1, 2), (1, 3), (2, 4), (2, 5), (3, 7), (4, 8) };

		// Pedras: (mínimo, máximo) por grau, de Mágica a Lendária.
		private static readonly (int, int)[] HealthFlatGrind = { (100, 200), (180, 250), (230, 450), (430, 550) };
		private static readonly (int, int)[] FlatGrind = { (6, 12), (10, 18), (12, 22), (18, 30) };
		private static readonly (int, int)[] PercentGrind = { (2, 5), (3, 6), (4, 7), (5, 10) };
		private static readonly (int, int)[] SpeedGrind = { (1, 2), (2, 3), (3, 4), (4, 5) };

		private static readonly (int, int)[] HealthFlatGem = { (130, 220), (200, 310), (290, 420), (400, 580) };
		private static readonly (int, int)[] FlatGem = { (10, 16), (15, 23), (20, 30), (28, 40) };
		private static readonly (int, int)[] PercentGem = { (3, 7), (5, 9), (7, 11), (9, 13) };
		private static readonly (int, int)[] SpeedGem = { (2, 4), (3, 6), (5, 8), (7, 10) };
		private static readonly (int, int)[] CritGem = { (2, 4), (3, 5), (4, 7), (6, 9) };
		private static readonly (int, int)[] CritDamageGem = { (3, 5), (4, 6), (5, 8), (7, 10) };
		private static readonly (int, int)[] ResistanceGem = { (3, 6), (5, 8), (6, 9), (8, 11) };

		/// <summary>Mana por tentativa, para chegar a +1 ... +15, de 1★ a 6★.</summary>
		private static readonly int[][] ManaCost =
		{
			new[] { 100, 150, 225, 330, 500, 750 },
			new[] { 175, 300, 475, 680, 950, 1475 },
			new[] { 250, 450, 725, 1030, 1400, 2200 },
			new[] { 400, 700, 1075, 1480, 1925, 3050 },
			new[] { 550, 950, 1425, 1930, 2450, 3900 },
			new[] { 775, 1275, 1875, 2455, 3175, 4875 },
			new[] { 1000, 1600, 2325, 2980, 3900, 5850 },
			new[] { 1300, 2025, 2850, 3680, 4750, 6975 },
			new[] { 1600, 2450, 3375, 4380, 5600, 8100 },
			new[] { 2000, 3000, 4075, 5205, 6600, 9350 },
			new[] { 2400, 3550, 4775, 6030, 7600, 10600 },
			new[] { 2925, 4225, 5600, 6980, 8850, 11975 },
			new[] { 3450, 4900, 6425, 7930, 10100, 13350 },
			new[] { 4100, 5700, 7375, 9130, 11600, 14850 },
			new[] { 4750, 6500, 8325, 10330, 13100, 16350 },
		};

		private static (double, double, double)[] MainTable(RuneStat stat) => stat switch
		{
			RuneStat.HealthFlat => HealthFlatMain,
			RuneStat.AttackFlat or RuneStat.DefenseFlat => FlatMain,
			RuneStat.Speed => SpeedMain,
			RuneStat.Crit => CritMain,
			RuneStat.CritDamage => CritDamageMain,
			RuneStat.Resistance or RuneStat.Accuracy => ResistanceMain,
			_ => PercentMain,
		};

		private static (int, int)[] SubstatTable(RuneStat stat) => stat switch
		{
			RuneStat.HealthFlat => HealthFlatSub,
			RuneStat.AttackFlat or RuneStat.DefenseFlat => FlatSub,
			RuneStat.Speed => SpeedSub,
			RuneStat.Crit => CritSub,
			RuneStat.CritDamage => CritDamageSub,
			RuneStat.Resistance or RuneStat.Accuracy => ResistanceSub,
			_ => PercentSub,
		};

		private static (int, int)[] GrindTable(RuneStat stat) => stat switch
		{
			RuneStat.HealthFlat => HealthFlatGrind,
			RuneStat.AttackFlat or RuneStat.DefenseFlat => FlatGrind,
			RuneStat.Speed => SpeedGrind,
			_ => PercentGrind,
		};

		private static (int, int)[] GemTable(RuneStat stat) => stat switch
		{
			RuneStat.HealthFlat => HealthFlatGem,
			RuneStat.AttackFlat or RuneStat.DefenseFlat => FlatGem,
			RuneStat.Speed => SpeedGem,
			RuneStat.Crit => CritGem,
			RuneStat.CritDamage => CritDamageGem,
			RuneStat.Resistance or RuneStat.Accuracy => ResistanceGem,
			_ => PercentGem,
		};

		private static int GradeIndex(int grade) => Math.Clamp(grade, 1, MaxGrade) - 1;

		private static int ToolIndex(RuneRarity grade) => Math.Clamp((int)grade, 1, 4) - 1;

		private static double Roll(Random random, RuneStat stat, (int Min, int Max) range) =>
			FromPoints(stat, random.Next(range.Min, range.Max + 1));

		private static (double, double) Scaled(RuneStat stat, (int Min, int Max) range) =>
			(FromPoints(stat, range.Min), FromPoints(stat, range.Max));

		/// <summary>Pontos da tabela para o valor da runa: número para os fixos, fração para as porcentagens.</summary>
		private static double FromPoints(RuneStat stat, double points) => IsFlat(stat) ? points : points / 100;
	}
}
