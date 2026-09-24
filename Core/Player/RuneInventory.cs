using System;
using System.Linq;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// As runas e as pedras do jogador: guardar, equipar, tirar, melhorar, afiar, encantar e desfazer.
	/// Cada operação que custa Pó de Sigilo ou uma pedra devolve falso quando não dá, sem mudar nada.
	/// </summary>
	public static class RuneInventory
	{
		/// <summary>Sorteia uma runa nova e guarda no inventário.</summary>
		public static Rune Create(Random random, PlayerState player, int grade, int? slot = null)
		{
			var rune = RuneForge.Generate(random, player.NextRuneId++, grade, slot);
			player.Runes.Add(rune);
			return rune;
		}

		/// <summary>A runa que ocupa o espaço de <paramref name="rune"/> na invocação, se houver.</summary>
		public static Rune? Occupant(PlayerState player, Rune rune, string summonId) =>
			player.Runes.FirstOrDefault(r => r != rune && r.EquippedOn == summonId && r.Slot == rune.Slot);

		public static bool Equip(PlayerState player, Rune rune, string summonId)
		{
			if (Occupant(player, rune, summonId) is { } occupant)
				occupant.EquippedOn = null;
			rune.EquippedOn = summonId;
			return true;
		}

		public static bool Unequip(PlayerState player, Rune rune)
		{
			rune.EquippedOn = null;
			return true;
		}

		/// <summary>Melhora até <paramref name="target"/> de uma vez, se o Pó pagar o caminho todo. Nunca falha.</summary>
		public static bool Upgrade(Random random, PlayerState player, Rune rune, int target)
		{
			target = Math.Min(target, RuneRules.MaxLevel);
			var cost = RuneRules.UpgradeCost(rune, target);
			if (target <= rune.Level || player.Dust < cost)
				return false;

			player.Dust -= cost;
			while (rune.Level < target)
				RuneForge.RaiseLevel(random, rune);
			return true;
		}

		/// <summary>Gasta uma Pedra de Afiar igual a <paramref name="tool"/> no subatributo.</summary>
		public static bool Grind(Random random, PlayerState player, Rune rune, int index, RuneTool tool)
		{
			if (!player.Tools.Contains(tool) || !RuneForge.Grind(random, rune, index, tool))
				return false;

			player.Tools.Remove(tool);
			return true;
		}

		/// <summary>Gasta uma Gema Encantada igual a <paramref name="tool"/> no subatributo.</summary>
		public static bool Enchant(Random random, PlayerState player, Rune rune, int index, RuneTool tool)
		{
			if (!player.Tools.Contains(tool) || !RuneForge.Enchant(random, rune, index, tool))
				return false;

			player.Tools.Remove(tool);
			return true;
		}

		/// <summary>Desfaz uma runa do inventário em Pó de Sigilo. Runa equipada precisa sair antes.</summary>
		public static int Sell(PlayerState player, Rune rune)
		{
			if (rune.EquippedOn != null)
				return 0;

			var value = RuneRules.SellValue(rune);
			player.Runes.Remove(rune);
			player.Dust += value;
			return value;
		}
	}
}
