using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sigilos.Sounds.Mastering;

namespace Sigilos.Sounds.Library
{
	/// <summary>
	/// O catálogo que o jogo lê (<c>sounds.json</c>, na pasta da biblioteca): para cada nome lógico
	/// (<c>combat.damage.fire</c>), a pasta, a descrição, a classe de mixagem, os arquivos das variações
	/// (o jogo sorteia entre eles), a duração e o pico. É refeito inteiro a cada geração, lendo os WAV que
	/// estão no disco: gerar só um grupo não tira os outros do catálogo.
	/// </summary>
	public static class Manifest
	{
		public const string FileName = "sounds.json";

		/// <summary>Grava o catálogo em <paramref name="outDir"/>; os caminhos saem como <c>res://</c> quando a pasta está dentro de <paramref name="root"/>.</summary>
		public static int Write(string outDir, string root, IReadOnlyList<SoundDef> catalog)
		{
			var prefix = ResPrefix(outDir, root);
			var written = 0;
			using var stream = new MemoryStream();
			using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
			{
				json.WriteStartObject();
				json.WriteString("generator", "Tools/sounds (docs/SONS.md)");
				json.WriteStartObject("sounds");
				foreach (var def in catalog)
				{
					var files = Enumerable.Range(0, def.Variations).Select(v => def.FileName(v)).Where(file => File.Exists(Path.Combine(outDir, def.Category, file))).ToList();
					if (files.Count == 0)
						continue;
					var seconds = 0.0;
					var peak = 0.0;
					foreach (var file in files)
					{
						var (samples, rate) = WavFile.Read(Path.Combine(outDir, def.Category, file));
						seconds = Math.Max(seconds, (double)samples.Length / rate);
						foreach (var s in samples)
							peak = Math.Max(peak, Math.Abs(s));
					}
					json.WriteStartObject(def.LogicalName);
					json.WriteString("category", def.Category);
					json.WriteString("description", def.Description);
					json.WriteString("mix", MixProfile.Of(def.Mix).Name);
					json.WriteStartArray("files");
					foreach (var file in files)
						json.WriteStringValue($"{prefix}{def.Category}/{file}");
					json.WriteEndArray();
					json.WriteNumber("seconds", Math.Round(seconds, 3));
					json.WriteNumber("peak_db", Math.Round(Loudness.Db(peak), 1));
					json.WriteEndObject();
					written++;
				}
				json.WriteEndObject();
				json.WriteEndObject();
			}
			var text = System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n";
			File.WriteAllText(Path.Combine(outDir, FileName), text);
			return written;
		}

		/// <summary><c>res://Assets/Audio/</c> para uma pasta dentro do projeto; fora dele, caminhos relativos ao catálogo.</summary>
		private static string ResPrefix(string outDir, string root)
		{
			var relative = Path.GetRelativePath(root, outDir);
			if (relative.StartsWith("..") || Path.IsPathRooted(relative))
				return "";
			return "res://" + (relative == "." ? "" : relative.Replace('\\', '/') + "/");
		}
	}
}
