using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O save a caminho da nuvem: gzip com teto de tamanho (um arquivo pequeno que descompacta enorme não passa) e o
	/// hash do JSON, o mesmo nos dois lados.
	/// </summary>
	public static class SaveCodec
	{
		/// <summary>O maior JSON de save que o servidor guarda.</summary>
		public const int MaxBytes = 8_388_608;

		public static byte[] Compress(byte[] data)
		{
			using var output = new MemoryStream();
			using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
				gzip.Write(data, 0, data.Length);
			return output.ToArray();
		}

		/// <summary>O conteúdo do gzip, ou nulo se não é gzip válido ou passa de <paramref name="limit"/> bytes.</summary>
		public static byte[]? Decompress(byte[] data, int limit = MaxBytes) => Decompress(data, limit, out _);

		/// <summary>Como acima; <paramref name="tooLarge"/> diz se o nulo foi por passar do teto (e não por não ser gzip).</summary>
		public static byte[]? Decompress(byte[] data, int limit, out bool tooLarge)
		{
			tooLarge = false;
			try
			{
				using var input = new MemoryStream(data);
				using var gzip = new GZipStream(input, CompressionMode.Decompress);
				using var output = new MemoryStream();
				var buffer = new byte[81920];
				int read;
				while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
				{
					if (output.Length + read > limit)
					{
						tooLarge = true;
						return null;
					}

					output.Write(buffer, 0, read);
				}

				return output.ToArray();
			}
			catch (InvalidDataException)
			{
				return null;
			}
		}

		/// <summary>O SHA-256 do JSON (UTF-8) em hexadecimal.</summary>
		public static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}
}
