using System;
using System.Security.Cryptography;
using System.Text;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// A última vez que este aparelho e a nuvem tiveram o mesmo save: a revisão da nuvem naquela hora e o
	/// hash do JSON. Mudou desde então quem não bate mais com isto.
	/// </summary>
	public sealed record SyncPoint(long Revision, string Hash);

	public enum SyncAction
	{
		/// <summary>Os dois já são iguais: só anota o ponto de sincronização.</summary>
		InSync,

		/// <summary>Só o aparelho mudou (ou a nuvem está vazia): envia o local.</summary>
		Upload,

		/// <summary>Só a nuvem mudou (ou o aparelho não tem save desta conta): fica o da nuvem.</summary>
		Download,

		/// <summary>Os dois mudaram: o jogador escolhe, vendo os dois resumos.</summary>
		Ask,

		/// <summary>Nenhum dos dois tem save: conta nova, jogo novo.</summary>
		Start,
	}

	/// <summary>
	/// Quem ganha entre o save do aparelho e o da nuvem (docs/SAVE_NUVEM.md, "Sincronizar"). Só decide:
	/// quem baixa, envia e guarda backup é o AccountSession.
	///
	/// - O aparelho mudou se o hash do save não bate com o do último ponto de sincronização. Comparar o
	///   conteúdo, e não um contador, faz gravar sem mudar nada (fechar, pausar) não contar como mudança.
	/// - A nuvem mudou se a revisão dela não é a do último ponto.
	/// - Sem ponto (a primeira vez desta conta neste aparelho, ou o save sem conta que vai subir para ela),
	///   os dois contam como mudados: com save dos dois lados, o jogador escolhe.
	/// - Conteúdo igual nunca pergunta, seja qual for o ponto (o envio deu certo, mas o ponto não chegou a
	///   ser gravado).
	/// </summary>
	public static class CloudSync
	{
		public static SyncAction Decide(string? local, SyncPoint? synced, CloudCopy? cloud)
		{
			if (local == null)
				return cloud == null ? SyncAction.Start : SyncAction.Download;
			if (cloud == null)
				return SyncAction.Upload;
			if (local == cloud.Json)
				return SyncAction.InSync;

			var localChanged = Changed(local, synced);
			var cloudChanged = synced == null || cloud.Revision != synced.Revision;
			if (localChanged && cloudChanged)
				return SyncAction.Ask;
			return localChanged ? SyncAction.Upload : SyncAction.Download;
		}

		/// <summary>O save mudou desde o ponto de sincronização (sem ponto, sempre).</summary>
		public static bool Changed(string json, SyncPoint? synced) => synced == null || Hash(json) != synced.Hash;

		public static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}
}
