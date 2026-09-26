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
	/// automático e mostra a taxa de vitória e a duração. É a "fase 0" do roadmap (GDD, seção 13):
	/// testar a matemática sem nenhum gráfico. Uso: <c>dotnet run --project Tests -- --simular</c>.
	///
	/// O time é o de quem joga sem sorte (<see cref="TestData.TypicalTeam"/>): a 5★ garantida e quatro
	/// 3★, sem Despertar. Na Campanha, sem runas — o pior caso —, no nível dos inimigos e 3 níveis
	/// abaixo. Nas Masmorras, sem runas e com seis runas 5★ +9 em cada monstro. O automático não usa
	/// Éter, então o relatório mede o time sem aprimoramentos.
	/// </summary>
	internal static class CampaignReport
	{
		private const int Seeds = 40;

		public static void Print(GameDatabase database)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
			Console.WriteLine($"Team: {string.Join(", ", TestData.TypicalTeam)} | {Seeds} fights per row");
			Console.WriteLine();
			Console.WriteLine("Campaign, no runes");
			Console.WriteLine("stage  enemies  team      wins   rounds      HP left");
			foreach (var stage in database.Stages)
			{
				foreach (var level in new[] { stage.Level, Math.Max(1, stage.Level - 3) })
				{
					var (wins, rounds, health) = Run(database, stage.Encounter, Team(database, stage.Stars, level, runed: false));
					Console.WriteLine($"{stage.Number,5}  {stage.Stars + "★" + stage.Level,7}  {stage.Stars + "★" + level,4}  {wins,8:P0}  {rounds,7:F1}  {health,11:P0}");
				}
			}

			Console.WriteLine();
			Console.WriteLine("Dungeons, team 6★ level 40: no runes | 5★ +9 runes");
			Console.WriteLine("dungeon      floor   enemies      wins   rounds |     wins   rounds");
			foreach (var dungeon in database.Dungeons)
			{
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var encounter = dungeon.Floor(floor).Encounter;
					var bare = Run(database, encounter, Team(database, 6, 40, runed: false));
					var runed = Run(database, encounter, Team(database, 6, 40, runed: true));
					Console.WriteLine($"{dungeon.Id,-12} {floor,5}  {encounter.Stars + "★" + encounter.Level,8}  {bare.Wins,8:P0}  {bare.Rounds,7:F1} | {runed.Wins,8:P0}  {runed.Rounds,7:F1}");
				}
			}
		}

		/// <summary>Uma luta só, turno a turno: <c>dotnet run --project Tests -- --fight=10</c>.</summary>
		public static void PrintBattle(GameDatabase database, int stageNumber)
		{
			var stage = database.Stage(stageNumber);
			var session = BattleFactory.Create(database, Team(database, stage.Stars, stage.Level, runed: false), stage.Encounter, seed: 1);
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

		/// <summary>
		/// O time típico nas estrelas e no nível pedidos (todos evoluídos até lá), sem Despertar e com as
		/// habilidades no nível 1; com runas, seis runas 5★ +9 sorteadas em cada monstro.
		/// </summary>
		private static BattleTeam Team(GameDatabase database, int stars, int level, bool runed)
		{
			var random = new Random(11);
			return new BattleTeam(TestData.TypicalTeam.Select(id =>
			{
				var runes = runed ? Enumerable.Range(1, RuneRules.Slots).Select(slot => Runed(random, slot)).ToList() : new List<Rune>();
				return new TeamMember(database.Summon(id), stars, level, false, Array.Empty<int>(), runes);
			}).ToList());
		}

		private static Rune Runed(Random random, int slot)
		{
			var rune = RuneForge.Generate(random, slot, 5, slot);
			while (rune.Level < 9)
				RuneForge.RaiseLevel(random, rune);
			return rune;
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
