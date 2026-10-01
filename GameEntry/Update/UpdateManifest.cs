using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sigilos.GameEntry.Update
{
	/// <summary>
	/// O que o servidor diz da versão mais nova (docs/ATUALIZACOES.md): qual é, a mínima que ainda vale, o
	/// arquivo e o SHA-256 dele. Só vale assinado pela chave de quem publica, que nunca sai do computador
	/// dele: quem invadir o servidor não consegue mandar outro executável, porque não consegue assinar.
	///
	/// O arquivo publicado (<c>latest.json</c>) é um envelope: o manifesto vai como texto, para a assinatura
	/// valer sobre os bytes exatos dele, e a assinatura (ECDSA P-256 com SHA-256) vai do lado.
	/// <code>
	/// {
	///   "manifest": "{\"platform\":\"windows\",\"version\":\"0.4.0\",\"minimum\":\"0.3.0\",...}",
	///   "signature": "Base64..."
	/// }
	/// </code>
	/// Quem escreve é Tools/release (<see cref="Write"/>); quem lê é o jogo (<see cref="Read"/>).
	/// </summary>
	public sealed partial record UpdateManifest(string Platform, Version Version, Version Minimum, string File, long Size, string Sha256)
	{
		/// <summary>Legível: aspas como \" e não " (o arquivo não vai para página nenhuma).</summary>
		private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

		/// <summary>O envelope assinado com a chave privada (PEM): o conteúdo de <c>latest.json</c>.</summary>
		public static string Write(UpdateManifest manifest, string privateKeyPem)
		{
			var text = JsonSerializer.Serialize(new
			{
				platform = manifest.Platform,
				version = manifest.Version.ToString(),
				minimum = manifest.Minimum.ToString(),
				file = manifest.File,
				size = manifest.Size,
				sha256 = manifest.Sha256.ToLowerInvariant(),
			});
			using var key = ECDsa.Create();
			key.ImportFromPem(privateKeyPem);
			var signature = key.SignData(Encoding.UTF8.GetBytes(text), HashAlgorithmName.SHA256);
			return JsonSerializer.Serialize(new { manifest = text, signature = Convert.ToBase64String(signature) }, Indented) + "\n";
		}

		/// <summary>
		/// O manifesto do envelope, se a assinatura confere com a chave pública (PEM) e o conteúdo é válido.
		/// Nulo em qualquer outro caso: o jogo trata como "não há atualização".
		/// </summary>
		public static UpdateManifest? Read(string envelope, string publicKeyPem)
		{
			try
			{
				using var outer = JsonDocument.Parse(envelope);
				var text = outer.RootElement.GetProperty("manifest").GetString();
				var signature = Convert.FromBase64String(outer.RootElement.GetProperty("signature").GetString() ?? "");
				if (text == null)
					return null;

				using var key = ECDsa.Create();
				key.ImportFromPem(publicKeyPem);
				if (!key.VerifyData(Encoding.UTF8.GetBytes(text), signature, HashAlgorithmName.SHA256))
					return null;

				using var inner = JsonDocument.Parse(text);
				var root = inner.RootElement;
				var manifest = new UpdateManifest(
					root.GetProperty("platform").GetString() ?? "",
					Version.Parse(root.GetProperty("version").GetString() ?? ""),
					Version.Parse(root.GetProperty("minimum").GetString() ?? ""),
					root.GetProperty("file").GetString() ?? "",
					root.GetProperty("size").GetInt64(),
					root.GetProperty("sha256").GetString() ?? "");
				return manifest.Valid ? manifest : null;
			}
			catch (Exception e) when (e is JsonException or FormatException or ArgumentException or InvalidOperationException
				or KeyNotFoundException or CryptographicException or OverflowException)
			{
				return null;
			}
		}

		/// <summary>Um nome de arquivo simples (fica na pasta do manifesto), tamanho e SHA-256 com cara de verdade.</summary>
		private bool Valid => SafeName().IsMatch(File) && Size > 0 && Hex().IsMatch(Sha256);

		[GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
		private static partial Regex SafeName();

		[GeneratedRegex("^[0-9a-fA-F]{64}$")]
		private static partial Regex Hex();
	}
}
