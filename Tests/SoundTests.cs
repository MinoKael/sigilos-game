using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.UI;
using Sigilos.UI.Audio;

namespace Sigilos.Tests
{
	/// <summary>
	/// Os efeitos sonoros no jogo (docs/SONS.md): o catálogo aponta para arquivos que existem, todo nome
	/// que o código toca está no catálogo, e a luta vira os sons certos (<see cref="BattleSounds"/>).
	/// </summary>
	internal static class SoundTests
	{
		/// <summary>Um nome de som no código: categoria do catálogo, ponto, o resto em minúsculas.</summary>
		private static readonly Regex SoundLiteral = new("\"((?:ui|combat|summon|rewards|grimoire|constellation|progression)(?:\\.[a-z0-9_]+)+)\"");

		private static readonly SkillDefinition Wave = new()
		{
			Name = "Onda",
			Cooldown = 3,
			Effects = TestData.Strike.Effects,
		};

		private static readonly SkillDefinition Deluge = new()
		{
			Name = "Dilúvio",
			Cooldown = BattleSounds.GrandCooldown,
			Effects = TestData.Strike.Effects,
		};

		private static SfxCatalog Catalog() => SfxCatalog.Parse(File.ReadAllText(Path.Combine(Program.ProjectRoot, "Assets", "Audio", "sounds.json")));

		/// <summary>Os sons dos eventos, momento a momento, como a tela da luta toca.</summary>
		private static List<Cue> Sounds(BattleSounds sounds, params BattleEvent[] events) => BattlePace.Beats(events).SelectMany(sounds.Of).ToList();

		private static List<string> Names(IEnumerable<Cue> cues) => cues.Select(cue => cue.Name).ToList();

		/// <summary>Os nomes numa linha, para comparar a sequência inteira.</summary>
		private static string Line(IEnumerable<string> names) => string.Join(" ", names);

		[Test]
		private static void CatalogFilesExist()
		{
			var catalog = Catalog();
			var problems = new List<string>();
			foreach (var name in catalog.Names)
			{
				if (catalog.Files(name).Count == 0)
					problems.Add($"{name}: sem arquivo");
				foreach (var file in catalog.Files(name))
				{
					if (!file.StartsWith("res://") || !File.Exists(Path.Combine(Program.ProjectRoot, file["res://".Length..])))
						problems.Add($"{name}: {file}");
				}
			}

			Assert.Empty(problems, "arquivos do catálogo que não existem");
		}

		/// <summary>
		/// Todo "categoria.nome" que o código da UI e do GameEntry escreve é um som do catálogo, ou uma chave
		/// de texto (as duas têm a mesma cara: "summon.title" é texto).
		/// </summary>
		[Test]
		private static void CodeOnlyPlaysCatalogSounds()
		{
			var catalog = Catalog();
			var texts = new HashSet<string>();
			using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Program.ProjectRoot, "Data", "texts", "pt-BR.json")), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
				Flatten(document.RootElement, "", texts);

			var found = 0;
			var problems = new List<string>();
			foreach (var folder in new[] { "UI", "GameEntry" })
			{
				foreach (var path in Directory.EnumerateFiles(Path.Combine(Program.ProjectRoot, folder), "*.cs", SearchOption.AllDirectories))
				{
					foreach (Match match in SoundLiteral.Matches(File.ReadAllText(path)))
					{
						var name = match.Groups[1].Value;
						if (texts.Contains(name))
							continue;
						found++;
						if (!catalog.Has(name))
							problems.Add($"{Path.GetFileName(path)}: {name}");
					}
				}
			}

			Assert.Empty(problems, "sons fora do catálogo");
			Assert.True(found > 30, $"o código toca os sons pelo nome ({found} achados)");
		}

		private static void Flatten(JsonElement node, string prefix, HashSet<string> keys)
		{
			foreach (var property in node.EnumerateObject())
			{
				var key = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
				if (property.Value.ValueKind == JsonValueKind.Object)
					Flatten(property.Value, key, keys);
				else
					keys.Add(key);
			}
		}

	}
}
