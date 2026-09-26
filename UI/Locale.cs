using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Godot;

namespace Sigilos.UI
{
	/// <summary>
	/// Todo texto da interface mora em Data/texts/{idioma}.json, por chave. O arquivo é aninhado para
	/// ficar legível (<c>{"runes": {"grind": "Grind"}}</c>) e a chave junta os nomes com ponto
	/// (<c>runes.grind</c>). Os marcadores {0}, {1}... recebem os valores, como em
	/// <see cref="string.Format(System.IFormatProvider, string, object[])"/>.
	///
	/// A base é en.json. Para traduzir, copie en.json com outro nome, troque os textos e rode o jogo com
	/// <c>-- --language=nome</c>. Chave que falta aparece entre ‹ › na tela e no console.
	/// </summary>
	public static class Locale
	{
		private static readonly Dictionary<string, string> Texts = new();
		private static readonly HashSet<string> Reported = new();

		/// <summary>Formato de números e datas do idioma.</summary>
		public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

		public static void Load(string json, string language)
		{
			Texts.Clear();
			using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
			Flatten(document.RootElement, "");
			try
			{
				Culture = CultureInfo.GetCultureInfo(language);
			}
			catch (CultureNotFoundException)
			{
				Culture = CultureInfo.InvariantCulture;
			}
		}

		public static bool Has(string key) => Texts.ContainsKey(key);

		public static string T(string key)
		{
			if (Texts.TryGetValue(key, out var text))
				return text;

			if (Reported.Add(key))
				GD.PushWarning($"Texto sem tradução: {key}");
			return $"‹{key}›";
		}

		public static string T(string key, params object[] args) => string.Format(Culture, T(key), args);

		private static void Flatten(JsonElement element, string prefix)
		{
			foreach (var property in element.EnumerateObject())
			{
				var key = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
				if (property.Value.ValueKind == JsonValueKind.Object)
					Flatten(property.Value, key);
				else
					Texts[key] = property.Value.GetString() ?? "";
			}
		}
	}
}
