using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Update
{
	public enum UpdateState
	{
		/// <summary>Esta já é a versão mais nova.</summary>
		Current,

		/// <summary>Há uma mais nova: o jogador escolhe se atualiza agora.</summary>
		Available,

		/// <summary>Esta ficou abaixo da mínima: atualizar ou sair.</summary>
		Required,

		/// <summary>Sem resposta, ou manifesto sem assinatura que confira: segue o jogo como está.</summary>
		Failed,
	}

	public enum DownloadResult
	{
		Done,
		Unreachable,

		/// <summary>Veio com outro tamanho ou outro SHA-256 que o do manifesto: descartado.</summary>
		Corrupt,

		/// <summary>A pasta do jogo não aceita gravar (sem permissão, como em Arquivos de Programas, ou sem espaço).</summary>
		CannotWrite,

		Canceled,
	}

	public sealed record UpdateCheck(UpdateState State, UpdateManifest? Manifest);

	/// <summary>
	/// A atualização do executável do Windows (docs/ATUALIZACOES.md), sem Godot:
	/// - <see cref="Check"/> lê <c>latest.json</c> da pasta das versões no servidor e confere a assinatura com
	///   a chave pública daqui (<see cref="PublicKey"/>).
	/// - <see cref="Download"/> baixa o executável novo para <c>Sigilos.exe.new</c>, ao lado do atual, e
	///   confere tamanho e SHA-256.
	/// - <see cref="Swap"/> troca os dois. O Windows não deixa apagar um executável aberto, mas deixa
	///   renomear: o atual vira <c>.old</c>, apagado na abertura seguinte (<see cref="Cleanup"/>).
	/// - <see cref="Relaunch"/> abre o novo só depois que este fechar: ao abrir, o Godot troca a cópia do
	///   .NET que extrai para o cache, e os arquivos dela ficam presos enquanto este processo existir.
	/// </summary>
	public sealed class Updater
	{
		public const string Platform = "windows";
		public const string ManifestName = "latest.json";

		/// <summary>A chave pública de quem publica (Tools/release). A privada fica só no computador dele.</summary>
		public const string PublicKey = """
			-----BEGIN PUBLIC KEY-----
			MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEZ2fDWqJ1A2UzGnzIuWHYy/bZ8yPT
			ILGWTAIgpUR5bC0TkMN04DbaeJ5FFJX5jhrbmcH8f1vBJgVrtz3x84Yd+Q==
			-----END PUBLIC KEY-----
			""";

		private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

		/// <summary>Quanto o download pode ficar parado antes de desistir (o arquivo inteiro pode levar minutos).</summary>
		private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

		/// <summary>De quanto em quanto a barra de progresso anda: avisar a cada pedaço lido encheria a fila da tela.</summary>
		private const long ProgressStep = 1 << 20;

		private readonly HttpClient _http;
		private readonly string _publicKey;

		/// <param name="http">Com a pasta das versões em <c>BaseAddress</c> (terminada em /) e sem tempo limite: quem mede é esta classe.</param>
		public Updater(HttpClient http, string publicKey = PublicKey)
		{
			_http = http;
			_publicKey = publicKey;
		}

		public async Task<UpdateCheck> Check(Version current)
		{
			string envelope;
			try
			{
				using var timeout = new CancellationTokenSource(CheckTimeout);
				using var response = await _http.GetAsync(ManifestName, timeout.Token).ConfigureAwait(false);
				if (!response.IsSuccessStatusCode)
					return new UpdateCheck(UpdateState.Failed, null);
				envelope = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
			}
			catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
			{
				return new UpdateCheck(UpdateState.Failed, null);
			}

			var manifest = UpdateManifest.Read(envelope, _publicKey);
			return manifest is { Platform: Platform }
				? new UpdateCheck(Decide(current, manifest), manifest)
				: new UpdateCheck(UpdateState.Failed, null);
		}

		/// <summary>O endereço do executável do manifesto (para baixar pelo navegador, quando a pasta do jogo não aceita gravar).</summary>
		public Uri Address(UpdateManifest manifest) => new(_http.BaseAddress!, manifest.File);

		public static UpdateState Decide(Version current, UpdateManifest manifest)
		{
			var here = Full(current);
			if (Full(manifest.Version) <= here)
				return UpdateState.Current;
			return here < Full(manifest.Minimum) ? UpdateState.Required : UpdateState.Available;
		}

		/// <summary>
		/// Baixa o arquivo do manifesto para <paramref name="target"/>, avisando quantos bytes já vieram. Só
		/// fica no disco se tamanho e SHA-256 conferirem; em qualquer outro fim, o arquivo é apagado.
		/// </summary>
		public async Task<DownloadResult> Download(UpdateManifest manifest, string target, IProgress<long>? progress, CancellationToken cancel)
		{
			FileStream output;
			try
			{
				output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous);
			}
			catch (Exception e) when (e is UnauthorizedAccessException or IOException)
			{
				return DownloadResult.CannotWrite;
			}

			var done = false;
			try
			{
				await using (output.ConfigureAwait(false))
				{
					// Fora do contexto do Godot (ConfigureAwait): lá cada volta do laço esperaria um quadro.
					using var response = await _http.GetAsync(manifest.File, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
					if (!response.IsSuccessStatusCode)
						return DownloadResult.Unreachable;

					var input = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
					await using (input.ConfigureAwait(false))
					{
						using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
						var buffer = new byte[1 << 16];
						long total = 0, reported = 0;
						while (true)
						{
							using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancel);
							idle.CancelAfter(IdleTimeout);
							var read = await input.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
							if (read == 0)
								break;
							total += read;
							if (total > manifest.Size)
								return DownloadResult.Corrupt;
							hash.AppendData(buffer, 0, read);
							await output.WriteAsync(buffer.AsMemory(0, read), cancel).ConfigureAwait(false);
							if (total - reported >= ProgressStep)
							{
								reported = total;
								progress?.Report(total);
							}
						}

						progress?.Report(total);
						done = total == manifest.Size && Convert.ToHexString(hash.GetHashAndReset()).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase);
						return done ? DownloadResult.Done : DownloadResult.Corrupt;
					}
				}
			}
			catch (OperationCanceledException) when (cancel.IsCancellationRequested)
			{
				return DownloadResult.Canceled;
			}
			catch (OperationCanceledException)
			{
				// O servidor parou de mandar (IdleTimeout).
				return DownloadResult.Unreachable;
			}
			catch (HttpRequestException)
			{
				return DownloadResult.Unreachable;
			}
			catch (IOException e) when (e is HttpIOException || e.InnerException is SocketException)
			{
				return DownloadResult.Unreachable;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return DownloadResult.CannotWrite;
			}
			finally
			{
				if (!done)
					TryDelete(target);
			}
		}

		public static string NewPath(string exe) => exe + ".new";

		public static string OldPath(string exe) => exe + ".old";

		/// <summary>Põe o <c>.new</c> no lugar do executável; o atual vira <c>.old</c>. Se a segunda troca falhar, desfaz a primeira.</summary>
		public static void Swap(string exe)
		{
			var old = OldPath(exe);
			if (File.Exists(old))
				File.Delete(old);
			File.Move(exe, old);
			try
			{
				File.Move(NewPath(exe), exe);
			}
			catch
			{
				File.Move(old, exe);
				throw;
			}
		}

		/// <summary>Apaga o que sobrou de uma atualização: o executável velho e um download pela metade.</summary>
		public static void Cleanup(string exe)
		{
			TryDelete(OldPath(exe));
			TryDelete(NewPath(exe));
		}

		/// <summary>
		/// Abre <paramref name="exe"/> quando o processo <paramref name="waitFor"/> (este) terminar, por um
		/// PowerShell escondido que espera e sai. Falso se nem ele abriu: aí o jogador abre o jogo de novo.
		/// </summary>
		public static bool Relaunch(string exe, int waitFor, IReadOnlyList<string> arguments)
		{
			static string Quote(string text) => "'" + text.Replace("'", "''") + "'";

			var path = Path.GetFullPath(exe);
			var command = $"Wait-Process -Id {waitFor} -Timeout 60 -ErrorAction SilentlyContinue; "
				+ $"Start-Process -FilePath {Quote(path)} -WorkingDirectory {Quote(Path.GetDirectoryName(path)!)}"
				+ (arguments.Count > 0 ? " -ArgumentList " + string.Join(",", arguments.Select(Quote)) : "");
			var shell = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
			{
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", command })
				shell.ArgumentList.Add(argument);

			try
			{
				using var process = Process.Start(shell);
				return process != null;
			}
			catch (Exception e) when (e is Win32Exception or InvalidOperationException)
			{
				return false;
			}
		}

		/// <summary>0.4 e 0.4.0 são a mesma versão (o <see cref="Version"/> do .NET acha a primeira menor).</summary>
		private static Version Full(Version version) =>
			new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

		private static void TryDelete(string path)
		{
			try
			{
				File.Delete(path);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Ainda preso (o processo velho fechando): fica para a próxima abertura.
			}
		}
	}
}
