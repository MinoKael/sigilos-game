using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sigilos.UI.Audio
{
	/// <summary>
	/// O catálogo dos efeitos sonoros, <c>Assets/Audio/sounds.json</c>, que o Tools/sounds gera junto com os
	/// arquivos (docs/SONS.md): para cada nome lógico, os arquivos das variações. Sem Godot, para os testes
	/// lerem o mesmo arquivo que o jogo.
	/// </summary>
	public sealed class SfxCatalog
	{
		public const string Path = "res://Assets/Audio/sounds.json";

		public static readonly SfxCatalog Empty = new(new Dictionary<string, IReadOnlyList<string>>());

		private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _files;

		private SfxCatalog(IReadOnlyDictionary<string, IReadOnlyList<string>> files)
		{
			_files = files;
		}

		/// <summary>Todos os nomes lógicos (<c>ui.button_click</c>, <c>combat.damage.fire</c>...).</summary>
		public IEnumerable<string> Names => _files.Keys;

		public static SfxCatalog Parse(string json)
		{
			using var document = JsonDocument.Parse(json);
			var files = new Dictionary<string, IReadOnlyList<string>>();
			foreach (var sound in document.RootElement.GetProperty("sounds").EnumerateObject())
				files[sound.Name] = sound.Value.GetProperty("files").EnumerateArray().Select(file => file.GetString() ?? "").Where(file => file.Length > 0).ToList();
			return new SfxCatalog(files);
		}

		public bool Has(string name) => _files.ContainsKey(name);

		/// <summary>Os arquivos das variações de <paramref name="name"/>; vazio se o catálogo não tem o som.</summary>
		public IReadOnlyList<string> Files(string name) => _files.TryGetValue(name, out var files) ? files : Array.Empty<string>();
	}
}
