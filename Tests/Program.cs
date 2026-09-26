using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Sigilos.Tests
{
	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class TestAttribute : Attribute
	{
	}

	/// <summary>
	/// Runner mínimo: acha os métodos estáticos marcados com [Test], roda cada um no seu próprio
	/// try/catch e lista tudo o que falhou — uma rodada mostra todas as falhas, não só a primeira.
	///
	/// Uso (na raiz do repositório):
	///   dotnet run --project Tests                  roda os testes
	///   dotnet run --project Tests -- --only=Summon só os testes com "Summon" no nome
	///   dotnet run --project Tests -- --simulate    relatório de balanceamento da campanha
	///   dotnet run --project Tests -- --fight=10    uma luta da fase 10, turno a turno
	/// </summary>
	internal static class Program
	{
		/// <summary>A raiz do repositório: a pasta que tem o project.godot.</summary>
		public static string ProjectRoot { get; } = FindProjectRoot();

		private static int Main(string[] args)
		{
			if (args.Contains("--simulate"))
			{
				CampaignReport.Print(TestData.LoadReal());
				return 0;
			}

			var battle = args.FirstOrDefault(a => a.StartsWith("--fight=", StringComparison.Ordinal));
			if (battle != null)
			{
				CampaignReport.PrintBattle(TestData.LoadReal(), int.Parse(battle["--fight=".Length..]));
				return 0;
			}

			var filter = args.FirstOrDefault(a => a.StartsWith("--only=", StringComparison.Ordinal))?["--only=".Length..];

			var tests = typeof(Program).Assembly.GetTypes()
				.SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
				.Where(method => method.GetCustomAttribute<TestAttribute>() != null)
				.Select(method => (Name: $"{method.DeclaringType!.Name}.{method.Name}", Method: method))
				.Where(test => filter is null || test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
				.OrderBy(test => test.Name, StringComparer.Ordinal)
				.ToList();

			if (tests.Count == 0)
			{
				Console.Error.WriteLine("Nenhum teste encontrado (o --only filtrou tudo?).");
				return 1;
			}

			var failures = 0;
			foreach (var (name, method) in tests)
			{
				try
				{
					method.Invoke(null, null);
					Console.WriteLine($"PASS  {name}");
				}
				catch (Exception exception)
				{
					failures++;
					var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
					Console.WriteLine($"FAIL  {name}");
					Console.WriteLine($"      >>> {cause.Message.Replace("\n", "\n          ")}");
					if (cause is not AssertionException)
						Console.WriteLine($"      {cause.GetType().Name} {cause.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
				}
			}

			Console.WriteLine();
			Console.WriteLine(failures == 0 ? $"{tests.Count} testes, todos passaram." : $"{failures} de {tests.Count} testes falharam.");
			return failures == 0 ? 0 : 1;
		}

		private static string FindProjectRoot()
		{
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("project.godot não encontrado acima do executável.");
		}
	}
}
