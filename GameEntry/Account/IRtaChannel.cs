using Sigilos.Core.Rta;
using System.Threading.Tasks;

namespace Sigilos.GameEntry.Account
{
	/// <summary>Por onde o jogador fala com uma partida da Arena do Véu: a conexão com o servidor (<see cref="RtaLink"/>) ou o treino local (<see cref="LocalRta"/>).</summary>
	public interface IRtaChannel
	{
		bool Live { get; }

		/// <summary>Manda uma mensagem. Falso se não deu para mandar.</summary>
		Task<bool> Send(RtaMessage message);
	}
}
