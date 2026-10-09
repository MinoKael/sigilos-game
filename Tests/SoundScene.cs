using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Content;
using Sigilos.UI;
using Sigilos.UI.Audio;

namespace Sigilos.Tests
{
	/// <summary>
	/// A luta em sons, para julgar a mistura e não cada arquivo sozinho (docs/SONS.md):
	/// <c>dotnet run --project Tests -- --battle-sounds=golem5</c> (a luta como no <c>--fight</c>; o padrão é
	/// golem5). Passa a luta pelo ritmo da tela (<see cref="BattlePace"/>) e pelos sons dela
	/// (<see cref="BattleSounds"/>) e diz, em cada velocidade, quantos sons tocam por minuto, quanto do tempo
	/// tem som e quantos soam juntos, com a duração de cada som no catálogo.
	///
	/// Com <c>--timeline</c>, só a lista "segundo nome" de cada som (na velocidade de <c>--speed=2</c>; o padrão
	/// é 1×), para misturar com a música e ouvir.
	/// </summary>
	internal static class SoundScene
	{
		/// <summary>O passo da contagem de sons juntos, em segundos.</summary>
		private const double Step = 0.01;

		public static int Run(GameDatabase database, string[] args)
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
			var option = args.First(a => a.StartsWith("--battle-sounds", StringComparison.Ordinal));
			var fight = option.Contains('=') ? option[(option.IndexOf('=') + 1)..] : "golem5";
			var events = CampaignReport.Fight(database, fight);
			var seconds = Durations();

			if (args.Contains("--timeline"))
			{
				var label = int.Parse(args.FirstOrDefault(a => a.StartsWith("--speed=", StringComparison.Ordinal))?["--speed=".Length..] ?? "1");
				var factor = BattlePace.Speeds.First(speed => speed.Label == label).Factor;
				foreach (var (start, name) in Timeline(events, factor))
					Console.WriteLine(FormattableString.Invariant($"{start:0.000}\t{name}"));
				return 0;
			}

			Console.WriteLine($"{fight}: {events.Count} eventos");
			var missing = new SortedSet<string>();
			foreach (var (label, factor) in BattlePace.Speeds)
			{
				var cues = Timeline(events, factor);
				missing.UnionWith(cues.Select(cue => cue.Name).Where(name => !seconds.ContainsKey(name)));
				var length = BattlePace.Seconds(events, factor);
				var spans = cues.Select(cue => (cue.Start, End: cue.Start + seconds.GetValueOrDefault(cue.Name))).ToList();
				var steps = (int)Math.Ceiling(length / Step);
				var together = new int[steps];
				foreach (var (start, end) in spans)
				{
					for (var i = (int)(start / Step); i < Math.Min(steps, (int)Math.Ceiling(end / Step)); i++)
						together[i]++;
				}

				double Share(int count) => steps == 0 ? 0 : together.Count(n => n >= count) / (double)steps;
				Console.WriteLine($"  {label}×: {TimeSpan.FromSeconds(length):m\\:ss} de luta, {cues.Count} sons ({cues.Count / Math.Max(length, 1) * 60:0} por minuto)");
				Console.WriteLine($"      som em {Share(1):P0} do tempo, dois ou mais juntos em {Share(2):P0}, três ou mais em {Share(3):P0}; no pior momento, {(steps == 0 ? 0 : together.Max())} juntos");
				var top = cues.GroupBy(cue => cue.Name).OrderByDescending(same => same.Count()).ThenBy(same => same.Key, StringComparer.Ordinal).Take(8);
				Console.WriteLine($"      os que mais tocam: {string.Join(", ", top.Select(same => $"{same.Key} {same.Count()}"))}");
			}

			if (missing.Count > 0)
				Console.WriteLine($"Fora do catálogo: {string.Join(", ", missing)}");
			return missing.Count == 0 ? 0 : 1;
		}

		/// <summary>Cada som da luta e quando começa, em segundos, com a tela acelerada por <paramref name="factor"/>.</summary>
		private static List<(double Start, string Name)> Timeline(IReadOnlyList<Core.Battle.BattleEvent> events, float factor)
		{
			var sounds = new BattleSounds();
			var cues = new List<(double Start, string Name)>();
			var now = 0.0;
			foreach (var beat in BattlePace.Beats(events))
			{
				cues.AddRange(sounds.Of(beat).Select(cue => (now + cue.Delay / factor, cue.Name)));
				now += beat.Seconds / factor;
			}

			return cues;
		}

		/// <summary>A duração de cada som do catálogo, em segundos.</summary>
		private static Dictionary<string, double> Durations()
		{
			using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Program.ProjectRoot, "Assets", "Audio", "sounds.json")));
			return document.RootElement.GetProperty("sounds").EnumerateObject().ToDictionary(sound => sound.Name, sound => sound.Value.GetProperty("seconds").GetDouble());
		}
	}
}
