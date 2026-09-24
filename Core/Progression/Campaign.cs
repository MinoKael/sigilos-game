using System;
using System.Collections.Generic;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Regras da campanha: qual fase está aberta e o que a vitória entrega. A primeira vitória de cada
	/// fase dá Pergaminhos (GDD, seção 12) e sempre solta uma runa; toda vitória dá Essência, Pó de
	/// Sigilo, experiência para o time e uma chance de runa. Fases com
	/// grau de pedra soltam uma Pedra de Afiar ou Gema na primeira vitória e às vezes depois.
	/// </summary>
	public static class Campaign
	{
		/// <summary>Chance de runa numa vitória repetida.</summary>
		public const double RepeatRuneChance = 0.5;

		/// <summary>Chance de pedra numa vitória repetida, nas fases que soltam pedra.</summary>
		public const double RepeatToolChance = 0.15;

		/// <summary>Uma fase abre quando a anterior foi vencida.</summary>
		public static bool IsUnlocked(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage + 1;

		public static bool IsCleared(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage;

		public static StageReward ApplyVictory(Random random, PlayerState player, StageDefinition stage)
		{
			var firstClear = !IsCleared(player, stage.Number);
			var scrolls = firstClear ? stage.FirstClearScrolls : 0;
			var essence = stage.Essence + (firstClear ? stage.FirstClearEssence : 0);

			player.Scrolls += scrolls;
			player.Essence += essence;
			player.Dust += stage.Dust;
			if (firstClear)
				player.HighestStage = stage.Number;

			var levelUps = new List<string>();
			foreach (var id in player.Team)
			{
				if (player.Owns(id) && Leveling.AddExperience(player.Summon(id), stage.Experience) > 0)
					levelUps.Add(id);
			}

			var rune = firstClear || random.NextDouble() < RepeatRuneChance
				? RuneInventory.Create(random, player, stage.RuneGrade)
				: null;

			RuneTool? tool = null;
			if (stage.ToolGrade > 0 && (firstClear || random.NextDouble() < RepeatToolChance))
			{
				tool = RuneForge.GenerateTool(random, (RuneRarity)stage.ToolGrade);
				player.Tools.Add(tool);
			}

			return new StageReward(scrolls, essence, stage.Dust, stage.Experience, firstClear, rune, tool, levelUps);
		}
	}
}
