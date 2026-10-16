using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>
	/// A calibragem da Arena: <c>dotnet run --project Tests -- --arena</c>. Cada Grau enfrenta, em composições
	/// sorteadas, os times de referência da Campanha (fases 20, 30, 40, 50) e o de runas 6★ +15: o do Grau tem de
	/// ganhar de ~85% e o do Grau abaixo, quase sempre; o do Grau acima, de ~50%.
	/// </summary>
	internal static class ArenaReport
	{
		private const int Compositions = 8;
		private const int FightsEach = 3;

		/// <summary>Os times de referência e onde esperamos que cada um fique na escada de Graus (o índice do Grau "dele").</summary>
		public static IReadOnlyList<(string Name, BattleTeam Team, string Own)> Teams(GameDatabase database) => new (string, BattleTeam, string)[]
		{
			("st20", ReferenceTeams.AtCampaign(database, 20), "adept_3"),
			("st30", ReferenceTeams.AtCampaign(database, 30), "alchemist_1"),
			("st40", ReferenceTeams.AtCampaign(database, 40), "alchemist_2"),
			("st50", ReferenceTeams.AtCampaign(database, 50), "alchemist_3"),
			("r6", ReferenceTeams.OnMap(ReferenceTeams.AtFloor(database, 5)), "magister"),
		};

		/// <summary>A taxa de vitória de <paramref name="team"/> contra defesas do <paramref name="grade"/>.</summary>
		public static double Wins(GameDatabase database, ArenaGrade grade, BattleTeam team)
		{
			var wins = 0;
			var total = 0;
			for (var composition = 1; composition <= Compositions; composition++)
			{
				var defense = ArenaTeams.Defense(database, grade.Ghost, new Random(composition * 101));
				for (var fight = 0; fight < FightsEach; fight++)
				{
					var session = BattleFactory.CreateDuel(database, team, defense, composition * 31 + fight);
					if (AutoBattle.Run(session))
						wins++;
					total++;
				}
			}

			return wins / (double)total;
		}

		public static void Print(GameDatabase database)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
			var teams = Teams(database);
			Console.WriteLine($"{Compositions * FightsEach} fights per cell; wins of each reference team against each Grade's defense (own grade marked *)");
			Console.WriteLine($"{"grade",-16} {"points",6}  " + string.Join("  ", teams.Select(t => $"{t.Name,5}")));
			foreach (var grade in database.Arena.Grades)
			{
				var cells = teams.Select(t => $"{Wins(database, grade, t.Team) * 100,4:0}{(t.Own == grade.Id ? "*" : " ")}");
				Console.WriteLine($"{grade.Name,-16} {grade.Points,6}  " + string.Join("  ", cells));
			}
		}
	}
}
