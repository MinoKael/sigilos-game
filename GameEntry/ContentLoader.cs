using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Sigilos.Core.Content;
using Sigilos.UI;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Lê Data/ pelo res:// e monta o <see cref="GameDatabase"/> e os textos da interface. É a única
	/// peça que sabe que os dados moram em arquivos do Godot; o Core e a UI recebem só texto.
	/// </summary>
	public static class ContentLoader
	{
		private const string DataFolder = "res://Data";

		/// <summary>
		/// Monta o banco de dados com os nomes no idioma dos textos carregados: carregue os textos
		/// (<see cref="LoadTexts"/>) antes, e de novo depois de trocar de idioma.
		/// </summary>
		public static GameDatabase Load()
		{
			// Um arquivo por família, com as variantes dela dentro.
			var families = DirAccess.GetFilesAt($"{DataFolder}/summons")
				.Where(file => file.EndsWith(".json"))
				.OrderBy(file => file)
				.Select(file => Localize(Read($"summons/{file}")));

			var database = GameDatabase.FromJson(
				statModel: Read("stat_model.json"),
				families: families,
				enemies: Localize(Read("enemies.json")),
				stages: Localize(Read("stages.json")),
				dungeons: Localize(Read("dungeons.json")),
				shop: Localize(Read("shop.json")),
				exploration: Localize(Read("exploration.json")));

			foreach (var problem in database.Validate())
				GD.PushError($"Data/: {problem}");

			return database;
		}

		/// <summary>O idioma base: todo texto e todo nome dos dados nascem em português; os outros arquivos são traduções.</summary>
		public const string BaseLanguage = "pt-BR";

		/// <summary>Carrega Data/texts/{idioma}.json; sem o arquivo, cai para a base.</summary>
		public static void LoadTexts(string language)
		{
			if (!FileAccess.FileExists($"{DataFolder}/texts/{language}.json"))
			{
				GD.PushWarning($"Sem Data/texts/{language}.json; usando {BaseLanguage}.");
				language = BaseLanguage;
			}

			Locale.Load(Read($"texts/{language}.json"), language);
			foreach (var key in Texts.MissingEnumKeys())
				GD.PushError($"Data/texts/{language}.json: falta a chave {key}");
		}

		/// <summary>Os idiomas que existem: um por arquivo em Data/texts, a base primeiro.</summary>
		public static IReadOnlyList<string> Languages()
		{
			var names = DirAccess.GetFilesAt($"{DataFolder}/texts")
				.Where(file => file.EndsWith(".json", StringComparison.Ordinal))
				.Select(file => file[..^".json".Length])
				.OrderBy(name => name != BaseLanguage)
				.ThenBy(name => name, StringComparer.Ordinal)
				.ToList();
			return names.Count == 0 ? new[] { BaseLanguage } : names;
		}

		/// <summary>
		/// Troca todo "name" e "base_name" de um arquivo de dados pelo nome no idioma de agora
		/// (<see cref="Locale.Name"/>). Na base não há o que trocar, e o texto passa como veio.
		/// </summary>
		private static string Localize(string json)
		{
			if (!Locale.HasNames)
				return json;

			var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
			Rename(root);
			return root?.ToJsonString() ?? json;
		}

		private static void Rename(JsonNode? node)
		{
			switch (node)
			{
				case JsonObject item:
					foreach (var (key, value) in item.ToList())
					{
						if (key is "name" or "base_name" && value is JsonValue text && text.TryGetValue<string>(out var name))
							item[key] = Locale.Name(name);
						else
							Rename(value);
					}

					break;
				case JsonArray list:
					foreach (var element in list)
						Rename(element);
					break;
			}
		}

		private static string Read(string file) => FileAccess.GetFileAsString($"{DataFolder}/{file}");
	}
}
