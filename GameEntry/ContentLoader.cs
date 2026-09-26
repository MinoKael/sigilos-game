using System.Linq;
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

		public static GameDatabase Load()
		{
			var summons = DirAccess.GetFilesAt($"{DataFolder}/summons")
				.Where(file => file.EndsWith(".json"))
				.OrderBy(file => file)
				.Select(file => Read($"summons/{file}"));

			var database = GameDatabase.FromJson(
				roles: Read("roles.json"),
				families: Read("families.json"),
				summons: summons,
				enemies: Read("enemies.json"),
				stages: Read("stages.json"),
				dungeons: Read("dungeons.json"),
				shop: Read("shop.json"));

			foreach (var problem in database.Validate())
				GD.PushError($"Data/: {problem}");

			return database;
		}

		/// <summary>Carrega Data/texts/{idioma}.json; sem o arquivo, cai para pt-BR.</summary>
		public static void LoadTexts(string language)
		{
			if (!FileAccess.FileExists($"{DataFolder}/texts/{language}.json"))
			{
				GD.PushWarning($"Sem Data/texts/{language}.json; usando pt-BR.");
				language = "pt-BR";
			}

			Locale.Load(Read($"texts/{language}.json"), language);
			foreach (var key in Texts.MissingEnumKeys())
				GD.PushError($"Data/texts/{language}.json: falta a chave {key}");
		}

		private static string Read(string file) => FileAccess.GetFileAsString($"{DataFolder}/{file}");
	}
}
