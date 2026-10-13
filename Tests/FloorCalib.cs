using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>
	/// Bancada de calibragem das Masmorras (--calib, ou --calib=golem): a taxa de vitória de cada andar para vários
	/// multiplicadores da escala, com os mesmos times do --digest (cada família, 6★ nível 40, desperta e com sigilos).
	/// Escolha o multiplicador que dá a taxa que o andar deve ter e grave escala × multiplicador em Data/dungeons.json.
	/// </summary>
	internal static class FloorCalib
	{
		private static readonly double[] Multipliers = { 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.15, 1.3 };

		public static int Run(GameDatabase database, string[] args)
		{
			if (args.Any(a => a == "--calib=stages"))
				return Stages(database);
			if (args.Any(a => a == "--calib=powerful"))
				return Powerful(database);
			if (args.Any(a => a == "--calib=atfloor"))
				return AtFloor(database);
			if (args.Any(a => a == "--calib=search"))
				return Search(database);

			const int seeds = 8;
			var only = args.FirstOrDefault(a => a.StartsWith("--calib=", StringComparison.Ordinal))?["--calib=".Length..];
			var teams = database.Families
				.Select(f => BattleDigest.Team(database, database.Summons.Where(s => s.FamilyId == f.Id).OrderBy(s => s.Element).Select(s => s.Id).ToArray(), 6, 40, true, true))
				.ToList();

			Console.WriteLine("andar  escala  " + string.Join(" ", Multipliers.Select(m => $"x{m:0.00}".PadLeft(6))));
			foreach (var dungeon in database.Dungeons.Where(d => only == null || d.Id == only))
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var real = dungeon.Floor(floor).Encounter;
					var cells = Multipliers.Select(m =>
					{
						var encounter = real with { Scale = real.Scale * m };
						var wins = 0;
						foreach (var team in teams)
						{
							for (var seed = 1; seed <= seeds; seed++)
							{
								if (AutoBattle.Run(BattleFactory.Create(database, team, encounter, seed)))
									wins++;
							}
						}

						return $"{100.0 * wins / (teams.Count * seeds):0}%".PadLeft(6);
					});
					Console.WriteLine($"{dungeon.Id}{floor}".PadRight(7) + $"{real.Scale:0.00}".PadLeft(5) + "   " + string.Join(" ", cells));
				}
			}

			return 0;
		}

		/// <summary>
		/// O alvo documentado de cada andar (ReferenceTeams.AtFloor): o time do andar vence (cerca de 75%) e o do andar
		/// de baixo perde. Para cada andar, as duas taxas em vários multiplicadores da escala.
		/// </summary>
		private static int AtFloor(GameDatabase database)
		{
			var multipliers = new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.7, 1.0 };
			Console.WriteLine("andar  escala  (time do andar / time de baixo) por multiplicador: " + string.Join("  ", multipliers.Select(m => $"x{m:0.00}")));
			foreach (var dungeon in database.Dungeons)
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var real = dungeon.Floor(floor).Encounter;
					var at = ReferenceTeams.AtFloor(database, floor);
					var below = ReferenceTeams.AtFloor(database, floor - 1);
					var cells = multipliers.Select(m =>
					{
						var encounter = real with { Scale = real.Scale * m };
						return $"{ReferenceTeams.WinRate(database, at, encounter, 12):P0}/{ReferenceTeams.WinRate(database, below, encounter, 12):P0}".PadLeft(9);
					});
					Console.WriteLine($"{dungeon.Id}{floor}".PadRight(8) + $"{real.Scale:0.00}".PadLeft(5) + "  " + string.Join(" ", cells));
				}
			}

			return 0;
		}

		/// <summary>
		/// Procura, andar a andar, o multiplicador da escala que mais folga dá às duas condições de ReferenceTeams.AtFloor
		/// (o do andar vence, o de baixo perde) e imprime a escala nova, as taxas e a folga.
		/// </summary>
		private static int Search(GameDatabase database)
		{
			foreach (var dungeon in database.Dungeons)
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var real = dungeon.Floor(floor).Encounter;
					var at = ReferenceTeams.AtFloor(database, floor);
					var below = ReferenceTeams.AtFloor(database, floor - 1);
					(double Scale, double At, double Below, double Margin) best = (0, 0, 1, -9);
					for (var m = 0.05; m <= 1.6; m += 0.025)
					{
						var encounter = real with { Scale = real.Scale * m };
						var a = ReferenceTeams.WinRate(database, at, encounter, 16);
						var b = ReferenceTeams.WinRate(database, below, encounter, 16);
						var margin = Math.Min(a - 0.75, 0.25 - b);
						if (margin > best.Margin)
							best = (Math.Round(real.Scale * m, 2), a, b, margin);
					}

					Console.WriteLine($"{dungeon.Id}{floor}: escala {real.Scale:0.00} -> {best.Scale:0.00}  (do andar {best.At:P0}, de baixo {best.Below:P0}, folga {best.Margin:0.00})");
				}
			}

			return 0;
		}

		/// <summary>O andar 5 de cada Masmorra contra o time forte genérico, para vários multiplicadores (ele não deve passar).</summary>
		private static int Powerful(GameDatabase database)
		{
			var powerful = ReferenceTeams.Powerful(database);
			foreach (var dungeon in database.Dungeons)
			{
				var floor = dungeon.Floor(dungeon.Floors.Count);
				var cells = new[] { 1.0, 1.8, 2.5, 3.5, 5.0, 7.0 }.Select(m =>
					$"x{m:0.0}={ReferenceTeams.WinRate(database, powerful, floor.Encounter with { Scale = floor.Encounter.Scale * m }, 20):P0}");
				Console.WriteLine($"{dungeon.Id}5 escala {floor.Scale:0.00}: {string.Join("  ", cells)}");
			}

			return 0;
		}

		/// <summary>A última fase (e a 40) para vários multiplicadores: a equipe de nível 20 com sigilos, a sem sigilos e a de 6★ nível 40 pelada.</summary>
		private static int Stages(GameDatabase database)
		{
			var last = database.Stages.Last();
			var bare = ReferenceTeams.Bare(database);
			var runed = ReferenceTeams.AtStage(database, last.Number);
			var noRunes = new BattleTeam(runed.Members.Select(m => m with { Runes = Array.Empty<Core.Runes.Rune>() }).ToList());
			Console.WriteLine($"fase {last.Number} (escala {last.Scale:0.00}): multiplicador → [nível 20 com sigilos | sem sigilos | 6★ pelado | pelado na fase 40]");
			var s40 = database.Stage(40);
			Console.WriteLine($"fase 40 (escala {s40.Scale:0.00}): multiplicador → 6★ pelado");
			foreach (var m in new[] { 0.6, 0.7, 0.8, 0.9, 1.0 })
				Console.WriteLine($"x{m:0.00}: {ReferenceTeams.WinRate(database, bare, s40.Encounter with { Scale = s40.Encounter.Scale * m }, 20):P0}");

			foreach (var m in new[] { 1.0, 1.05, 1.1, 1.15 })
			{
				var encounter = last.Encounter with { Scale = last.Encounter.Scale * m };
				var f40 = database.Stage(40).Encounter;
				Console.WriteLine($"x{m:0.00}: {ReferenceTeams.WinRate(database, runed, encounter, 20):P0} | {ReferenceTeams.WinRate(database, noRunes, encounter, 20):P0} | {ReferenceTeams.WinRate(database, bare, encounter, 20):P0} | {ReferenceTeams.WinRate(database, bare, f40, 20):P0}");
			}

			return 0;
		}
	}
}
