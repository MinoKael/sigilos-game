using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// O destino das cópias repetidas. Fundir uma cópia em outra da mesma variante sobe uma habilidade
	/// sorteada em um nível, entre as que ainda não estão no máximo (a do Despertar só entra depois de
	/// despertar); liberar devolve Fragmentos, que pagam a evolução. Nos dois casos o monstro some e as
	/// runas dele voltam ao inventário.
	/// </summary>
	public static class Fusion
	{
		/// <summary>Índices das habilidades que ainda sobem, na ordem de <see cref="SummonDefinition.AllSkills"/>.</summary>
		public static IReadOnlyList<int> Upgradable(GameDatabase database, OwnedSummon monster)
		{
			var skills = database.Summon(monster.SummonId).SkillsFor(monster.Awakened);
			return Enumerable.Range(0, skills.Count).Where(i => monster.SkillLevel(i) < skills[i].MaxLevel).ToList();
		}

		/// <summary>Quantas cópias ainda viram nível de habilidade.</summary>
		public static int SkillUpsLeft(GameDatabase database, OwnedSummon monster)
		{
			var skills = database.Summon(monster.SummonId).SkillsFor(monster.Awakened);
			return Enumerable.Range(0, skills.Count).Sum(i => Math.Max(0, skills[i].MaxLevel - monster.SkillLevel(i)));
		}

		public static bool CanFuse(PlayerState player, GameDatabase database, int targetId, int materialId) =>
			targetId != materialId &&
			player.Monster(targetId) is { } target &&
			player.Monster(materialId) is { } material &&
			target.SummonId == material.SummonId &&
			Upgradable(database, target).Count > 0;

		/// <summary>O material some; uma habilidade sorteada do alvo sobe. Devolve o índice dela, ou -1.</summary>
		public static int Fuse(Random random, PlayerState player, GameDatabase database, int targetId, int materialId)
		{
			if (!CanFuse(player, database, targetId, materialId))
				return -1;

			var target = player.Monster(targetId)!;
			var options = Upgradable(database, target);
			var index = options[random.Next(options.Count)];
			target.RaiseSkill(index);
			Roster.Remove(player, materialId);
			return index;
		}

		/// <summary>Funde vários, na ordem, até as habilidades do alvo chegarem ao máximo. Devolve quantos subiram.</summary>
		public static int FuseMany(Random random, PlayerState player, GameDatabase database, int targetId, IEnumerable<int> materialIds) =>
			materialIds.ToList().Count(materialId => Fuse(random, player, database, targetId, materialId) >= 0);

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
