using System.Linq;
using Godot;
using Sigilos.Core.Content;

namespace Sigilos.GameEntry
{
	/// <summary>
	/// Lê Data/ pelo res:// e monta o <see cref="GameDatabase"/>. É a única peça que sabe que os
	/// dados moram em arquivos do Godot; o Core recebe só texto.
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
				pages: Read("pages.json"),
				conjurers: Read("conjurers.json"));

			foreach (var problem in database.Validate())
				GD.PushError($"Data/: {problem}");

			return database;
		}

		private static string Read(string file) => FileAccess.GetFileAsString($"{DataFolder}/{file}");
	}
}
