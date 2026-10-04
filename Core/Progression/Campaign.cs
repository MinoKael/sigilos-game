using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;
using System;
using System.Collections.Generic;
using System.Linq;

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
		/// <summary>
		/// As regiões (GDD, seção 4), pela primeira fase de cada uma: a Planície dos Menires (1 a 20), o
		/// Arquipélago Afogado (21 a 40) e a Cidadela do Selo Partido (41 até a última fase).
		/// </summary>
		public static readonly IReadOnlyList<int> RegionStarts = new[] { 1, 21, 41 };

		/// <summary>A região da fase, de 0 em diante.</summary>
		public static int RegionOf(int stageNumber) => RegionStarts.Count(start => stageNumber >= start) - 1;

		/// <summary>A primeira e a última fase da região, cortada no fim da Campanha.</summary>
		public static (int First, int Last) Region(int region, int stageCount) =>
			(RegionStarts[region], region + 1 < RegionStarts.Count ? RegionStarts[region + 1] - 1 : stageCount);

		/// <summary>Chance de runa numa vitória repetida.</summary>
		public const double RepeatRuneChance = 0.5;
		public const double MonsterChance = 0.25;
		public const int MonsterRarityDrop = 2;

        /// <summary>Uma fase abre quando a anterior foi vencida.</summary>
        public static bool IsUnlocked(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage + 1;

		public static bool IsCleared(PlayerState player, int stageNumber) => stageNumber <= player.HighestStage;

		/// <summary>Se a luta pode começar. Não cobra nada: a Mana sai só na vitória.</summary>
		public static EntryProblem Check(PlayerState player, StageDefinition stage) =>
			Mana.Check(player, IsUnlocked(player, stage.Number), stage.Mana, dropsRunes: true);

		public static VictoryReward ApplyVictory(Random random, PlayerState player, StageDefinition stage, GameDatabase database)
		{
			var mana = Mana.Spend(player, stage.Mana);
			var firstClear = !IsCleared(player, stage.Number);
			var scrolls = firstClear ? stage.FirstClearScrolls : 0;
			var essence = stage.Essence + (firstClear ? stage.FirstClearEssence : 0);

			player.Scrolls += scrolls;
			player.Essence += essence;
			if (firstClear)
				player.HighestStage = stage.Number;

			var prize = firstClear ? Milestones.ForFirstClear(stage) : Prize.None;
			Milestones.Grant(player, prize);
			var levelUps = Leveling.GiveExperience(player, Teams.Of(player, Teams.Campaign), stage.Experience);
			var level = player.AccountLevel;
			var accountLevels = Account.GiveExperience(player, stage.Experience);
			prize += Milestones.ForAccountLevels(level, player.AccountLevel);
			var rune = firstClear || random.NextDouble() < RepeatRuneChance
				? RuneInventory.Create(random, player, stage.RuneGrade)
				: null;

            var pool = database.Summons.Where(s => s.Rarity == MonsterRarityDrop).ToList();
            var summon = SummonRitual.WeightedPick(random, pool);
            SummonResult? summonResult = null;
			if (random.NextDouble() < MonsterChance && summon != null)
			{
				// Primeira cópia se ainda não tinha nenhuma: tem de olhar antes de entregar esta.
				var firstCopy = !player.Owns(summon.Id);
				summonResult = new SummonResult(summon, Roster.Add(player, summon), firstCopy);
			}

			return new VictoryReward(mana, scrolls, 0, essence, stage.Experience, firstClear, rune, summonResult, Array.Empty<RuneTool>(), levelUps, accountLevels, prize);
		}
	}
}
