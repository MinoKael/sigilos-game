using System;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// As runas do jogador: guardar, equipar, melhorar, refazer e desfazer. Cada operação que custa Pó
	/// de Sigilo devolve falso quando não dá para pagar, sem mudar nada.
	/// </summary>
	public static class RuneInventory
	{
		/// <summary>Sorteia uma runa nova e guarda no inventário.</summary>
		public static Rune Create(Random random, PlayerState player, int grade)
		{
			var rune = RuneForge.Generate(random, player.NextRuneId++, grade);
			player.Runes.Add(rune);
			return rune;
		}

		/// <summary>Põe a runa na invocação. A que ocupava o mesmo espaço volta ao inventário.</summary>
		public static void Equip(PlayerState player, Rune rune, string summonId)
		{
			foreach (var other in player.Runes)
			{
				if (other != rune && other.EquippedOn == summonId && other.Slot == rune.Slot)
					other.EquippedOn = null;
			}

			rune.EquippedOn = summonId;
		}

		public static void Unequip(Rune rune) => rune.EquippedOn = null;

		public static bool Upgrade(Random random, PlayerState player, Rune rune)
		{
			var cost = RuneRules.UpgradeCost(rune);
			if (rune.Level >= RuneRules.MaxLevel || player.Dust < cost)
				return false;

			player.Dust -= cost;
			RuneForge.RaiseLevel(random, rune);
			return true;
		}

		public static bool Reroll(Random random, PlayerState player, Rune rune, int substatIndex)
		{
			var cost = RuneRules.RerollCost(rune);
			if (player.Dust < cost || substatIndex < 0 || substatIndex >= rune.Substats.Count)
				return false;

			player.Dust -= cost;
			RuneForge.Reroll(random, rune, substatIndex);
			return true;
		}

		/// <summary>Desfaz a runa em Pó de Sigilo. Devolve quanto Pó rendeu.</summary>
		public static int Sell(PlayerState player, Rune rune)
		{
			var value = RuneRules.SellValue(rune);
			player.Runes.Remove(rune);
			player.Dust += value;
			return value;
		}
	}
}
