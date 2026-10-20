using System.Text;
using Sigilos.Core.Player;

namespace Sigilos.Tests
{
	/// <summary>O gzip do save na nuvem: ida e volta, o teto contra arquivo que explode e o hash igual nos dois lados.</summary>
	internal static class SaveCodecTests
	{
		[Test]
		private static void CompressedSaveComesBackIdentical()
		{
			var json = PlayerSave.ToJson(NewGame.Create(System.DateTime.UnixEpoch, new System.Random(1), TestData.Database));
			var bytes = Encoding.UTF8.GetBytes(json);
			var packed = SaveCodec.Compress(bytes);
			Assert.True(packed.Length < bytes.Length / 2, "o JSON encolhe");
			Assert.Equal(json, Encoding.UTF8.GetString(SaveCodec.Decompress(packed)!), "volta igual");
		}

		[Test]
		private static void DecompressRefusesGarbageAndBombs()
		{
			Assert.True(SaveCodec.Decompress(Encoding.UTF8.GetBytes("{\"Version\":1}")) == null, "JSON puro não é gzip");

			var bomb = SaveCodec.Compress(new byte[SaveCodec.MaxBytes + 1]);
			Assert.True(bomb.Length < 100_000, "o arquivo é pequeno");
			Assert.True(SaveCodec.Decompress(bomb) == null, "mas descompacta além do teto");
			Assert.True(SaveCodec.Decompress(bomb, SaveCodec.MaxBytes + 10) != null, "dentro do teto passa");
		}

		[Test]
		private static void HashIsTheSha256OfTheUtf8Text()
		{
			Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", SaveCodec.Hash(""), "vazio");
			Assert.Equal(SaveCodec.Hash("ação"), SaveCodec.Hash("ação"), "estável");
		}
	}
}
