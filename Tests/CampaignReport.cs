using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// Relatório, não teste: roda cada fase da Campanha e cada andar de Masmorra muitas vezes no
	/// automático e mostra a taxa de vitória. É a calibragem do GDD (seção 10), para conferir depois de
	/// mexer em atributos, habilidades ou ondas. Uso: <c>dotnet run --project Tests -- --simulate</c>.
	///
	/// Na Campanha, cada fase contra quem chega a ela (<see cref="ReferenceTeams.AtStage"/>: vence de 80%
	/// para cima, 70% nos chefes) e contra o 6★ nível 40 sem runas (o outro jeito de terminar a
	/// Campanha). Nas Masmorras, cada andar contra o degrau que ele pede e contra o de baixo
	/// (<see cref="ReferenceTeams.Tier"/>). O automático não usa Éter, então o relatório mede o time sem
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

		/// <summary>Uma luta só, turno a turno: <c>dotnet run --project Tests -- --fight=10</c>.</summary>
		public static void PrintBattle(GameDatabase database, int stageNumber)
		{
			var stage = database.Stage(stageNumber);
			var session = BattleFactory.Create(database, ReferenceTeams.AtStage(database, stageNumber), stage.Encounter, seed: 1);
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
