using System;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Regras da campanha: qual fase está aberta e o que a vitória custa e entrega. Toda vitória custa a
	/// Mana da fase (a derrota não custa nada) e dá Essência, experiência para a equipe da Campanha e
	/// para a conta, e uma chance de runa de até 4 estrelas; a primeira vitória dá Pergaminhos (GDD,
	/// seção 12) e sempre solta uma runa. Runas maiores e pedras vêm das Masmorras.
	/// </summary>
	public static class Campaign
	{
		/// <summary>Chance de runa numa vitória repetida.</summary>
		public const double RepeatRuneChance = 0.5;

		/// <summary>Uma fase abre quando a anterior foi vencida.</summary>
		public static bool IsUnlocked(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage + 1;

		public static bool IsCleared(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage;

		/// <summary>Se a luta pode começar. Não cobra nada: a Mana sai só na vitória.</summary>
		public static EntryProblem Check(PlayerState player, StageDefinition stage) =>
			Mana.Check(player, IsUnlocked(player, stage.Number), stage.Mana, dropsRunes: true);

		public static VictoryReward ApplyVictory(Random random, PlayerState player, StageDefinition stage)
		{
			var mana = Mana.Spend(player, stage.Mana);
			var firstClear = !IsCleared(player, stage.Number);
			var scrolls = firstClear ? stage.FirstClearScrolls : 0;
			var essence = stage.Essence + (firstClear ? stage.FirstClearEssence : 0);

			player.Scrolls += scrolls;
			player.Essence += essence;
			if (firstClear)
				player.HighestStage = stage.Number;

			var levelUps = Leveling.GiveExperience(player, Teams.Of(player, Teams.Campaign), stage.Experience);
			var accountLevels = Account.GiveExperience(player, stage.Experience);
			var rune = firstClear || random.NextDouble() < RepeatRuneChance
				? RuneInventory.Create(random, player, stage.RuneGrade)
				: null;

			return new VictoryReward(mana, scrolls, 0, essence, stage.Experience, firstClear, rune, Array.Empty<RuneTool>(), levelUps, accountLevels);
		}
	}
}
