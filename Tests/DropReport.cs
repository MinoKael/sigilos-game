using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// O drop das Masmorras, conferido e contado por dia: <c>dotnet run --project Tests -- --drops</c>. Opções:
	/// <c>--drops=golem</c> (só uma Masmorra), <c>--victories=50000</c> (vitórias por andar; padrão 20 000) e
	/// <c>--seed=7</c>.
	///
	/// Cada andar vence muitas vezes pelo <see cref="Dungeons.ApplyVictory"/> de verdade, já vencido antes e com
	/// a conta no nível 100, e o que caiu é comparado com a tabela de Data/dungeons.json:
	/// - quantas runas ou pedras cada vitória solta;
	/// - as estrelas e a raridade da runa, e o conjunto (só os da Masmorra, por igual);
	/// - na Forja, o grau de cada pedra e metade de cada tipo;
	/// - a chance do Pergaminho Místico e a do Núcleo de Infusão.
	/// A Mana, a Essência e a experiência de toda vitória têm de ser as do andar, e a primeira vitória paga o
	/// Ouro e o marco dela. Uma parte sorteada fora de <see cref="Sigmas"/> desvios-padrão da tabela é erro
	/// (com 20 000 vitórias, uns 1,6 ponto percentual numa chance de 50%), e a saída é 1.
	///
	/// No fim, o que cada andar rende num dia de Mana da canalização, pela tabela: a conta para revalidar o
	/// drop. <see cref="DungeonTests"/> roda a mesma conferência com menos vitórias.
	/// </summary>
	internal static class DropReport
	{
		private const int DefaultVictories = 20_000;

		/// <summary>Quantos desvios-padrão a parte sorteada pode se afastar da tabela.</summary>
		private const double Sigmas = 4.5;

		/// <summary>A Mana de um dia de canalização.</summary>
		private const double ManaPerDay = Mana.PerHour * 24;

		private static readonly Lazy<Dictionary<string, string>> SetNames = new(() => Names("set"));
		private static readonly Lazy<Dictionary<string, string>> RarityNames = new(() => Names("rarity"));

		/// <summary>A Forja solta metade Pedras de Afiar, metade Gemas.</summary>
		private static readonly Dictionary<RuneToolKind, double> ToolKinds = new() { [RuneToolKind.Grindstone] = 50, [RuneToolKind.Gem] = 50 };

		/// <summary>Uma parte do drop: a chance da tabela (em %) e quantas vezes caiu em quantas tentativas.</summary>
		internal sealed record Share(string Label, double Expected, int Hits, int Trials)
		{
			public double Observed => 100.0 * Hits / Trials;

			/// <summary>Chance de 0% ou 100% é exata; as outras ficam dentro de <see cref="Sigmas"/> desvios-padrão.</summary>
			public bool Ok =>
				Expected <= 0 ? Hits == 0
				: Expected >= 100 ? Hits == Trials
				: Math.Abs(Observed - Expected) <= Sigmas * Math.Sqrt(Expected * (100 - Expected) / Trials);
		}

		/// <summary>Um andar conferido: as partes sorteadas, em grupos (estrelas, raridade...), e o que não bateu.</summary>
		internal sealed record FloorDrops(IReadOnlyList<(string Group, IReadOnlyList<Share> Shares)> Groups, IReadOnlyList<string> Problems);

		public static int Run(GameDatabase database, string[] args)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
			var only = Option(args, "--drops");
			var victories = int.TryParse(Option(args, "--victories"), out var count) && count > 0 ? count : DefaultVictories;
			var seed = int.TryParse(Option(args, "--seed"), out var number) ? number : 1;
			var dungeons = database.Dungeons.Where(d => only is null || d.Id == only).ToList();
			if (dungeons.Count == 0)
			{
				Console.Error.WriteLine($"A Masmorra \"{only}\" não existe. As Masmorras: {string.Join(", ", database.Dungeons.Select(d => d.Id))}.");
				return 1;
			}

			Console.WriteLine($"{victories:N0} vitórias por andar, semente {seed}; tabela → sorteado, em %; fora de {Sigmas} desvios-padrão é erro (FORA)");
			var random = new Random(seed);
			var problems = new List<string>();
			var shares = 0;
			foreach (var dungeon in dungeons)
			{
				Console.WriteLine();
				var loot = dungeon.Kind == DungeonKind.Runes ? "runas de " + string.Join(", ", dungeon.Sets.Select(SetName)) : "Pedras de Afiar e Gemas";
				Console.WriteLine($"{dungeon.Name} ({dungeon.Id}): {loot}; abre na fase {dungeon.UnlockStage}");
				for (var floor = 1; floor <= dungeon.Floors.Count; floor++)
				{
					var drops = Inspect(dungeon, floor, victories, random);
					Console.WriteLine(Header(dungeon.Floor(floor), floor));
					foreach (var (group, parts) in drops.Groups)
						Console.WriteLine($"    {group,-11} " + string.Join("   ", parts.Select(Cell)));
					problems.AddRange(drops.Problems);
					shares += drops.Groups.Sum(g => g.Shares.Count);
				}
			}

			PrintPerDay(dungeons);

			Console.WriteLine();
			if (problems.Count == 0)
			{
				Console.WriteLine($"Tudo pela tabela: {shares} partes sorteadas e os valores fixos de {dungeons.Sum(d => d.Floors.Count)} andares.");
				return 0;
			}

			Console.WriteLine($"{problems.Count} problema(s):");
			foreach (var problem in problems)
				Console.WriteLine("  " + problem);
			return 1;
		}

		/// <summary>Vence o andar <paramref name="victories"/> vezes e compara o que caiu com a tabela dele.</summary>
		public static FloorDrops Inspect(DungeonDefinition dungeon, int number, int victories, Random random)
		{
			var floor = dungeon.Floor(number);
			var problems = FirstClear(dungeon, number, random).ToList();
			var player = Player(dungeon, dungeon.Floors.Count);
			var essence = floor.Essence + floor.Experience / Account.ExperiencePerEssenceAtMax;
			var grades = new Dictionary<int, int>();
			var rarities = new Dictionary<RuneRarity, int>();
			var sets = new Dictionary<RuneSet, int>();
			var kinds = new Dictionary<RuneToolKind, int>();
			var (complete, scrolls, cores) = (0, 0, 0);
			var fixedWrong = false;
			for (var i = 0; i < victories; i++)
			{
				player.Mana = floor.Mana;
				player.Runes.Clear();
				player.Tools.Clear();
				var reward = Dungeons.ApplyVictory(random, player, dungeon, number);
				var prize = reward.Prize ?? Prize.None;
				if (!fixedWrong && (reward.Mana != floor.Mana || reward.Essence != essence || reward.Experience != floor.Experience
					|| reward.Gold != 0 || reward.FirstClear || prize.LegendaryScrolls + prize.LightDarkScrolls != 0))
				{
					fixedWrong = true;
					problems.Add($"{dungeon.Id} andar {number}, vitória {i + 1}: {reward.Mana} Mana, {reward.Essence} Essência, {reward.Experience} XP, {reward.Gold} Ouro, {Describe(prize)}; "
						+ $"o andar diz {floor.Mana} Mana, {essence} Essência (com a da conta), {floor.Experience} XP, nada mais");
				}

				if (reward.Rune is { } rune)
				{
					Add(grades, rune.Grade);
					Add(rarities, rune.Rarity);
					Add(sets, rune.Set);
				}

				foreach (var tool in reward.Tools)
				{
					Add(rarities, tool.Grade);
					Add(kinds, tool.Kind);
				}

				var whole = dungeon.Kind == DungeonKind.Runes
					? reward.Rune != null && reward.Tools.Count == 0
					: reward.Rune == null && reward.Tools.Count == floor.ToolCount;
				complete += whole ? 1 : 0;
				scrolls += reward.Scrolls;
				cores += prize.InfusionCores;
			}

			var groups = new List<(string Group, IReadOnlyList<Share> Shares)>();
			if (dungeon.Kind == DungeonKind.Runes)
			{
				groups.Add(("quantidade", new[] { new Share("1 runa", 100, complete, victories) }));
				groups.Add(("estrelas", Shares(floor.Grades, grades, victories, grade => $"{grade}★")));
				groups.Add(("raridade", Shares(floor.Rarities, rarities, victories, RarityName)));
				groups.Add(("conjunto", Shares(dungeon.Sets.Distinct().ToDictionary(set => set, _ => 100.0 / dungeon.Sets.Distinct().Count()), sets, victories, SetName)));
			}
			else
			{
				var tools = victories * floor.ToolCount;
				groups.Add(("quantidade", new[] { new Share(floor.ToolCount == 1 ? "1 pedra" : $"{floor.ToolCount} pedras", 100, complete, victories) }));
				groups.Add(("grau", Shares(floor.Rarities, rarities, tools, RarityName)));
				groups.Add(("tipo", Shares(ToolKinds, kinds, tools, ToolName)));
			}

			groups.Add(("a mais", new[]
			{
				new Share("Pergaminho Místico", floor.ScrollChance, scrolls, victories),
				new Share("Núcleo de Infusão", floor.CoreChance, cores, victories),
			}));

			problems.AddRange(groups.SelectMany(g => g.Shares.Where(s => !s.Ok)
				.Select(s => $"{dungeon.Id} andar {number}, {g.Group} {s.Label}: {s.Observed:0.00}% em {s.Trials:N0}, a tabela diz {s.Expected:0.##}%")));
			return new FloorDrops(groups, problems);
		}

		/// <summary>A primeira vitória do andar paga o Ouro e o marco dele; o Núcleo da chance pode vir junto.</summary>
		private static IEnumerable<string> FirstClear(DungeonDefinition dungeon, int number, Random random)
		{
			var floor = dungeon.Floor(number);
			var player = Player(dungeon, number - 1);
			player.Mana = floor.Mana;
			var reward = Dungeons.ApplyVictory(random, player, dungeon, number);
			var prize = reward.Prize ?? Prize.None;
			var milestone = Milestones.ForFirstClear(floor);
			var extraCores = prize.InfusionCores - milestone.InfusionCores;
			if (!reward.FirstClear || reward.Gold != floor.FirstClearGold || prize.LegendaryScrolls != milestone.LegendaryScrolls
				|| prize.LightDarkScrolls != milestone.LightDarkScrolls || extraCores < 0 || extraCores > (floor.CoreChance > 0 ? 1 : 0))
				yield return $"{dungeon.Id} andar {number}, primeira vitória: {reward.Gold} Ouro, {Describe(prize)}; o andar diz {floor.FirstClearGold} Ouro, {Describe(milestone)}";
		}

		/// <summary>
		/// Uma conta no nível 100 (a experiência vira Essência, sem os marcos de nível no meio) que já venceu a
		/// Masmorra até o andar <paramref name="cleared"/>.
		/// </summary>
		private static PlayerState Player(DungeonDefinition dungeon, int cleared)
		{
			var player = TestData.PlayerWith("phoenix_fire");
			player.AccountLevel = Account.MaxLevel;
			player.DungeonFloors[dungeon.Id] = cleared;
			return player;
		}

		/// <summary>O que cada andar rende num dia de Mana da canalização, pela tabela (a média, sem a sorte).</summary>
		private static void PrintPerDay(IReadOnlyList<DungeonDefinition> dungeons)
		{
			Console.WriteLine();
			Console.WriteLine($"Por dia, pela tabela: {ManaPerDay:0} de Mana da canalização ({Mana.PerHour:0} por hora), toda num andar só");
			Console.WriteLine($"{"masmorra",-8}  {"andar",5}  {"vitórias",8}  {"Essência",9}  {"drop",-13}  {"6★",5}  {"Heroicas",8}  {"Lendárias",9}  {"6★ Lendárias",12}  {"Pergaminhos",11}  {"Núcleos",7}");
			foreach (var dungeon in dungeons)
			{
				for (var number = 1; number <= dungeon.Floors.Count; number++)
				{
					var floor = dungeon.Floor(number);
					var wins = ManaPerDay / floor.Mana;
					var runes = dungeon.Kind == DungeonKind.Runes;
					var drops = wins * (runes ? 1 : floor.ToolCount);
					var six = floor.Grades.GetValueOrDefault(6) / 100;
					var hero = drops * floor.Rarities.GetValueOrDefault(RuneRarity.Hero) / 100;
					var legendary = drops * floor.Rarities.GetValueOrDefault(RuneRarity.Legendary) / 100;
					Console.WriteLine($"{dungeon.Id,-8}  {number,5}  {wins,8:0.0}  {wins * floor.Essence,9:N0}  {drops,6:0.0} {(runes ? "runas " : "pedras")}"
						+ $"  {(runes ? (drops * six).ToString("0.0") : "-"),5}  {hero,8:0.0}  {legendary,9:0.0}  {(runes ? (legendary * six).ToString("0.00") : "-"),12}"
						+ $"  {wins * floor.ScrollChance / 100,11:0.00}  {wins * floor.CoreChance / 100,7:0.00}");
				}
			}

			Console.WriteLine($"A Essência é a do andar; com a conta no nível 100, cada vitória dá também um terço da experiência ({Account.ExperiencePerEssenceAtMax} por 1).");
			Console.WriteLine("Na Forja, Heroicas e Lendárias são o grau das pedras.");
		}

		private static string Header(DungeonFloor floor, int number)
		{
			var milestone = Milestones.ForFirstClear(floor);
			return $"  andar {number}  {floor.Stars}★{floor.Level}  {floor.Mana} Mana · {floor.Essence:N0} Essência (+{floor.Experience / Account.ExperiencePerEssenceAtMax:N0} com a conta no 100)"
				+ $" · {floor.Experience:N0} XP · primeira vitória: {floor.FirstClearGold} Ouro{(milestone.IsEmpty ? "" : ", " + Describe(milestone))}";
		}

		private static string Cell(Share share) =>
			$"{share.Label} {share.Expected:0.##} → {share.Observed:0.00}{(share.Ok ? "" : " FORA")}";

		/// <summary>Cada valor da tabela e cada um que caiu fora dela (chance 0%), em ordem.</summary>
		private static IReadOnlyList<Share> Shares<T>(IReadOnlyDictionary<T, double> table, Dictionary<T, int> counts, int trials, Func<T, string> label)
			where T : notnull =>
			table.Keys.Union(counts.Keys).OrderBy(key => key)
				.Select(key => new Share(label(key), table.GetValueOrDefault(key), counts.GetValueOrDefault(key), trials))
				.ToList();

		private static void Add<T>(Dictionary<T, int> counts, T key)
			where T : notnull =>
			counts[key] = counts.GetValueOrDefault(key) + 1;

		private static string Describe(Prize prize)
		{
			var parts = new List<string>();
			if (prize.InfusionCores > 0)
				parts.Add(Amount(prize.InfusionCores, "Núcleo de Infusão", "Núcleos de Infusão"));
			if (prize.LegendaryScrolls > 0)
				parts.Add(Amount(prize.LegendaryScrolls, "Pergaminho Lendário", "Pergaminhos Lendários"));
			if (prize.LightDarkScrolls > 0)
				parts.Add(Amount(prize.LightDarkScrolls, "Pergaminho de Luz e Trevas", "Pergaminhos de Luz e Trevas"));
			return parts.Count == 0 ? "sem marco" : string.Join(", ", parts);
		}

		private static string Amount(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

		private static string SetName(RuneSet set) => SetNames.Value.GetValueOrDefault(set.ToString(), set.ToString());

		private static string RarityName(RuneRarity rarity) => RarityNames.Value.GetValueOrDefault(rarity.ToString(), rarity.ToString());

		private static string ToolName(RuneToolKind kind) => kind == RuneToolKind.Grindstone ? "Pedra de Afiar" : "Gema Encantada";

		/// <summary>Os nomes do jogo (Data/texts/pt-BR.json) de um grupo de textos, pelo id.</summary>
		private static Dictionary<string, string> Names(string group)
		{
			var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
			using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Program.ProjectRoot, "Data", "texts", "pt-BR.json")), options);
			return document.RootElement.GetProperty(group).EnumerateObject()
				.Where(text => text.Value.ValueKind == JsonValueKind.String)
				.ToDictionary(text => text.Name, text => text.Value.GetString()!);
		}

		/// <summary>O valor de <c>--nome=valor</c>; nulo sem a opção ou sem o valor.</summary>
		private static string? Option(string[] args, string name) =>
			args.FirstOrDefault(arg => arg.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..];
	}
}
