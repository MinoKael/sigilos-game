using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sigilos.Sounds.Library;
using Sigilos.Sounds.Mastering;
using Sigilos.Sounds.Synth;

namespace Sigilos.Sounds
{
	/// <summary>
	/// Gera a biblioteca de efeitos sonoros (docs/SONS.md): cada efeito é sintetizado do zero, sem nenhum
	/// arquivo de áudio de fora, e sai em <c>Assets/Audio/&lt;pasta&gt;/&lt;nome&gt;.wav</c> (16 bits, mono),
	/// com o catálogo <c>Assets/Audio/sounds.json</c>. Da raiz do repositório:
	///
	/// <c>dotnet run --project Tools/sounds -c Release</c>
	///   Gera tudo e apaga os WAV que não são mais de nenhum efeito.
	///
	/// <c>--only=ui</c>, <c>--only=combat/</c>, <c>--only=ui/button_click,combat.damage.fire</c>
	///   Só uma pasta (exata), uma pasta com as de dentro (terminada em /) ou efeitos soltos (com / ou .).
	///
	/// <c>--seed=7451</c>  a semente (a mesma semente dá os mesmos bytes; trocar muda a biblioteca inteira).
	/// <c>--out=pasta</c>  outra pasta de saída (padrão: Assets/Audio do projeto).
	/// <c>--rate=44100</c> a taxa de amostragem.
	/// <c>--list</c>       só lista os efeitos.
	/// <c>--report</c>     mostra, por arquivo, duração, pico, volume percebido e quanto da energia está
	///                     no grave (abaixo de 150 Hz) e no agudo (acima de 6 kHz), e avisa o que passou do ponto.
	/// </summary>
	internal static class Program
	{
		private static int Main(string[] args)
		{
			Options options;
			try
			{
				options = Options.Parse(args);
			}
			catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
			{
				Console.Error.WriteLine(e.Message);
				return Usage();
			}
			if (options.Help)
				return Usage();

			var catalog = SoundCatalog.All();
			if (options.List)
			{
				foreach (var def in catalog)
					Console.WriteLine($"{def.Id,-40} {def.Variations}x {MixProfile.Of(def.Mix).Name,-7} {def.Description}");
				Console.WriteLine($"{catalog.Count} efeitos, {catalog.Sum(def => def.Variations)} arquivos.");
				return 0;
			}

			var selected = Select(catalog, options.Only, out var unknown);
			if (unknown.Count > 0)
			{
				Console.Error.WriteLine($"Nenhum efeito para: {string.Join(", ", unknown)} (veja --list).");
				return 1;
			}

			var root = FindRoot();
			var outDir = Path.GetFullPath(options.Out ?? Path.Combine(root, "Assets", "Audio"));
			var clock = Stopwatch.StartNew();
			var removed = Clean(outDir, selected, catalog, full: options.Only.Count == 0);
			var results = Generate(selected, outDir, options);
			var listed = Manifest.Write(outDir, root, catalog);

			var bytes = results.Sum(result => result.Bytes);
			Console.WriteLine($"{selected.Count} efeitos, {results.Count} arquivos ({bytes / 1024.0 / 1024.0:0.0} MB) em {clock.Elapsed.TotalSeconds:0.0} s, semente {options.Seed}.");
			if (removed > 0)
				Console.WriteLine($"{removed} arquivos velhos apagados.");
			Console.WriteLine($"Catálogo: {Path.Combine(outDir, Manifest.FileName)} ({listed} efeitos).");
			if (options.Report)
				Report(results);
			return 0;
		}

		private static int Usage()
		{
			Console.WriteLine("Uso: dotnet run --project Tools/sounds -c Release -- [--only=ui,combat/,ui/button_click] [--seed=7451] [--out=pasta] [--rate=44100] [--list] [--report]");
			return 1;
		}

		/// <summary>Os efeitos que <paramref name="tokens"/> pedem (todos, sem nenhum); <paramref name="unknown"/> são os que não acharam nada.</summary>
		private static List<SoundDef> Select(IReadOnlyList<SoundDef> catalog, IReadOnlyList<string> tokens, out List<string> unknown)
		{
			unknown = new List<string>();
			if (tokens.Count == 0)
				return catalog.ToList();
			var picked = new HashSet<SoundDef>();
			foreach (var raw in tokens)
			{
				var token = raw.Replace('.', '/').Replace('\\', '/');
				var matches = catalog.Where(def => token.EndsWith('/') ? def.Id.StartsWith(token, StringComparison.Ordinal) : def.Id == token || def.Category == token).ToList();
				if (matches.Count == 0)
					unknown.Add(raw);
				picked.UnionWith(matches);
			}
			return catalog.Where(picked.Contains).ToList();
		}

