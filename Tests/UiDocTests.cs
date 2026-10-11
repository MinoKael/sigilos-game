using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sigilos.Tests
{
	/// <summary>
	/// Os exemplos da documentação da interface (docs/ui) batem com o código: cada bloco <c>csharp</c> está,
	/// linha por linha, em UI/Examples/UiExamples.cs, que o build do jogo compila. Os testes não compilam
	/// código da Godot, então a conferência é essa: o build garante que o arquivo compila, e este teste
	/// garante que a documentação mostra o arquivo.
	/// </summary>
	internal static class UiDocTests
	{
		private static readonly Regex Block = new("```csharp\\r?\\n(.*?)```", RegexOptions.Singleline);

		/// <summary>As linhas sem os espaços das pontas e sem as vazias, entre quebras: o recuo não conta.</summary>
		private static string Normalize(string text) =>
			"\n" + string.Join("\n", text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)) + "\n";

		[Test]
		private static void ExamplesCompile()
		{
			var root = Program.ProjectRoot;
			var examples = Normalize(File.ReadAllText(Path.Combine(root, "UI", "Examples", "UiExamples.cs")));
			var docs = Directory.GetFiles(Path.Combine(root, "docs", "ui"), "*.md", SearchOption.AllDirectories);
			Assert.True(docs.Length > 0, "docs/ui sem páginas");

			var problems = new List<string>();
			var blocks = 0;
			foreach (var doc in docs)
			{
				foreach (Match match in Block.Matches(File.ReadAllText(doc)))
				{
					blocks++;
					var snippet = Normalize(match.Groups[1].Value);
					if (!examples.Contains(snippet))
						problems.Add($"{Path.GetRelativePath(root, doc)}: {snippet.Trim().Split('\n')[0]}");
				}
			}

			Assert.True(blocks > 0, "docs/ui sem exemplos csharp");
			Assert.Empty(problems, "exemplos de docs/ui fora de UI/Examples/UiExamples.cs");
		}
	}
}
