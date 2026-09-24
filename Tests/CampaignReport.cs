using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>
	/// Relatório, não teste: roda cada fase da campanha muitas vezes no automático e mostra a taxa de
	/// vitória e a duração. É a "fase 0" do roadmap (GDD, seção 13): testar a matemática de
	/// Velocidade e Éter sem nenhum gráfico. Uso: <c>dotnet run --project Tests -- --simular</c>.
	///
	/// O time é o de quem joga sem sorte: os três Diabretes iniciais e a 5★ garantida do tutorial.
	/// O nível esperado na fase N é N (a Essência da primeira vitória paga mais ou menos um nível por
	/// fase), com o teto da região; cada fase roda nesse nível e 3 abaixo dele.
	/// </summary>
	internal static class CampaignReport
	{
		private const int Seeds = 40;

		private static readonly string[] TypicalTeam = { "fenix_fogo", "diabrete_fogo", "diabrete_agua", "diabrete_luz" };
		private static readonly string[] TypicalPages = { "dardo_arcano", "colheita", "presagio", "chuva_de_estilhacos" };

		public static void Print(GameDatabase database)
		{
			Console.WriteLine($"Time: {string.Join(", ", TypicalTeam)} | {Seeds} lutas por linha | postura Equilibrada");
			Console.WriteLine();
			Console.WriteLine("fase  inimigos  time  vitórias  rodadas  vida que sobra");
			foreach (var stage in database.Stages)
			{
				var expected = Math.Min(SharedLevel.RegionOneCap, stage.Number);
				foreach (var level in new[] { expected, Math.Max(1, expected - 3) })
				{
					var (wins, rounds, health) = Run(database, stage, level);
					Console.WriteLine($"{stage.Number,4}  {stage.Level,8}  {level,4}  {wins,8:P0}  {rounds,7:F1}  {health,14:P0}");
				}
			}
		}

		/// <summary>Uma luta só, turno a turno: <c>dotnet run --project Tests -- --luta=10</c>.</summary>
		public static void PrintBattle(GameDatabase database, int stageNumber)
		{
			var stage = database.Stage(stageNumber);
			var session = BattleFactory.Create(database, Team(database, stage.Level), stage, seed: 1);
			var log = new List<BattleEvent>(session.Start());
			while (!session.IsOver)
			{
				var turn = session.BeginTurn();
				log.AddRange(turn.Events);
				if (!turn.NeedsDecision)
					continue;

				log.AddRange(turn.Actor switch
				{
					ConjurerSeat => session.Act(AutoPilot.ForConjurer(session, Posture.Balanced)),
					BattleUnit { Side: Side.Allies } ally => session.Act(AutoPilot.ForAlly(session, ally, Posture.Balanced)),
					BattleUnit enemy => session.Act(AutoPilot.ForEnemy(session, enemy)),
					_ => Array.Empty<BattleEvent>(),
				});
			}

			foreach (var e in log)
			{
				Console.WriteLine(e switch
				{
					TurnStarted t => $"\n[{t.Round,2}] {t.Actor.Name}",
					SkillUsed s => $"     usa {s.Skill.Name}{(s.Enhanced ? " (aprimorada)" : "")}",
					PageCast p => $"     lança {p.Page.Page.Name}",
					Damaged d => $"     {d.Target.Name} -{d.Amount}{(d.Crit ? " crítico" : "")} ({d.Target.Health:F0}/{d.Target.MaxHealth:F0})",
					Healed h => $"     {h.Target.Name} +{h.Amount}",
					StatusApplied a => $"     {a.Target.Name} recebe {a.Status} ({a.Turns})",
					Died d => $"     {d.Unit.Name} cai",
					WaveStarted w => $"\n=== onda {w.Wave}/{w.WaveCount}",
					BattleEnded b => $"\n=== {(b.Victory ? "vitória" : "derrota")}",
					_ => $"     {e.GetType().Name}",
				});
			}
		}

		private static BattleTeam Team(GameDatabase database, int level) => new(
			TypicalTeam.Select(id => new TeamMember(database.Summon(id), 0)).ToList(),
			level,
			database.Conjurers[0],
			TypicalPages.Select(database.Page).ToList());

		private static (double Wins, double Rounds, double Health) Run(GameDatabase database, StageDefinition stage, int level)
		{
			var team = Team(database, level);

			var results = new List<(bool Won, double Rounds, double Health)>();
			for (var seed = 1; seed <= Seeds; seed++)
			{
				var session = BattleFactory.Create(database, team, stage, seed);
				var won = AutoBattle.Run(session, Posture.Balanced);
				var health = session.Allies.Sum(u => u.Health) / session.Allies.Sum(u => u.MaxHealth);
				results.Add((won, session.Time, health));
			}

			return (results.Count(r => r.Won) / (double)Seeds, results.Average(r => r.Rounds), results.Average(r => r.Health));
		}
	}
}
