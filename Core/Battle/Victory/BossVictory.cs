using System.Linq;

namespace Sigilos.Core.Battle.Victory
{
	/// <summary>
	/// Todo chefe da luta caído de vez (o que cai para voltar ainda não conta), em qualquer onda; sem
	/// chefe, a regra comum (<see cref="AllWavesVictory"/>).
	/// </summary>
	internal sealed class BossVictory : VictoryRule
	{
		private readonly VictoryRule _fallback = new AllWavesVictory();

		public override bool Won(BattleSession session)
		{
			var bosses = session.Bosses.ToList();
			return bosses.Count > 0 ? bosses.All(boss => !boss.CanTakeTurn) : _fallback.Won(session);
		}
	}
}
