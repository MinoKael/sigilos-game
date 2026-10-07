using System.Text.Json;
using Sigilos.Core.Social;
using Sigilos.GameEntry.Account;

namespace Sigilos.Tests
{
	/// <summary>Os amigos do lado do jogo: a lista como o servidor manda (docs/SERVIDOR_PROPRIO.md) e os lugares do limite.</summary>
	internal static class FriendsTests
	{
		[Test]
		private static void TheListReadsAsTheServerSendsIt()
		{
			using var json = JsonDocument.Parse("""
				{
				  "max": 50,
				  "friends": [{ "id": "a", "name": "Aprendiz", "online": true }, { "id": "b", "name": "Andarilho", "online": false }],
				  "incoming": [{ "id": "c", "name": "Viajante" }, { "id": "", "name": "Sem id" }, { "id": "d" }],
				  "outgoing": [{ "id": "e", "name": "Peregrino" }]
				}
				""");
			var list = CloudFriends.Read(json.RootElement)!;

			Assert.Equal(2, list.Friends.Count, "os amigos");
			Assert.Equal(true, list.Friends[0].Online, "jogando agora");
			Assert.Equal(false, list.Friends[1].Online, "fora do jogo");
			Assert.Equal(1, list.Incoming.Count, "quem vem sem id ou sem nome fica de fora");
			Assert.Equal("Peregrino", list.Outgoing[0].Name, "os convites enviados");
			Assert.Equal(3, list.Taken, "amigos e convites enviados ocupam lugar; os recebidos, não");
			Assert.Equal(false, list.Full, "sobra lugar");

			using var bare = JsonDocument.Parse("{}");
			Assert.Equal(FriendList.Limit, CloudFriends.Read(bare.RootElement)!.Max, "sem max, vale o limite da regra");
			using var wrong = JsonDocument.Parse("[]");
			Assert.Equal(null, CloudFriends.Read(wrong.RootElement), "o que não é objeto não é lista");
		}

		[Test]
		private static void TheLimitCountsFriendsAndInvitesSent()
		{
			var one = new[] { new Friend("a", "Aprendiz") };
			Assert.Equal(true, new FriendList(one, new Friend[0], one, 2).Full, "um amigo e um convite enviado enchem dois lugares");
			Assert.Equal(false, new FriendList(one, one, new Friend[0], 2).Full, "o convite recebido não ocupa lugar");
		}
	}
}
