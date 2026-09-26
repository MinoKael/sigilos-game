using System;
using System.Collections.Generic;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Regras das Masmorras (GDD, seção 11): a Masmorra abre depois de uma fase da Campanha, e cada andar
	/// depois do anterior. Toda vitória custa a Mana do andar (a derrota não custa nada) e dá Essência,
	/// experiência para a equipe da Masmorra e para a conta e o drop dela — sempre. A primeira vitória
	/// de cada andar dá Ouro.
	/// </summary>
	public static class Dungeons
	{
		public static bool IsUnlocked(PlayerState player, DungeonDefinition dungeon) => player.HighestStage >= dungeon.UnlockStage;

		/// <summary>Maior andar vencido. 0 = nenhum.</summary>
		public static int Cleared(PlayerState player, DungeonDefinition dungeon) =>
			player.DungeonFloors.TryGetValue(dungeon.Id, out var floor) ? floor : 0;

		public static bool IsFloorUnlocked(PlayerState player, DungeonDefinition dungeon, int floor) =>
			IsUnlocked(player, dungeon) && floor >= 1 && floor <= Math.Min(dungeon.Floors.Count, Cleared(player, dungeon) + 1);

		/// <summary>Se a luta pode começar. Não cobra nada: a Mana sai só na vitória.</summary>
		public static EntryProblem Check(PlayerState player, DungeonDefinition dungeon, int floor) =>
			Mana.Check(player, IsFloorUnlocked(player, dungeon, floor), dungeon.Floor(floor).Mana, dungeon.Kind == DungeonKind.Runes);

		public static VictoryReward ApplyVictory(Random random, PlayerState player, DungeonDefinition dungeon, int floorNumber)
		{
			var floor = dungeon.Floor(floorNumber);
			var mana = Mana.Spend(player, floor.Mana);
			var firstClear = floorNumber > Cleared(player, dungeon);
			var gold = firstClear ? floor.FirstClearGold : 0;
			if (firstClear)
				player.DungeonFloors[dungeon.Id] = floorNumber;

			player.Gold += gold;
			player.Essence += floor.Essence;
			var levelUps = Leveling.GiveExperience(player, Teams.Of(player, dungeon.Id), floor.Experience);
			var accountLevels = Account.GiveExperience(player, floor.Experience);

			Rune? rune = null;
			var tools = new List<RuneTool>();
			if (dungeon.Kind == DungeonKind.Runes)
			{
				var grade = random.Next(floor.MinGrade, floor.MaxGrade + 1);
				rune = RuneInventory.Create(random, player, grade, dungeon.Sets, floor.MinRarity);
			}
			else
			{
				for (var i = 0; i < floor.ToolCount; i++)
				{
					var tool = RuneForge.GenerateTool(random, (RuneRarity)floor.ToolGrade);
					player.Tools.Add(tool);
					tools.Add(tool);
				}
			}

			return new VictoryReward(mana, 0, gold, floor.Essence, floor.Experience, firstClear, rune, tools, levelUps, accountLevels);
		}
	}
}
