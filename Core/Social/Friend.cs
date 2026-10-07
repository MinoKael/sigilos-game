using System.Collections.Generic;

namespace Sigilos.Core.Social
{
	/// <summary>
	/// Outra conta na lista de amigos: o id dela (o das ações: aceitar, recusar, desfazer), o nome e, só para
	/// amigos, se está jogando agora. Nada além do nome identifica o jogador.
	/// </summary>
	public sealed record Friend(string Id, string Name, bool Online = false);

	/// <summary>
	/// Os amigos da conta e os convites, recebidos e enviados, como o servidor conta (docs/SERVIDOR_PROPRIO.md).
	/// O limite vale para os amigos mais os convites enviados que esperam resposta: o servidor recusa passar dele.
	/// </summary>
	public sealed record FriendList(IReadOnlyList<Friend> Friends, IReadOnlyList<Friend> Incoming, IReadOnlyList<Friend> Outgoing, int Max = FriendList.Limit)
	{
		/// <summary>O limite da regra, quando o servidor não manda o dele.</summary>
		public const int Limit = 50;

		/// <summary>Os lugares ocupados: amigos e convites enviados.</summary>
		public int Taken => Friends.Count + Outgoing.Count;

		public bool Full => Taken >= Max;
	}
}
