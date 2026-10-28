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

		/// <summary>O jogador entrou numa parte do jogo pela primeira vez: o jogo grava na conta (a tela não escreve no save).</summary>
		public static System.Action<Sigilos.Core.Progression.Feature>? FeatureSeen { get; set; }

		public static void Seen(Sigilos.Core.Progression.Feature feature) => FeatureSeen?.Invoke(feature);

		/// <summary>
		/// A velocidade da luta na tela, deste aparelho (o índice em <see cref="Sigilos.Core.Battle.BattlePace.Speeds"/>): a mesma no
		/// manual e no automático, e a próxima luta começa nela. Quem lê do aparelho e guarda (<see cref="BattleSpeedSaved"/>) é o GameRoot.
		/// </summary>
		public static int BattleSpeed { get; set; }

		public static System.Action<int>? BattleSpeedSaved { get; set; }

		public static void ChooseBattleSpeed(int speed)
		{
			BattleSpeed = speed;
			BattleSpeedSaved?.Invoke(speed);
		}
	}
}
