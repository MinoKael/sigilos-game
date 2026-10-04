using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// A coleção de monstros e o Baú. A coleção tem <see cref="PlayerState.CollectionCapacity"/> vagas (cresce na Loja);
	/// o que passa disso vai para o Baú, que não tem limite. No Baú o monstro não luta, mas guarda as
	/// runas dele — que assim não ocupam o inventário de runas.
	/// </summary>
	public static class Roster
	{
		public static bool IsFull(PlayerState player) => player.Collection.Count() >= player.CollectionCapacity;

		/// <summary>Quantas vagas a coleção ainda tem.</summary>
		public static int FreeSlots(PlayerState player) => System.Math.Max(0, player.CollectionCapacity - player.Collection.Count());

		/// <summary>Uma cópia nova da variante, nas estrelas naturais e no nível 1: na coleção, ou no Baú se a coleção está cheia.</summary>
		public static OwnedSummon Add(PlayerState player, SummonDefinition summon)
		{
			var monster = new OwnedSummon { Id = player.NextMonsterId++, SummonId = summon.Id, Stars = summon.Rarity, Stored = IsFull(player) };
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

		/// <summary>Guarda vários no Baú (os que já estão lá ficam). Devolve quantos foram.</summary>
		public static int StoreMany(PlayerState player, IEnumerable<int> monsterIds) =>
			monsterIds.ToList().Count(id => Store(player, id));

		/// <summary>Tira vários do Baú, na ordem, enquanto a coleção tiver vaga. Devolve quantos saíram.</summary>
		public static int RetrieveMany(PlayerState player, IEnumerable<int> monsterIds) =>
			monsterIds.ToList().Count(id => Retrieve(player, id));

		/// <summary>Tira do Baú, se a coleção tem vaga.</summary>
		public static bool Retrieve(PlayerState player, int monsterId)
		{
			if (player.Monster(monsterId) is not { Stored: true } monster || IsFull(player))
				return false;

			monster.Stored = false;
			return true;
		}

		/// <summary>Tira o monstro da conta (fundido ou solto): sai das equipes e as runas voltam ao inventário.</summary>
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
