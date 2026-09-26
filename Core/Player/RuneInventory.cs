using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// As runas e as pedras do jogador: guardar, equipar, tirar, melhorar, afiar, encantar e desfazer.
	/// Cada operação que custa Essência ou uma pedra devolve falso quando não dá, sem mudar nada.
	///
	/// O inventário (as runas soltas) tem <see cref="Capacity"/> vagas. Runa equipada não conta — nem
	/// a de monstro no Baú, que guarda as runas dele. Com o inventário cheio, as lutas que soltam runa
	/// esperam (Core/Progression/Mana) e tirar runa de um monstro também.
	/// </summary>
	public static class RuneInventory
	{
		public const int Capacity = 800;

		/// <summary>Runas soltas, fora de qualquer monstro.</summary>
		public static int Count(PlayerState player) => player.Runes.Count(r => r.EquippedOn == null);

		public static bool IsFull(PlayerState player) => Count(player) >= Capacity;

		/// <summary>Sorteia uma runa nova e guarda no inventário.</summary>
		public static Rune Create(
			Random random,
			PlayerState player,
			int grade,
			IReadOnlyList<RuneSet>? sets = null,
			RuneRarity minRarity = RuneRarity.Normal)
		{
			var rune = RuneForge.Generate(random, player.NextRuneId++, grade, null, sets, minRarity);
			player.Runes.Add(rune);
			return rune;
		}

		/// <summary>A runa que ocupa o espaço de <paramref name="rune"/> no monstro, se houver.</summary>
		public static Rune? Occupant(PlayerState player, Rune rune, int monsterId) =>
			player.Runes.FirstOrDefault(r => r != rune && r.EquippedOn == monsterId && r.Slot == rune.Slot);

		/// <summary>
		/// Põe a runa no monstro, na coleção ou no Baú; a que ocupava o espaço volta ao inventário. Vinda
		/// de outro monstro, a troca só acontece se a que sai cabe no inventário.
		/// </summary>
		public static bool Equip(PlayerState player, Rune rune, int monsterId)
		{
			if (player.Monster(monsterId) == null || rune.EquippedOn == monsterId)
				return false;

			var occupant = Occupant(player, rune, monsterId);
			if (occupant != null && rune.EquippedOn != null && IsFull(player))
				return false;

			if (occupant != null)
				occupant.EquippedOn = null;
			rune.EquippedOn = monsterId;
			return true;
		}

		/// <summary>Tira a runa do monstro, se cabe no inventário.</summary>
		public static bool Unequip(PlayerState player, Rune rune)
		{
			if (rune.EquippedOn == null || IsFull(player))
				return false;

			rune.EquippedOn = null;
			return true;
		}

		/// <summary>Tira todas as runas do monstro, mesmo passando da capacidade (o monstro saiu da conta).</summary>
		public static void UnequipAll(PlayerState player, int monsterId)
		{
			foreach (var rune in player.Runes.Where(r => r.EquippedOn == monsterId))
				rune.EquippedOn = null;
		}

		/// <summary>Melhora até <paramref name="target"/> de uma vez, se a Essência pagar o caminho todo. Nunca falha.</summary>
		public static bool Upgrade(Random random, PlayerState player, Rune rune, int target)
		{
			target = Math.Min(target, RuneRules.MaxLevel);
			var cost = RuneRules.UpgradeCost(rune, target);
			if (target <= rune.Level || player.Essence < cost)
				return false;

			player.Essence -= cost;
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

		/// <summary>Desfaz uma runa do inventário em Essência. Runa equipada precisa sair antes.</summary>
		public static int Sell(PlayerState player, Rune rune)
		{
			if (rune.EquippedOn != null)
				return 0;

			var value = RuneRules.SellValue(rune);
			player.Runes.Remove(rune);
			player.Essence += value;
			return value;
		}

		/// <summary>Desfaz várias de uma vez; as equipadas ficam. Devolve a Essência total.</summary>
		public static int SellAll(PlayerState player, IEnumerable<Rune> runes) => runes.ToList().Sum(rune => Sell(player, rune));
	}
}
