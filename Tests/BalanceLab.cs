using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>
	/// A bancada de balanceamento: varre as composições possíveis com as invocações escolhidas contra um ou
	/// mais encontros, com o investimento pedido (estrelas, nível, Despertar, habilidades, runas), e grava um
	/// relatório HTML (<see cref="BalanceReport"/>): o ranking das composições, o peso de cada monstro e de
	/// cada família, a distribuição das vitórias e lutas de exemplo com o log completo e a Vida turno a turno.
	///
	///   dotnet run --project Tests -c Release -- --balance --vs=golem5
	///   dotnet run --project Tests -c Release -- --balance --help
	///
	/// As lutas são as do jogo (BattleFactory e AutoBattle, o automático sem foco no chefe). Com a semente 1, a
	/// luta i usa a semente i × 7919, a mesma de <see cref="ReferenceTeams.WinRate"/>: o número bate com os testes.
	/// </summary>
	internal static class BalanceLab
	{
		public const string Help = @"Bancada de balanceamento: dotnet run --project Tests -c Release -- --balance [opções]

Contra o quê
  --vs=golem5,crypt5       andares de Masmorra (<id><andar>), uma Masmorra inteira (golem),
                           fases (stage50) ou dungeons (o último andar de cada Masmorra)
Quem entra
  --pool=mystic            mystic: 3★ a 5★ de Fogo, Água e Vento (o Pergaminho Místico; padrão)
                           free: 3★ e 4★ de Fogo, Água e Vento | all: todas as invocações
  --ids=a,b,c              só estas invocações (no lugar do pool)
  --stars=3,4              só estas estrelas naturais     --elements=fire,water
  --roles=attack,support   só estes papéis                 --families=goblin,imp
  --exclude=a,b            tira estas
  --must=a,b               toda composição tem estas
  --size=5                 monstros por time
  --max5=1 --max4=2        no máximo N de 5★ / 4★          --min5=1 --min4=1  pelo menos N de 5★ / 4★
                           (o time gratuito: --pool=free --min4=1 --max4=1)
  --leader=id              quem lidera (padrão: a melhor Liderança de maior raridade)
  --comp=a,b,c,d,e         só esta composição (várias separadas por ;), sem varredura
Investimento (o preset e o que mudar nele)
  --investment=prepared    prepared: 6★ nível 40, desperto, habilidades no máximo, runas 6★ +12 (+15 nas pares), 8 sorteios por vaga
                           sweet: 5★ nível 35, habilidades no máximo, runas 5★ +12, 3 sorteios
                           campaign: estrelas naturais, nível 20, runas 4★ +12 | bare: 6★ nível 40, sem runas
  --evo=6|natural  --level=40  --awakened=yes|no  --skills=max|base
  --runes=6:15 (estrelas:melhora, 0 = sem runas)   --picks=8 (runas sorteadas por vaga; fica a melhor)
Execução
  --fights=10              lutas por composição e encontro
  --limit=2000             composições no máximo (acima disso, uma amostra ao acaso)
  --seed=1                 a semente (1: as mesmas lutas dos testes)
  --samples=2              lutas de exemplo com log completo (as melhores, a do meio e a pior)
  --threads=0              núcleos (0 = todos)
  --out=reports/balance.html";

		internal sealed record Investment(string Name, int? Evo, int Level, bool Awakened, bool MaxSkills, int RuneGrade, int RuneLevel, int EvenLevel, int Picks)
		{
			public string Describe() =>
				$"{(Evo is { } evo ? $"{evo}★" : "estrelas naturais")} nível {Level}, {(Awakened ? "desperto" : "sem Despertar")}, habilidades {(MaxSkills ? "no máximo" : "no nível 1")}, " +
				(RuneGrade <= 0 ? "sem runas" : $"runas {RuneGrade}★ +{RuneLevel}{(EvenLevel > RuneLevel ? $" (+{EvenLevel} nas vagas pares)" : "")}, {Picks} sorteio(s) por vaga");
		}

		private static readonly Dictionary<string, Investment> Presets = new()
		{
			["prepared"] = new("prepared", 6, 40, true, true, 6, 12, 15, 8),
			["sweet"] = new("sweet", 5, 35, false, true, 5, 12, 0, 3),
			["campaign"] = new("campaign", null, 20, false, false, 4, 12, 0, 1),
			["bare"] = new("bare", 6, 40, false, false, 0, 0, 0, 1),
		};

		internal sealed record Target(string Key, string Label, Encounter Encounter);

		internal sealed class Settings
		{
			public List<Target> Targets = new();
			public List<SummonDefinition> Pool = new();
			public string PoolLabel = "";
			public List<string> Must = new();
			public int Size = 5;
			public int? Max5;
			public int? Max4;
			public int? Min5;
			public int? Min4;
			public string? Leader;
			public List<string[]>? Comps;
			public Investment Investment = Presets["prepared"];
			public int Fights = 10;
			public int Limit = 2000;
			public int Seed = 1;
			public int Samples = 2;
			public int Threads;
			public string Out = "reports/balance.html";
		}

		/// <summary>Uma composição e o que ela fez em cada encontro (vitórias de 0 a 1, rodadas e Vida que sobra nas vitórias).</summary>
		internal sealed class Comp
		{
			public required string[] Ids;
			public double[] Wins = Array.Empty<double>();
			public double[] Rounds = Array.Empty<double>();
			public double[] Health = Array.Empty<double>();
			public double MeanWins => Wins.Average();
		}

		public static int Run(GameDatabase database, string[] args)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
			if (args.Contains("--help") || Opt(args, "vs") == null)
			{
				Console.WriteLine(Help);
				return Opt(args, "vs") == null && !args.Contains("--help") ? 1 : 0;
			}

			Settings settings;
			try
			{
				settings = Parse(database, args);
			}
			catch (ArgumentException error)
			{
				Console.Error.WriteLine(error.Message);
				return 1;
			}

			var clock = Stopwatch.StartNew();
			var (comps, total) = Compositions(settings);
			if (comps.Count == 0)
			{
				Console.Error.WriteLine("Nenhuma composição com esses filtros.");
				return 1;
			}

			Console.WriteLine($"{comps.Count} composições{(total > comps.Count ? $" (amostra de {total:N0} possíveis)" : "")} × {settings.Targets.Count} encontro(s) × {settings.Fights} lutas, {settings.Investment.Describe()}");
			var done = 0;
			var options = new ParallelOptions { MaxDegreeOfParallelism = settings.Threads > 0 ? settings.Threads : Environment.ProcessorCount };
			Parallel.ForEach(comps, options, comp =>
			{
				var team = Team(database, settings, comp.Ids);
				comp.Wins = new double[settings.Targets.Count];
				comp.Rounds = new double[settings.Targets.Count];
				comp.Health = new double[settings.Targets.Count];
				for (var t = 0; t < settings.Targets.Count; t++)
				{
					int wins = 0;
					double rounds = 0, health = 0;
					for (var i = 1; i <= settings.Fights; i++)
					{
						var session = BattleFactory.Create(database, team, settings.Targets[t].Encounter, FightSeed(settings, i));
						if (!AutoBattle.Run(session))
							continue;
						wins++;
						rounds += session.Time;
						health += session.Allies.Sum(u => Math.Max(0, u.Health)) / session.Allies.Sum(u => u.MaxHealth);
					}

					comp.Wins[t] = wins / (double)settings.Fights;
					comp.Rounds[t] = wins > 0 ? rounds / wins : double.NaN;
					comp.Health[t] = wins > 0 ? health / wins : double.NaN;
				}

				var count = System.Threading.Interlocked.Increment(ref done);
				if (!Console.IsOutputRedirected && count % Math.Max(1, comps.Count / 50) == 0)
					Console.Write($"\r{count * 100 / comps.Count}%");
			});
			Console.WriteLine($"{(Console.IsOutputRedirected ? "" : "\r")}Simulado em {clock.Elapsed.TotalSeconds:0.0} s");

			var samples = new List<BalanceReport.FightLog>();
			for (var t = 0; t < settings.Targets.Count; t++)
			{
				foreach (var (title, comp) in SampleComps(settings, comps, t))
				{
					var team = Team(database, settings, comp.Ids);
					var won = comp.Wins[t] >= 0.5;
					// A luta que representa a composição: uma vitória de quem costuma vencer, uma derrota de quem costuma perder.
					var seed = Enumerable.Range(1, settings.Fights).Select(i => FightSeed(settings, i))
						.FirstOrDefault(s => AutoBattle.Run(BattleFactory.Create(database, team, settings.Targets[t].Encounter, s)) == won, FightSeed(settings, 1));
					samples.Add(BalanceReport.Record(database, team, settings.Targets[t], seed, title, t));
				}
			}

			var path = Path.IsPathRooted(settings.Out) ? settings.Out : Path.Combine(Program.ProjectRoot, settings.Out);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, BalanceReport.Html(database, settings, comps, total, samples, clock.Elapsed));
			PrintSummary(database, settings, comps);
			Console.WriteLine($"Relatório: {path}");
			return 0;
		}

		/// <summary>Com a semente 1, a luta i usa i × 7919: as mesmas lutas de <see cref="ReferenceTeams.WinRate"/>.</summary>
		internal static int FightSeed(Settings settings, int fight) => fight * 7919 + (settings.Seed - 1) * 104729;

		internal static BattleTeam Team(GameDatabase database, Settings settings, IReadOnlyList<string> ids)
		{
			var inv = settings.Investment;
			return ReferenceTeams.Custom(database, ids, s => Math.Max(s.Rarity, inv.Evo ?? s.Rarity), inv.Level, inv.Awakened, inv.MaxSkills,
				inv.RuneGrade, inv.RuneLevel, inv.EvenLevel, inv.Picks);
		}

		// Composições --------------------------------------------------------------------------------

		/// <summary>Todas as composições que passam nos filtros, ou uma amostra ao acaso delas; e quantas são ao todo.</summary>
		private static (List<Comp> Comps, BigInteger Total) Compositions(Settings settings)
		{
			if (settings.Comps != null)
				return (settings.Comps.Select(ids => new Comp { Ids = Lead(settings, ids) }).ToList(), settings.Comps.Count);

			var must = settings.Must.Select(id => settings.Pool.First(s => s.Id == id)).ToList();
			var rest = settings.Pool.Where(s => !settings.Must.Contains(s.Id)).ToList();
			var k = settings.Size - must.Count;
			if (k < 0 || k > rest.Count)
				return (new List<Comp>(), 0);

			bool Fits(IEnumerable<SummonDefinition> team) =>
				(settings.Max5 is not { } max5 || team.Count(s => s.Rarity == 5) <= max5) &&
				(settings.Max4 is not { } max4 || team.Count(s => s.Rarity == 4) <= max4) &&
				(settings.Min5 is not { } min5 || team.Count(s => s.Rarity == 5) >= min5) &&
				(settings.Min4 is not { } min4 || team.Count(s => s.Rarity == 4) >= min4);

			var total = Choose(rest.Count, k);
			var result = new List<Comp>();
			if (total <= settings.Limit * 4L)
			{
				foreach (var combo in Combos(rest.Count, k))
				{
					var team = must.Concat(combo.Select(i => rest[i])).ToList();
					if (Fits(team))
						result.Add(new Comp { Ids = Lead(settings, team.Select(s => s.Id).ToArray()) });
				}

				var exact = result.Count;
				var shuffle = new Random(settings.Seed);
				if (result.Count > settings.Limit)
					result = result.OrderBy(_ => shuffle.Next()).Take(settings.Limit).ToList();
				return (result, exact);
			}

			// Grande demais para listar: sorteia composições distintas que passam nos filtros.
			var random = new Random(settings.Seed);
			var seen = new HashSet<string>();
			for (var tries = 0; result.Count < settings.Limit && tries < settings.Limit * 200; tries++)
			{
				var pick = Enumerable.Range(0, rest.Count).OrderBy(_ => random.Next()).Take(k).OrderBy(i => i).ToList();
				var team = must.Concat(pick.Select(i => rest[i])).ToList();
				if (Fits(team) && seen.Add(string.Join(",", pick)))
					result.Add(new Comp { Ids = Lead(settings, team.Select(s => s.Id).ToArray()) });
			}

			return (result, total);
		}

		/// <summary>Põe o líder na frente: o pedido, senão quem tem Liderança, da maior raridade (a primeira em empate).</summary>
		private static string[] Lead(Settings settings, string[] ids)
		{
			var pool = settings.Pool.ToDictionary(s => s.Id);
			var leader = settings.Leader != null && ids.Contains(settings.Leader)
				? settings.Leader
				: ids.Where(id => pool.TryGetValue(id, out var s) && s.Leader != null).OrderByDescending(id => pool[id].Rarity).ThenByDescending(id => pool[id].Leader!.Value).FirstOrDefault();
			return leader == null ? ids : new[] { leader }.Concat(ids.Where(id => id != leader)).ToArray();
		}

		private static IEnumerable<int[]> Combos(int n, int k)
		{
			var index = Enumerable.Range(0, k).ToArray();
			if (k == 0)
			{
				yield return index;
				yield break;
			}

			while (true)
			{
				yield return (int[])index.Clone();
				var i = k - 1;
				while (i >= 0 && index[i] == n - k + i)
					i--;
				if (i < 0)
					yield break;
				index[i]++;
				for (var j = i + 1; j < k; j++)
					index[j] = index[j - 1] + 1;
			}
		}

		private static BigInteger Choose(int n, int k)
		{
			BigInteger result = 1;
			for (var i = 1; i <= k; i++)
				result = result * (n - k + i) / i;
			return result;
		}

		/// <summary>As lutas de exemplo de um encontro: as melhores, a mais perto de 50% (a beira do balanceamento) e a pior.</summary>
		private static IEnumerable<(string Title, Comp Comp)> SampleComps(Settings settings, List<Comp> comps, int t)
		{
			if (settings.Comps != null)
			{
				foreach (var comp in comps)
					yield return ("Composição pedida", comp);
				yield break;
			}

			var ranked = comps.OrderByDescending(c => c.Wins[t]).ThenBy(c => double.IsNaN(c.Rounds[t]) ? double.MaxValue : c.Rounds[t]).ToList();
			var chosen = new HashSet<Comp>();
			for (var i = 0; i < Math.Min(settings.Samples, ranked.Count); i++)
			{
				chosen.Add(ranked[i]);
				yield return ($"{i + 1}ª melhor", ranked[i]);
			}

			var edge = ranked.OrderBy(c => Math.Abs(c.Wins[t] - 0.5)).First();
			if (chosen.Add(edge))
				yield return ("Na beira (perto de 50%)", edge);
			var worst = ranked[^1];
			if (chosen.Add(worst))
				yield return ("A pior", worst);
		}

		// Opções -------------------------------------------------------------------------------------

		private static string? Opt(string[] args, string name) =>
			args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..];

		private static List<string> List(string? value) =>
			value == null ? new List<string>() : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

		private static int Int(string[] args, string name, int fallback) =>
			Opt(args, name) is { } text ? int.TryParse(text, out var value) ? value : throw new ArgumentException($"--{name}: '{text}' não é um número.") : fallback;

		private static Settings Parse(GameDatabase database, string[] args)
		{
			var settings = new Settings();
			foreach (var key in List(Opt(args, "vs")))
				settings.Targets.AddRange(Targets(database, key));

			// Invocações: o pool e os filtros.
			var all = database.Summons.ToList();
			var poolName = Opt(args, "pool") ?? "mystic";
			var pool = poolName switch
			{
				"all" => all,
				"mystic" => all.Where(s => s.Rarity >= 3 && SummonRates.Allows(ScrollKind.Mystic, s.Element)).ToList(),
				"free" => all.Where(s => s.Rarity is 3 or 4 && SummonRates.Allows(ScrollKind.Mystic, s.Element)).ToList(),
				_ => throw new ArgumentException($"--pool: '{poolName}' (all, mystic ou free)."),
			};
			settings.PoolLabel = poolName;
			if (List(Opt(args, "ids")) is { Count: > 0 } ids)
			{
				pool = ids.Select(id => all.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException($"--ids: '{id}' não existe.")).ToList();
				settings.PoolLabel = "ids";
			}

			var stars = List(Opt(args, "stars")).Select(int.Parse).ToList();
			var elements = List(Opt(args, "elements")).Select(e => Enum.Parse<Element>(e, true)).ToList();
			var roles = List(Opt(args, "roles")).Select(r => Enum.Parse<Role>(r, true)).ToList();
			var families = List(Opt(args, "families"));
			var exclude = List(Opt(args, "exclude"));
			pool = pool.Where(s => (stars.Count == 0 || stars.Contains(s.Rarity)) && (elements.Count == 0 || elements.Contains(s.Element))
				&& (roles.Count == 0 || roles.Contains(s.Role)) && (families.Count == 0 || families.Contains(s.FamilyId)) && !exclude.Contains(s.Id)).ToList();

			settings.Must = List(Opt(args, "must"));
			foreach (var id in settings.Must.Where(id => pool.All(s => s.Id != id)))
				pool.Add(all.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException($"--must: '{id}' não existe."));

			if (Opt(args, "comp") is { } comps)
			{
				settings.Comps = comps.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(c => List(c).ToArray()).ToList();
				foreach (var id in settings.Comps.SelectMany(c => c).Where(id => pool.All(s => s.Id != id)))
					pool.Add(all.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException($"--comp: '{id}' não existe."));
			}

			settings.Pool = pool.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
			settings.Size = Int(args, "size", 5);
			settings.Max5 = Opt(args, "max5") != null ? Int(args, "max5", 0) : null;
			settings.Max4 = Opt(args, "max4") != null ? Int(args, "max4", 0) : null;
			settings.Min5 = Opt(args, "min5") != null ? Int(args, "min5", 0) : null;
			settings.Min4 = Opt(args, "min4") != null ? Int(args, "min4", 0) : null;
			settings.Leader = Opt(args, "leader");

			// Investimento: o preset e o que mudar nele.
			var presetName = Opt(args, "investment") ?? "prepared";
			var inv = Presets.TryGetValue(presetName, out var preset) ? preset : throw new ArgumentException($"--investment: '{presetName}' (prepared, sweet, campaign ou bare).");
			if (Opt(args, "evo") is { } evo)
				inv = inv with { Evo = evo == "natural" ? null : int.Parse(evo) };
			if (Opt(args, "level") != null)
				inv = inv with { Level = Int(args, "level", inv.Level) };
			if (Opt(args, "awakened") is { } awakened)
				inv = inv with { Awakened = awakened is "yes" or "true" or "sim" or "1" };
			if (Opt(args, "skills") is { } skills)
				inv = inv with { MaxSkills = skills is "max" or "maximo" or "máximo" };
			if (Opt(args, "runes") is { } runes)
			{
				var parts = runes.Split(':');
				inv = inv with { RuneGrade = int.Parse(parts[0]), RuneLevel = parts.Length > 1 ? int.Parse(parts[1]) : 12, EvenLevel = 0 };
			}

			if (Opt(args, "picks") != null)
				inv = inv with { Picks = Int(args, "picks", inv.Picks) };
			settings.Investment = inv with { Name = presetName };

			settings.Fights = Math.Max(1, Int(args, "fights", 10));
			settings.Limit = Math.Max(1, Int(args, "limit", 2000));
			settings.Seed = Int(args, "seed", 1);
			settings.Samples = Math.Max(0, Int(args, "samples", 2));
			settings.Threads = Int(args, "threads", 0);
			settings.Out = Opt(args, "out") ?? settings.Out;
			if (settings.Targets.Count == 0)
				throw new ArgumentException("--vs: nenhum encontro.");
			return settings;
		}

		private static IEnumerable<Target> Targets(GameDatabase database, string key)
		{
			if (key == "dungeons")
			{
				foreach (var d in database.Dungeons)
					yield return new Target($"{d.Id}{d.Floors.Count}", $"{d.Name} · andar {d.Floors.Count}", d.Floor(d.Floors.Count).Encounter);
				yield break;
			}

			if (key.StartsWith("stage", StringComparison.Ordinal) && int.TryParse(key[5..], out var number))
			{
				yield return new Target(key, $"Fase {number}", database.Stage(number).Encounter);
				yield break;
			}

			var dungeon = database.Dungeons.OrderByDescending(d => d.Id.Length).FirstOrDefault(d => key.StartsWith(d.Id, StringComparison.Ordinal))
				?? throw new ArgumentException($"--vs: '{key}' não é Masmorra nem fase (ex.: golem5, golem, stage50, dungeons).");
			var rest = key[dungeon.Id.Length..];
			var floors = rest == "" ? Enumerable.Range(1, dungeon.Floors.Count) : new[] { int.Parse(rest) };
			foreach (var floor in floors)
				yield return new Target($"{dungeon.Id}{floor}", $"{dungeon.Name} · andar {floor}", dungeon.Floor(floor).Encounter);
		}

		private static void PrintSummary(GameDatabase database, Settings settings, List<Comp> comps)
		{
			for (var t = 0; t < settings.Targets.Count; t++)
			{
				var good = comps.Count(c => c.Wins[t] >= 0.7);
				Console.WriteLine();
				Console.WriteLine($"{settings.Targets[t].Label}: {good} de {comps.Count} composições vencem 70%+");
				foreach (var comp in comps.OrderByDescending(c => c.Wins[t]).ThenBy(c => double.IsNaN(c.Rounds[t]) ? 999 : c.Rounds[t]).Take(10))
					Console.WriteLine($"  {comp.Wins[t],5:P0} {(double.IsNaN(comp.Rounds[t]) ? "   -" : comp.Rounds[t].ToString("0.0").PadLeft(5))}  {string.Join(", ", comp.Ids.Select(id => database.Summon(id).Name))}");
			}
		}
	}
}
