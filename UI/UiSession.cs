using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.UI
{
	/// <summary>
	/// O jogo aberto agora, para os poucos componentes que aparecem em qualquer tela e precisam dele sem
	/// que cada tela o repasse: o resumo de monstro do toque longo (<see cref="Components.MonsterSummary"/>)
	/// lê daqui as runas e as equipes do monstro. Quem preenche é o GameRoot, ao carregar; as telas
	/// continuam recebendo o que mostram pelo construtor.
	/// </summary>
	public static class UiSession
	{
		public static GameDatabase? Database { get; set; }
		public static PlayerState? Player { get; set; }

		/// <summary>
		/// A conta mudou por fora da tela aberta (a Batalha automática entregou uma vitória): o cabeçalho de
		/// recursos se atualiza sozinho, sem remontar a tela.
		/// </summary>
		public static event System.Action? Changed;

		public static void NotifyChanged() => Changed?.Invoke();
	}
}
