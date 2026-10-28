namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Versão do contrato entre o jogo e o servidor. Sobe quando uma regra nova (efeito, passiva, ação do servidor) exige o
	/// código novo nos dois lados; mudança só de dados (<c>Data/*.json</c>) não sobe. O servidor recusa quem está abaixo.
	/// </summary>
	public static class Protocol
	{
		/// <summary>2: luta em dois pedidos (o fim paga, depois do tempo da luta), kit de sigilos e nome pelo servidor, hora só do servidor.</summary>
		public const int Version = 2;

		/// <summary>O protocolo mais antigo que o servidor ainda atende; sobe quando uma regra muda de forma incompatível.</summary>
		public const int Minimum = 2;

		public static bool Accepts(int clientProtocol, int minimum) => clientProtocol >= minimum;
	}
}
