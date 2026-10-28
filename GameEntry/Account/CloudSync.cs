using System;
using System.Security.Cryptography;
using System.Text;

namespace Sigilos.GameEntry.Account
{
	/// <summary>
	/// A última cópia do servidor que este aparelho guardou: a revisão dela e o hash do JSON. As ações e as lutas partem
	/// desta revisão; uma revisão diferente no servidor é outra cópia, que o aparelho baixa de novo.
	/// </summary>
	public sealed record SyncPoint(long Revision, string Hash);

	public static class CloudSync
	{
		public static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}
}
