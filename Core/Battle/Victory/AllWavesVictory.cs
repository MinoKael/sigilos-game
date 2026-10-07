using System.Linq;

namespace Sigilos.Core.Battle.Victory
{
	/// <summary>A última onda sem ninguém que ainda possa agir (quem caiu para voltar ainda conta).</summary>
	internal sealed class AllWavesVictory : VictoryRule
	{
		public override bool Won(BattleSession session) =>
			session.Wave == session.WaveCount && !session.Enemies.Any(u => u.CanTakeTurn);
	}
}
