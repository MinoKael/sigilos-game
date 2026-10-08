using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// Regras das Masmorras (GDD, seção 11): a Masmorra abre depois de uma fase da Campanha, e cada andar
	/// depois do anterior. Toda vitória custa a Mana do andar (a derrota não custa nada) e dá Essência,
	/// experiência para a equipe da Masmorra e para a conta e o drop dela — sempre —, e tem a chance do
	/// andar de soltar também um Pergaminho Místico. A primeira vitória de cada andar dá Ouro.
	/// </summary>
	public static class Dungeons
	{
		/// <summary>
		/// Aberta depois da fase dela, ou se o jogador já venceu algum andar: o que já foi vencido nunca
		/// fecha de novo, mesmo que a fase que abre a Masmorra mude nos dados.
		/// </summary>
		public static bool IsUnlocked(PlayerState player, DungeonDefinition dungeon) =>
			player.HighestStage >= dungeon.UnlockStage || Cleared(player, dungeon) > 0;

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
			var prize = firstClear ? Milestones.ForFirstClear(floor) : Prize.None;
			var levelUps = Leveling.GiveExperience(player, Teams.Of(player, dungeon.Id), floor.Experience);
			var level = player.AccountLevel;
			var account = Account.GiveExperience(player, floor.Experience);
			prize += Milestones.ForAccountLevels(level, player.AccountLevel);

			Rune? rune = null;
			var tools = new List<RuneTool>();
			if (dungeon.Kind == DungeonKind.Runes)
			{
				var grade = Roll(random, floor.Grades);
				rune = RuneInventory.Create(random, player, grade, dungeon.Sets, Roll(random, floor.Rarities));
			}
			else
			{
				for (var i = 0; i < floor.ToolCount; i++)
				{
					var tool = RuneForge.GenerateTool(random, Roll(random, floor.Rarities));
					player.Tools.Add(tool);
					tools.Add(tool);
				}
			}

			// Sorteado depois do drop, para o Pergaminho não mudar as runas e pedras que cada semente dá.
			var scrolls = random.NextDouble() * 100 < floor.ScrollChance ? 1 : 0;
			player.Scrolls += scrolls;

			// O Núcleo de toda vitória é sorteado por último, e só nos andares que têm chance dele.
			var core = floor.CoreChance > 0 && random.NextDouble() * 100 < floor.CoreChance ? new Prize(InfusionCores: 1) : Prize.None;
			Milestones.Grant(player, (firstClear ? Milestones.ForFirstClear(floor) : Prize.None) + core);
			prize += core;

			return new VictoryReward(mana, scrolls, gold, floor.Essence + account.Essence, floor.Experience, firstClear, rune, null, tools, levelUps, account.Levels, prize);
		}

		/// <summary>Sorteia pela tabela de chances do andar (em %), na ordem das chaves.</summary>
		private static T Roll<T>(Random random, IReadOnlyDictionary<T, double> chances)
			where T : notnull
		{
			var ordered = chances.OrderBy(c => c.Key).ToList();
			var roll = random.NextDouble() * ordered.Sum(c => c.Value);
			foreach (var (value, chance) in ordered)
			{
				roll -= chance;
				if (roll < 0)
					return value;
			}

			return ordered.Last(c => c.Value > 0).Key;
		}
	}
}
