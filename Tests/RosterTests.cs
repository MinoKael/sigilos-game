using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>Coleção, Baú, fusão de cópias e equipes por conteúdo.</summary>
	internal static class RosterTests
	{
		[Test]
		private static void ChestKeepsMonstersOutOfTeamsAndBackWhenThereIsRoom()
		{
			var player = TestData.PlayerWith("wolf_fire", "wolf_water");
			var id = player.Monsters[0].Id;
			Teams.Toggle(player, "golem", id);
			var rune = RuneInventory.Create(new Random(1), player, 2);
			RuneInventory.Equip(player, rune, id);

			Assert.True(Roster.Store(player, id), "vai para o Baú");
			Assert.Equal(id, rune.EquippedOn, "leva as runas junto");
			Assert.False(Teams.Of(player, Teams.Campaign).Contains(id), "sai da equipe da Campanha");
			Assert.False(Teams.Of(player, "golem").Contains(id), "sai das equipes das Masmorras");
			Assert.False(Teams.Toggle(player, Teams.Campaign, id), "no Baú não entra em equipe");

			Assert.True(Roster.Retrieve(player, id), "volta do Baú");
			Assert.False(player.Monster(id)!.Stored, "na coleção");

			for (var i = player.Collection.Count(); i < player.CollectionCapacity; i++)
				Roster.Add(player, TestData.Summon("wolf_light"));
			Roster.Store(player, id);
			Roster.Add(player, TestData.Summon("wolf_light"));
			Assert.False(Roster.Retrieve(player, id), "coleção cheia: fica no Baú");
		}

		[Test]
		private static void FusingACopyRaisesARandomSkill()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith("wizard_fire", "wizard_fire", "wizard_water", "druid_fire");
			var (target, copy, other, stranger) = (player.Monsters[0], player.Monsters[1], player.Monsters[2], player.Monsters[3]);
			var rune = RuneInventory.Create(new Random(1), player, 3);
			RuneInventory.Equip(player, rune, copy.Id);

			Assert.True(Fusion.CanFuse(player, database, target.Id, other.Id), "outro elemento da mesma família funde");
			Assert.False(Fusion.CanFuse(player, database, target.Id, stranger.Id), "outra família não funde");
			var index = Fusion.Fuse(new Random(1), player, database, target.Id, copy.Id);
			Assert.True(index >= 0, "funde a cópia");
			Assert.Equal(2, target.SkillLevel(index), "a habilidade sorteada sobe para o nível 2");
			Assert.Equal(null, player.Monster(copy.Id), "a cópia some");
			Assert.Equal(null, rune.EquippedOn, "as runas da cópia voltam ao inventário");
			Assert.False(Teams.Of(player, Teams.Campaign).Contains(copy.Id), "e ela sai da equipe");

			target.SkillLevels = database.Summon("wizard_fire").Skills.Select(s => s.MaxLevel).ToList();
			var more = Roster.Add(player, TestData.Summon("wizard_fire"));
			Assert.Equal(0, Fusion.SkillUpsLeft(database, target), "tudo no máximo");
			Assert.Equal(-1, Fusion.Fuse(new Random(1), player, database, target.Id, more.Id), "com tudo no máximo não funde mais");
		}

		[Test]
		private static void LockedMonsterIsNeitherReleasedNorUsedAsMaterial()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith("wizard_fire", "wizard_fire");
			var (target, copy) = (player.Monsters[0], player.Monsters[1]);
			copy.Locked = true;

			Assert.False(Fusion.CanFuse(player, database, target.Id, copy.Id), "bloqueado não vira material");
			Assert.Equal(-1, Fusion.Fuse(new Random(1), player, database, target.Id, copy.Id), "e a fusão não acontece");
			Assert.Equal(0, Fusion.Release(player, database, copy.Id), "bloqueado não se solta");
			Assert.Equal(0, Fusion.ReleaseMany(player, database, new[] { copy.Id }), "nem em lote");
			Assert.Equal(2, player.Monsters.Count, "os dois ficam");

			target.Locked = true;
			copy.Locked = false;
			Assert.True(Fusion.Fuse(new Random(1), player, database, target.Id, copy.Id) >= 0, "bloqueado ainda recebe a fusão");

			var saved = PlayerSave.FromJson(PlayerSave.ToJson(player))!;
			Assert.True(saved.Monster(target.Id)!.Locked, "o bloqueio vai no save");
		}

		/// <summary>Um save de quando os mapas levavam cinco volta com quatro: a Líder e os três seguintes; a Masmorra segue com cinco.</summary>
		[Test]
		private static void SaveOfFiveOnTheMapIsTrimmedToFour()
		{
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var ids = player.Monsters.Select(m => m.Id).ToList();
			player.Teams[Teams.Campaign] = ids.ToList();
			player.Teams[Teams.Exploration] = ids.ToList();
			player.Teams["golem"] = ids.ToList();

			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player))!;
			Assert.True(Teams.Of(loaded, Teams.Campaign).SequenceEqual(ids.Take(4)), "a Campanha fica com quatro, a Líder primeiro");
			Assert.Equal(4, Teams.Of(loaded, Teams.Exploration).Count, "a Exploração também");
			Assert.Equal(5, Teams.Of(loaded, "golem").Count, "a Masmorra segue com cinco");
		}

	}
}
