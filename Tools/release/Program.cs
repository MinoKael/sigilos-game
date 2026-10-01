using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Sigilos.GameEntry.Update;

namespace Sigilos.Release
{
	/// <summary>
	/// Publica uma versão do Windows para a atualização automática (docs/ATUALIZACOES.md). Da raiz do
	/// repositório:
	///
	/// <c>dotnet run --project Tools/release -- keygen</c>
	///   Cria o par de chaves, uma vez só. A privada vai para <c>%USERPROFILE%\.sigilos\update-key.pem</c>
	///   (ou <c>--key</c>, ou a variável SIGILOS_UPDATE_KEY) e nunca entra no repositório; a pública sai na
	///   tela, para <c>Updater.PublicKey</c>. Trocar a chave depois obriga todo jogador a baixar a versão
	///   nova à mão: as cópias que já estão por aí só aceitam a antiga.
	///
	/// <c>dotnet run --project Tools/release -- windows [--minimum 0.3.0] [--exe caminho] [--upload usuario@host:/pasta]</c>
	///   Depois de exportar: lê a versão do project.godot, pega <c>Releases/v&lt;versão&gt;/Windows/Sigilos.exe</c>
	///   e monta <c>Releases/v&lt;versão&gt;/Update/</c> com <c>Sigilos-&lt;versão&gt;.exe</c> e o
	///   <c>latest.json</c> assinado. <c>--minimum</c> é a menor versão que ainda pode jogar (padrão: todas);
	///   abaixo dela, o jogo só deixa atualizar ou sair. <c>--upload</c> envia pelo scp, o executável antes
	///   do manifesto.
	/// </summary>
	internal static class Program
	{
		private static int Main(string[] args)
		{
			try
			{
				return args.FirstOrDefault() switch
				{
					"keygen" => KeyGen(args),
					"windows" => Windows(args),
					_ => Usage(),
				};
			}
			catch (ReleaseException e)
			{
				Console.Error.WriteLine(e.Message);
				return 1;
			}
		}

		private static int Usage()
		{
			Console.WriteLine("Uso: dotnet run --project Tools/release -- keygen [--key caminho]");
			Console.WriteLine("     dotnet run --project Tools/release -- windows [--minimum 0.3.0] [--exe caminho] [--key caminho] [--upload usuario@host:/pasta]");
			return 1;
		}

		private static int KeyGen(string[] args)
		{
			var path = KeyPath(args);
			if (File.Exists(path))
				throw new ReleaseException($"Já existe uma chave em {path}. Trocar a chave tira a atualização de todo mundo que já tem o jogo: apague à mão, se for isso mesmo.");

			using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem() + "\n");
			Console.WriteLine($"Chave privada: {path}");
			Console.WriteLine("Guarde uma cópia fora deste computador e não a coloque no repositório.");
			Console.WriteLine();
			Console.WriteLine("Chave pública, para Updater.PublicKey (GameEntry/Update/Updater.cs):");
			Console.WriteLine(key.ExportSubjectPublicKeyInfoPem());
			return 0;
		}

