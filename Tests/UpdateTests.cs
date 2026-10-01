using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sigilos.GameEntry.Update;

namespace Sigilos.Tests
{
	/// <summary>
	/// A atualização do executável (GameEntry/Update), sem rede nem Godot: o manifesto só vale assinado,
	/// quem decide se oferece, obriga ou cala, o download conferido e a troca dos arquivos.
	/// </summary>
	internal static class UpdateTests
	{
		private static readonly byte[] Exe = Encoding.UTF8.GetBytes("o executável novo, de mentira");

		[Test]
		private static void ManifestOnlyCountsWithTheRightSignature()
		{
			var (privateKey, publicKey) = Keys();
			var manifest = Manifest();
			var envelope = UpdateManifest.Write(manifest, privateKey);

			Assert.Equal(manifest, UpdateManifest.Read(envelope, publicKey), "lê o que foi assinado");
			Assert.Equal(null, UpdateManifest.Read(envelope.Replace("0.4.0", "0.9.0"), publicKey), "mexer no manifesto quebra a assinatura");
			Assert.Equal(null, UpdateManifest.Read(envelope, Keys().Public), "assinado com outra chave não vale");
			Assert.Equal(null, UpdateManifest.Read("não é JSON", publicKey), "lixo não vale");

			var outside = UpdateManifest.Write(manifest with { File = "../Sigilos.exe" }, privateKey);
			Assert.Equal(null, UpdateManifest.Read(outside, publicKey), "o arquivo fica na pasta do manifesto, nem assinado sai dela");
		}

		[Test]
		private static void TheGameKeyIsAValidPublicKey()
		{
			using var key = ECDsa.Create();
			key.ImportFromPem(Updater.PublicKey);
			Assert.Equal(256, key.KeySize, "P-256");
		}

		[Test]
		private static void NewerVersionIsOfferedAndBelowTheMinimumIsRequired()
		{
			var manifest = Manifest();

			Assert.Equal(UpdateState.Available, Updater.Decide(new Version(0, 3, 0), manifest), "mais nova");
			Assert.Equal(UpdateState.Required, Updater.Decide(new Version(0, 2, 9), manifest), "abaixo da mínima");
			Assert.Equal(UpdateState.Current, Updater.Decide(new Version(0, 4, 0), manifest), "a mesma");
			Assert.Equal(UpdateState.Current, Updater.Decide(new Version(0, 4), manifest), "0.4 é a 0.4.0");
			Assert.Equal(UpdateState.Current, Updater.Decide(new Version(0, 5, 0), manifest), "mais nova que a publicada (uma versão de teste)");
		}

		[Test]
		private static void CheckReadsTheServerAndIgnoresWhatItCannotTrust()
		{
			var (privateKey, publicKey) = Keys();
			var server = new Server { Manifest = UpdateManifest.Write(Manifest(), privateKey) };
			var check = Run(new Updater(server.Client(), publicKey).Check(new Version(0, 3, 0)));
			Assert.Equal(UpdateState.Available, check.State, "oferece");
			Assert.Equal(new Version(0, 4, 0), check.Manifest?.Version, "a versão do servidor");

			server.Manifest = UpdateManifest.Write(Manifest() with { Platform = "android" }, privateKey);
			Assert.Equal(UpdateState.Failed, Run(new Updater(server.Client(), publicKey).Check(new Version(0, 3, 0))).State, "manifesto de outra plataforma");

			server.Manifest = null;
			Assert.Equal(UpdateState.Failed, Run(new Updater(server.Client(), publicKey).Check(new Version(0, 3, 0))).State, "nada publicado (404)");

			var offline = new Server { Unreachable = true };
			Assert.Equal(UpdateState.Failed, Run(new Updater(offline.Client(), publicKey).Check(new Version(0, 3, 0))).State, "sem rede segue o jogo");
		}

