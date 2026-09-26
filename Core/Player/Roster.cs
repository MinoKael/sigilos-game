using System.Linq;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// A coleção de monstros e o Baú. A coleção tem <see cref="PlayerState.CollectionCapacity"/> vagas;
	/// o que passa disso vai para o Baú, que não tem limite. No Baú o monstro não luta, mas guarda as
	/// runas dele — que assim não ocupam o inventário de runas.
	/// </summary>
	public static class Roster
	{
		public static bool IsFull(PlayerState player) => player.Collection.Count() >= PlayerState.CollectionCapacity;

		/// <summary>Uma cópia nova da variante, no nível 1: na coleção, ou no Baú se a coleção está cheia.</summary>
		public static OwnedSummon Add(PlayerState player, string summonId)
		{
			var monster = new OwnedSummon { Id = player.NextMonsterId++, SummonId = summonId, Stored = IsFull(player) };
			player.Monsters.Add(monster);
			return monster;
		}

		/// <summary>Guarda no Baú: sai de todas as equipes e leva as runas junto.</summary>
		public static bool Store(PlayerState player, int monsterId)
		{
			if (player.Monster(monsterId) is not { Stored: false } monster)
				return false;

			Teams.Leave(player, monsterId);
			monster.Stored = true;
			return true;
		}

		/// <summary>Tira do Baú, se a coleção tem vaga.</summary>
		public static bool Retrieve(PlayerState player, int monsterId)
		{
			if (player.Monster(monsterId) is not { Stored: true } monster || IsFull(player))
				return false;

			monster.Stored = false;
			return true;
		}

		/// <summary>Tira o monstro da conta (fundido ou liberado): sai das equipes e as runas voltam ao inventário.</summary>
		public static bool Remove(PlayerState player, int monsterId)
		{
			if (player.Monster(monsterId) is not { } monster)
				return false;

			Teams.Leave(player, monsterId);
			RuneInventory.UnequipAll(player, monsterId);
			player.Monsters.Remove(monster);
			return true;
		}
	}
}
