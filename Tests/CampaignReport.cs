using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// Relatório, não teste: roda cada fase da Campanha e cada andar de Masmorra muitas vezes no
	/// automático e mostra a taxa de vitória. É a calibragem do GDD (seção 10), para conferir depois de
	/// mexer em atributos, habilidades ou ondas. Uso: <c>dotnet run --project Tests -- --simulate</c>.
	///
	/// Na Campanha, cada fase contra quem chega a ela (<see cref="ReferenceTeams.AtStage"/>: vence de 80%
	/// para cima, 70% nos chefes) e contra o 6★ nível 40 sem runas (que vai até a fase 40: a 50 pede
	/// runas). Nas Masmorras, cada andar contra o degrau que ele pede e contra o de baixo
	/// (<see cref="ReferenceTeams.AtFloor"/>). O automático não usa Éter, então o relatório mede o time sem
	/// aprimoramentos.
	/// </summary>
	internal static class CampaignReport
	{
		private const int Seeds = 40;

		public static void Print(GameDatabase database)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
			Console.WriteLine($"Team: {string.Join(", ", TestData.TypicalTeam)} | {Seeds} fights per row");
			Console.WriteLine();
			Console.WriteLine("Campaign: reference team at the stage | 6★ level 40, no runes");
			Console.WriteLine("stage  enemies         wins  rounds |     wins  rounds");
			foreach (var stage in database.Stages)
			{
				var reference = Run(database, stage.Encounter, ReferenceTeams.AtStage(database, stage.Number));
				var bare = Run(database, stage.Encounter, ReferenceTeams.Bare(database));
				Console.WriteLine($"{stage.Number,5}  {stage.Stars + "★" + stage.Level + " ×" + stage.Scale.ToString("0.00"),-12}  {reference.Wins,6:P0}  {reference.Rounds,6:F1} | {bare.Wins,8:P0}  {bare.Rounds,6:F1}");
			}

			Console.WriteLine();
			Console.WriteLine("Dungeons: the team the floor asks for | the team of the floor below");
			Console.WriteLine("dungeon      floor   enemies             wins | below wins");
			foreach (var dungeon in database.Dungeons)
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					var asked = Run(database, encounter, ReferenceTeams.AtFloor(database, floor));
					var below = Run(database, encounter, ReferenceTeams.AtFloor(database, floor - 1));
					Console.WriteLine($"{dungeon.Id,-12} {floor,5}   {encounter.Stars + "★" + encounter.Level + " ×" + encounter.Scale.ToString("0.00"),-12}  {asked.Wins,8:P0} | {below.Wins,10:P0}");
				}
			}
		}

		/// <summary>
		/// As Masmorras de especialização, andar a andar: <c>dotnet run --project Tests -- --dungeons</c>. Cada
		/// andar contra o time típico do degrau (genérico), a equipe de especialista do degrau e a do de
		/// baixo, o ponto doce (5★, habilidades no máximo, runas 5★ +12), a preparação do andar 5 (6★
		/// desperta, runas 6★ +12) e o time forte genérico com o mesmo investimento. Depois, cada
		/// especialista no andar 5 das outras (o time certo para uma não serve para todas) e quanto custa
		/// montar cada equipe, em dias de Essência.
		/// </summary>
		public static void PrintDungeons(GameDatabase database)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
			var dungeons = ReferenceTeams.Specialists.Keys.Select(database.Dungeon).ToList();
			Console.WriteLine($"{Seeds} fights per cell; wins (average rounds of the wins)");
			Console.WriteLine("dungeon floor  enemies        generic      | specialist   below        | sweet spot   prepared     powerful");
			foreach (var dungeon in dungeons)
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					var cells = new[]
					{
						ReferenceTeams.AtFloor(database, floor),
						ReferenceTeams.Specialist(database, dungeon.Id, floor),
						ReferenceTeams.Specialist(database, dungeon.Id, floor - 1),
						ReferenceTeams.Specialist(database, dungeon.Id, 4),
						ReferenceTeams.Specialist(database, dungeon.Id, 5),
						ReferenceTeams.Powerful(database),
					}.Select(team => Cell(Run(database, encounter, team))).ToList();
					Console.WriteLine($"{dungeon.Id,-7} {floor,5}  {encounter.Stars + "★" + encounter.Level + " ×" + encounter.Scale.ToString("0.00"),-12}  {cells[0]} | {cells[1]} {cells[2]} | {cells[3]} {cells[4]} {cells[5]}");
				}
			}

			Console.WriteLine();
			Console.WriteLine("Floor 5, each prepared specialist (rows) in each Dungeon (columns)");
			Console.WriteLine("        " + string.Join(" ", dungeons.Select(d => $"{d.Id,-12}")));
			foreach (var team in dungeons)
			{
				var row = dungeons.Select(d => Cell(Run(database, d.Floor(d.Floors.Count).Encounter, ReferenceTeams.Specialist(database, team.Id, 5))));
				Console.WriteLine($"{team.Id,-7} " + string.Join(" ", row));
			}

			// As três visões do andar 5, com a mesma preparação: gratuito (efeitos), OK (dano bruto) e Spd (sincronia).
			Console.WriteLine();
			Console.WriteLine("Three ways of playing, at the floor 5 preparation: free (one 4★, four 3★), ok (specialist), spd (speed sync)");
			Console.WriteLine("dungeon floor  free         ok           spd");
			foreach (var dungeon in dungeons.Where(d => ReferenceTeams.Free.ContainsKey(d.Id)))
			{
				var views = new[] { ReferenceTeams.Free[dungeon.Id], ReferenceTeams.Specialists[dungeon.Id], ReferenceTeams.Fast[dungeon.Id] }
					.Select(members => ReferenceTeams.Prepared(database, members)).ToList();
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					Console.WriteLine($"{dungeon.Id,-7} {floor,5}  " + string.Join(" ", views.Select(team => Cell(Run(database, encounter, team)))));
				}

				var allies = BattleFactory.Create(database, views[2], dungeon.Floor(dungeon.Floors.Count).Encounter, 1).Allies;
				Console.WriteLine("        spd order: " + string.Join(" > ", allies.OrderByDescending(u => u.TurnSpeed).Select(u => $"{u.Name} {u.TurnSpeed:0}")));
			}

			// Essência por dia de quem terminou a Campanha: a canalização o dia todo e a Mana do dia no andar 4,
			// com as runas que caem desfeitas (a média da tabela do andar).
			var floor4 = dungeons[0].Floor(4);
			var sold = floor4.Grades.Sum(g => floor4.Rarities.Sum(r => g.Value * r.Value / 1e4
				* RuneRules.SellValue(new Rune { Grade = g.Key, Substats = Enumerable.Range(0, (int)r.Key).Select(_ => new RuneSubstat()).ToList() })));
			var perDay = Idle.EssencePerHour(database.Stages.Count) * 24 + Mana.PerHour * 24 / floor4.Mana * (floor4.Essence + sold);
			Console.WriteLine();
			Console.WriteLine($"Investment (Awakenings, rune upgrades) at {perDay:0} Essence a day");
			foreach (var dungeon in dungeons)
			{
				var sweet = Investment(ReferenceTeams.Specialist(database, dungeon.Id, 4));
				var prepared = Investment(ReferenceTeams.Specialist(database, dungeon.Id, 5));
				Console.WriteLine($"{dungeon.Id,-7} sweet spot {sweet,9:N0} ({sweet / perDay:0.0} days) | prepared {prepared,9:N0} ({prepared / perDay:0.0} days)");
			}
		}

		private static string Cell((double Wins, double Rounds, double Health) result) =>
			$"{result.Wins,4:P0} ({result.Rounds,4:F0})".PadRight(12);

		/// <summary>A Essência que a equipe custou além do nível: Despertar e a melhora das runas (evoluir só gasta Fragmentos).</summary>
		private static double Investment(BattleTeam team) => team.Members.Sum(member =>
			(member.Awakened ? Awakening.Cost(member.Summon.Rarity) : 0)
			+ member.Runes.Sum(rune => Enumerable.Range(0, rune.Level).Sum(level => RuneRules.UpgradeCost(rune.Grade, level))));

		/// <summary>
		/// Uma luta só, turno a turno: <c>--fight=10</c> (a fase 10) ou <c>--fight=golem5:prepared</c> (o andar 5
		/// do Golem contra a equipe pedida: generic, specialist, sweet, prepared ou powerful; o padrão é specialist).
		/// </summary>
		public static void PrintBattle(GameDatabase database, string fight)
		{
			var name = fight.Split(':')[0];
			var team = fight.Contains(':') ? fight[(fight.IndexOf(':') + 1)..] : "specialist";
			var dungeon = database.Dungeons.FirstOrDefault(d => name.StartsWith(d.Id, StringComparison.Ordinal));
			var floor = dungeon == null ? 0 : int.Parse(name[dungeon.Id.Length..]);
			var (encounter, members) = dungeon == null
				? (database.Stage(int.Parse(name)).Encounter, ReferenceTeams.AtStage(database, int.Parse(name)))
				: (dungeon.Floor(floor).Encounter, team switch
				{
					"generic" => ReferenceTeams.AtFloor(database, floor),
					"sweet" => ReferenceTeams.Specialist(database, dungeon.Id, 4),
					"prepared" => ReferenceTeams.Specialist(database, dungeon.Id, 5),
					"powerful" => ReferenceTeams.Powerful(database),
					_ => ReferenceTeams.Specialist(database, dungeon.Id, floor),
				});
			var session = BattleFactory.Create(database, members, encounter, seed: 1);
			var log = new List<BattleEvent>(session.Start());
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				log.AddRange(turn.Events);
				if (turn.NeedsDecision)
					log.AddRange(session.Act(AutoPilot.For(session, turn.Actor)));
			}

			foreach (var e in log)
			{
				Console.WriteLine(e switch
				{
					TurnStarted t => $"\n[{t.Round,2}] {t.Actor.Name}",
					SkillUsed s => $"     uses {s.Skill.Name}",
					Damaged d => $"     {d.Target.Name} -{d.Amount}{(d.Crit ? " crit" : "")}",
					Healed h => $"     {h.Target.Name} +{h.Amount}",
					StatusApplied a => $"     {a.Target.Name} gets {a.Status} ({a.Turns})",
					Died d => $"     {d.Unit.Name} falls",
					WaveStarted w => $"\n=== wave {w.Wave}/{w.WaveCount}",
					BattleEnded b => $"\n=== {(b.Victory ? "victory" : "defeat")}",
					_ => $"     {e.GetType().Name}",
				});
			}
		}

		private static (double Wins, double Rounds, double Health) Run(GameDatabase database, Encounter encounter, BattleTeam team)
		{
			var results = new List<(bool Won, double Rounds, double Health)>();
			for (var seed = 1; seed <= Seeds; seed++)
			{
				var session = BattleFactory.Create(database, team, encounter, seed);
				var won = AutoBattle.Run(session);
				var health = session.Allies.Sum(u => u.Health) / session.Allies.Sum(u => u.MaxHealth);
				results.Add((won, session.Time, health));
			}

			return (results.Count(r => r.Won) / (double)Seeds, results.Average(r => r.Rounds), results.Average(r => r.Health));
		}
	}
}