		/// <summary>
		/// Apaga as variações que sobraram dos efeitos escolhidos (um efeito que tinha 3 e agora tem 2) e, numa
		/// geração completa, todo WAV da biblioteca que não é de nenhum efeito. Leva junto o <c>.import</c>.
		/// </summary>
		private static int Clean(string outDir, IReadOnlyList<SoundDef> selected, IReadOnlyList<SoundDef> catalog, bool full)
		{
			var removed = 0;
			foreach (var def in selected)
			{
				var folder = Path.Combine(outDir, def.Category);
				if (!Directory.Exists(folder))
					continue;
				var mine = new Regex($"^{Regex.Escape(def.Name)}(_\\d+)?\\.wav$");
				var expected = Enumerable.Range(0, def.Variations).Select(v => def.FileName(v)).ToHashSet();
				foreach (var path in Directory.GetFiles(folder, "*.wav"))
				{
					var file = Path.GetFileName(path);
					if (mine.IsMatch(file) && !expected.Contains(file))
						removed += Delete(path);
				}
			}
			if (full && Directory.Exists(outDir))
			{
				var known = catalog.SelectMany(def => Enumerable.Range(0, def.Variations).Select(v => Path.GetFullPath(Path.Combine(outDir, def.Category, def.FileName(v))))).ToHashSet(StringComparer.OrdinalIgnoreCase);
				foreach (var path in Directory.GetFiles(outDir, "*.wav", SearchOption.AllDirectories))
					if (!known.Contains(Path.GetFullPath(path)))
						removed += Delete(path);
			}
			return removed;
		}

		private static int Delete(string wav)
		{
			File.Delete(wav);
			if (File.Exists(wav + ".import"))
				File.Delete(wav + ".import");
			return 1;
		}

		private static List<Result> Generate(IReadOnlyList<SoundDef> selected, string outDir, Options options)
		{
			foreach (var category in selected.Select(def => def.Category).Distinct())
				Directory.CreateDirectory(Path.Combine(outDir, category));
			var jobs = selected.SelectMany(def => Enumerable.Range(0, def.Variations).Select(v => (Def: def, Variation: v))).ToList();
			var results = new ConcurrentBag<Result>();
			Parallel.ForEach(jobs, job =>
			{
				var (def, variation) = job;
				var patch = new Patch(new Rng(Rng.SeedFor(options.Seed, def.Id, variation)), variation, options.Rate);
				def.Build(patch);
				var profile = MixProfile.Of(def.Mix);
				var samples = Master.Finish(patch.Render(), options.Rate, profile, def.LevelDb);
				var path = Path.Combine(outDir, def.Category, def.FileName(variation));
				WavFile.Write(path, samples, options.Rate, new Rng(Rng.SeedFor(options.Seed, def.Id + "#dither", variation)));
				results.Add(new Result(def, variation, samples, options.Rate, 44 + samples.Length * 2));
			});
			var order = selected.Select((def, index) => (def, index)).ToDictionary(pair => pair.def, pair => pair.index);
			return results.OrderBy(result => order[result.Def]).ThenBy(result => result.Variation).ToList();
		}

		private static void Report(IReadOnlyList<Result> results)
		{
			Console.WriteLine();
			Console.WriteLine($"{"arquivo",-46} {"seg",5} {"pico",6} {"vol",6} {"alvo",6} {"grave",6} {"agudo",6}");
			var warnings = new List<string>();
			foreach (var result in results)
			{
				var profile = MixProfile.Of(result.Def.Mix);
				var target = profile.LoudnessDb + result.Def.LevelDb;
				var loudness = Loudness.Of(result.Samples, result.Rate);
				var peak = Loudness.Db(LayerRenderer.Peak(result.Samples));
				var low = Loudness.ShareBelow(result.Samples, result.Rate, 150);
				var high = Loudness.ShareAbove(result.Samples, result.Rate, 6000);
				var name = $"{result.Def.Category}/{result.Def.FileName(result.Variation)}";
				Console.WriteLine($"{name,-46} {result.Seconds,5:0.00} {peak,6:0.0} {loudness,6:0.0} {target,6:0.0} {low * 100,5:0}% {high * 100,5:0}%");
				if (peak > profile.CeilingDb + 0.05)
					warnings.Add($"{name}: pico {peak:0.0} dB acima do teto {profile.CeilingDb} dB");
				if (loudness < target - 3)
					warnings.Add($"{name}: {target - loudness:0.0} dB abaixo do alvo (o limitador segurou: muito pico para pouco corpo)");
				if (low > 0.45)
					warnings.Add($"{name}: {low * 100:0}% da energia abaixo de 150 Hz");
				if (high > (result.Def.Mix is Mix.Ui or Mix.Combat ? 0.2 : 0.3))
					warnings.Add($"{name}: {high * 100:0}% da energia acima de 6 kHz");
			}
			Console.WriteLine();
			Console.WriteLine(warnings.Count == 0 ? "Nenhum aviso." : $"{warnings.Count} avisos:");
			foreach (var warning in warnings)
				Console.WriteLine("  " + warning);
		}

		/// <summary>A pasta do project.godot, subindo a partir da pasta atual.</summary>
		private static string FindRoot()
		{
			for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
				if (File.Exists(Path.Combine(dir.FullName, "project.godot")))
					return dir.FullName;
			throw new InvalidOperationException("Não achei o project.godot: rode de dentro do repositório.");
		}

		private sealed record Result(SoundDef Def, int Variation, double[] Samples, int Rate, long Bytes)
		{
			public double Seconds => (double)Samples.Length / Rate;
		}
	}
}