		private static int Windows(string[] args)
		{
			var root = FindRoot();
			var version = GameVersion(root);
			var minimum = Option(args, "--minimum") is { } text ? ParseVersion(text, "--minimum") : new Version(0, 3, 0);
			if (minimum > version)
				throw new ReleaseException($"--minimum {minimum} é maior que a versão {version}.");

			var exe = Option(args, "--exe") ?? Path.Combine(root, "Releases", $"v{version}", "Windows", "Sigilos.exe");
			if (!File.Exists(exe))
				throw new ReleaseException($"Não achei {exe}. Exporte o Windows antes (o caminho do preset é Releases/v{version}/Windows/Sigilos.exe), ou passe --exe.");

			var privateKey = ReadKey(KeyPath(args));
			var output = Path.Combine(root, "Releases", $"v{version}", "Update");
			Directory.CreateDirectory(output);
			var name = $"Sigilos-{version}.exe";
			var copy = Path.Combine(output, name);
			File.Copy(exe, copy, overwrite: true);

			string sha256;
			using (var stream = File.OpenRead(copy))
				sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
			var manifest = new UpdateManifest(Updater.Platform, version, minimum, name, new FileInfo(copy).Length, sha256);
			var envelope = UpdateManifest.Write(manifest, privateKey);

			// A mesma leitura que o jogo faz, com a chave pública que está nele.
			if (UpdateManifest.Read(envelope, Updater.PublicKey) != manifest)
				throw new ReleaseException("O jogo não aceitaria este manifesto: confira Updater.PublicKey.");

			var latest = Path.Combine(output, Updater.ManifestName);
			File.WriteAllText(latest, envelope);
			Console.WriteLine($"Versão {version} (mínima {minimum}), {manifest.Size / 1048576.0:0.0} MB, SHA-256 {sha256}");
			Console.WriteLine($"  {copy}");
			Console.WriteLine($"  {latest}");

			if (Option(args, "--upload") is { } target)
			{
				// O executável antes: um manifesto apontando para um arquivo que ainda não chegou daria erro no download.
				Upload(copy, target);
				Upload(latest, target);
				Console.WriteLine($"Enviado para {target}.");
			}
			else
			{
				Console.WriteLine();
				Console.WriteLine("Envie para a pasta releases/windows do servidor, o executável antes do manifesto:");
				Console.WriteLine($"  scp \"{copy}\" usuario@host:/home/ubuntu/srv/sigilos/releases/windows/");
				Console.WriteLine($"  scp \"{latest}\" usuario@host:/home/ubuntu/srv/sigilos/releases/windows/");
			}

			return 0;
		}

		/// <summary>Lê a chave privada e confere que ela é o par da pública que está no jogo.</summary>
		private static string ReadKey(string path)
		{
			if (!File.Exists(path))
				throw new ReleaseException($"Não achei a chave privada em {path} (crie com keygen, ou passe --key).");

			var pem = File.ReadAllText(path);
			using var key = ECDsa.Create();
			using var game = ECDsa.Create();
			try
			{
				key.ImportFromPem(pem);
				game.ImportFromPem(Updater.PublicKey);
			}
			catch (ArgumentException)
			{
				throw new ReleaseException("A chave privada ou Updater.PublicKey não é um PEM válido.");
			}

			if (!key.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(game.ExportSubjectPublicKeyInfo()))
				throw new ReleaseException($"A chave de {path} não é a do jogo (Updater.PublicKey): o jogo recusaria a assinatura.");
			return pem;
		}

		private static void Upload(string file, string target)
		{
			using var scp = Process.Start(new ProcessStartInfo("scp") { ArgumentList = { file, target }, UseShellExecute = false })
				?? throw new ReleaseException("Não deu para abrir o scp.");
			scp.WaitForExit();
			if (scp.ExitCode != 0)
				throw new ReleaseException($"O scp falhou ({scp.ExitCode}) ao enviar {Path.GetFileName(file)}.");
		}

		private static string KeyPath(string[] args) =>
			Option(args, "--key")
			?? Environment.GetEnvironmentVariable("SIGILOS_UPDATE_KEY")
			?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sigilos", "update-key.pem");

		/// <summary>A versão do jogo (<c>application/config/version</c>), a mesma que ele compara ao abrir.</summary>
		private static Version GameVersion(string root)
		{
			var match = Regex.Match(File.ReadAllText(Path.Combine(root, "project.godot")), "^config/version=\"([^\"]+)\"", RegexOptions.Multiline);
			if (!match.Success)
				throw new ReleaseException("project.godot não tem application/config/version.");
			return ParseVersion(match.Groups[1].Value, "config/version");
		}

		private static Version ParseVersion(string text, string what) =>
			Version.TryParse(text, out var version) && version.Build >= 0
				? version
				: throw new ReleaseException($"{what}: \"{text}\" não é uma versão como 0.4.0.");

		private static string? Option(string[] args, string name)
		{
			var index = Array.IndexOf(args, name);
			return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
		}

		/// <summary>A raiz do repositório (onde está o project.godot), subindo de onde o comando rodou.</summary>
		private static string FindRoot()
		{
			foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
			{
				for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
				{
					if (File.Exists(Path.Combine(dir.FullName, "project.godot")))
						return dir.FullName;
				}
			}

			throw new ReleaseException("Não achei o project.godot: rode da pasta do jogo.");
		}

		private sealed class ReleaseException(string message) : Exception(message);
	}
}