		[Test]
		private static void DownloadKeepsOnlyTheFileThatMatchesTheManifest()
		{
			var folder = Folder();
			var target = Path.Combine(folder, "Sigilos.exe.new");
			var server = new Server();
			var updater = new Updater(server.Client(), Keys().Public);
			long reported = 0;

			var done = Run(updater.Download(Manifest(), target, new Progress(bytes => reported = bytes), CancellationToken.None));
			Assert.Equal(DownloadResult.Done, done, "baixou e conferiu");
			Assert.Equal(Convert.ToBase64String(Exe), Convert.ToBase64String(File.ReadAllBytes(target)), "o arquivo inteiro");
			Assert.Equal((long)Exe.Length, reported, "a barra chega ao fim");

			var corrupt = Run(updater.Download(Manifest() with { Sha256 = new string('0', 64) }, target, null, CancellationToken.None));
			Assert.Equal(DownloadResult.Corrupt, corrupt, "SHA-256 diferente");
			Assert.False(File.Exists(target), "e o arquivo não fica");

			var shorter = Run(updater.Download(Manifest() with { Size = Exe.Length - 1 }, target, null, CancellationToken.None));
			Assert.Equal(DownloadResult.Corrupt, shorter, "veio mais que o tamanho do manifesto");

			var canceled = Run(updater.Download(Manifest(), target, null, new CancellationToken(true)));
			Assert.Equal(DownloadResult.Canceled, canceled, "cancelado");
			Assert.False(File.Exists(target), "sem sobra");

			var blocked = Run(updater.Download(Manifest(), Path.Combine(folder, "não existe", "Sigilos.exe.new"), null, CancellationToken.None));
			Assert.Equal(DownloadResult.CannotWrite, blocked, "pasta que não aceita gravar");
			Directory.Delete(folder, true);
		}

		[Test]
		private static void SwapPutsTheNewInPlaceAndCleanupRemovesTheOld()
		{
			var folder = Folder();
			var exe = Path.Combine(folder, "Sigilos.exe");
			File.WriteAllText(exe, "velho");
			File.WriteAllText(Updater.NewPath(exe), "novo");

			Updater.Swap(exe);
			Assert.Equal("novo", File.ReadAllText(exe), "o novo no lugar");
			Assert.Equal("velho", File.ReadAllText(Updater.OldPath(exe)), "o velho guardado até a próxima abertura");

			Updater.Cleanup(exe);
			Assert.False(File.Exists(Updater.OldPath(exe)), "a próxima abertura apaga o velho");

			// Sem o .new, a segunda troca falha e a primeira volta: o executável continua lá.
			var failed = false;
			try
			{
				Updater.Swap(exe);
			}
			catch (FileNotFoundException)
			{
				failed = true;
			}

			Assert.True(failed, "a troca avisa que falhou");
			Assert.Equal("novo", File.ReadAllText(exe), "e desfaz");
			Directory.Delete(folder, true);
		}

		private static UpdateManifest Manifest() =>
			new(Updater.Platform, new Version(0, 4, 0), new Version(0, 3, 0), "Sigilos-0.4.0.exe", Exe.Length, Convert.ToHexString(SHA256.HashData(Exe)).ToLowerInvariant());

		private static (string Private, string Public) Keys()
		{
			using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
			return (key.ExportPkcs8PrivateKeyPem(), key.ExportSubjectPublicKeyInfoPem());
		}

		private static string Folder()
		{
			var folder = Path.Combine(Path.GetTempPath(), "sigilos-update-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			return folder;
		}

		private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

		/// <summary>Progresso direto, sem contexto (o Progress do .NET avisaria depois, numa thread qualquer).</summary>
		private sealed class Progress(Action<long> report) : IProgress<long>
		{
			public void Report(long value) => report(value);
		}

		/// <summary>A pasta das versões no servidor: o latest.json (nulo é 404) e o executável.</summary>
		private sealed class Server : HttpMessageHandler
		{
			public string? Manifest { get; set; }

			public bool Unreachable { get; init; }

			public HttpClient Client() => new(this) { BaseAddress = new Uri("http://sigilos.test/releases/windows/"), Timeout = Timeout.InfiniteTimeSpan };

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				if (Unreachable)
					throw new HttpRequestException("sem rede");
				cancellationToken.ThrowIfCancellationRequested();

				var response = request.RequestUri!.AbsolutePath switch
				{
					"/releases/windows/latest.json" when Manifest != null => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Manifest) },
					"/releases/windows/Sigilos-0.4.0.exe" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Exe) },
					_ => new HttpResponseMessage(HttpStatusCode.NotFound),
				};
				return Task.FromResult(response);
			}
		}
	}
}
