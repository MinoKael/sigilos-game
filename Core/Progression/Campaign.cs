using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Regras da campanha: qual fase está aberta e o que a vitória entrega. A primeira vitória de cada
	/// fase dá Pergaminhos (GDD, seção 12); toda vitória dá Essência.
	/// </summary>
	public static class Campaign
	{
		/// <summary>Uma fase abre quando a anterior foi vencida.</summary>
		public static bool IsUnlocked(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage + 1;

		public static bool IsCleared(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage;

		public static StageReward ApplyVictory(PlayerState player, StageDefinition stage)
		{
			var firstClear = !IsCleared(player, stage.Number);
			var scrolls = firstClear ? stage.FirstClearScrolls : 0;
			var essence = stage.Essence + (firstClear ? stage.FirstClearEssence : 0);

			player.Scrolls += scrolls;
			player.Essence += essence;
			if (firstClear)
				player.HighestStage = stage.Number;

			return new StageReward(scrolls, essence, firstClear);
		}
	}
}
