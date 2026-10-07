using System.Collections.Generic;
using Sigilos.Core.Content;

namespace Sigilos.Core.Battle.Victory
{
	/// <summary>A estratégia de cada <see cref="VictoryCondition"/>.</summary>
	internal static class VictoryRules
	{
		private static readonly Dictionary<VictoryCondition, VictoryRule> Table = new()
		{
			[VictoryCondition.AllWaves] = new AllWavesVictory(),
			[VictoryCondition.Boss] = new BossVictory(),
		};

		public static VictoryRule Of(VictoryCondition condition) => Table[condition];
	}
}
