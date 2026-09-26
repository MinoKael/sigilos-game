using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O destino das cópias repetidas. Fundir uma cópia em outra da mesma variante dá +1 Eco (até 5);
	/// liberar um monstro devolve Fragmentos. Nos dois casos o monstro some e as runas dele voltam ao
	/// inventário.
	/// </summary>
	public static class Fusion
	{
		public static bool CanFuse(PlayerState player, int targetId, int materialId) =>
			targetId != materialId &&
			player.Monster(targetId) is { } target &&
			player.Monster(materialId) is { } material &&
			target.SummonId == material.SummonId &&
			target.Echoes < Growth.MaxEchoes;

		/// <summary>O material some; o alvo ganha +1 Eco. Nível, Ecos e Despertar do material se perdem.</summary>
		public static bool Fuse(PlayerState player, int targetId, int materialId)
		{
			if (!CanFuse(player, targetId, materialId))
				return false;

			player.Monster(targetId)!.Echoes++;
			Roster.Remove(player, materialId);
			return true;
		}

		/// <summary>Funde vários, na ordem, até o alvo chegar a 5 Ecos. Devolve quantos viraram Eco.</summary>
		public static int FuseMany(PlayerState player, int targetId, IEnumerable<int> materialIds) =>
			materialIds.ToList().Count(materialId => Fuse(player, targetId, materialId));

		public static int FragmentsFor(int rarity) => rarity switch
		{
			>= 5 => 20,
			4 => 10,
			_ => 5,
		};

		/// <summary>Libera o monstro em Fragmentos. Devolve quantos.</summary>
		public static int Release(PlayerState player, GameDatabase database, int monsterId)
		{
			if (player.Monster(monsterId) is not { } monster)
				return 0;

			var fragments = FragmentsFor(database.Summon(monster.SummonId).Rarity);
			Roster.Remove(player, monsterId);
			player.Fragments += fragments;
			return fragments;
		}

		/// <summary>Libera vários de uma vez. Devolve o total de Fragmentos.</summary>
		public static int ReleaseMany(PlayerState player, GameDatabase database, IEnumerable<int> monsterIds) =>
			monsterIds.ToList().Sum(id => Release(player, database, id));
	}
}
