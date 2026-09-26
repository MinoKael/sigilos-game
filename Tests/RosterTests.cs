using System;
using System.Linq;
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
			var player = TestData.PlayerWith("diabrete_fogo", "diabrete_agua");
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

			for (var i = player.Collection.Count(); i < PlayerState.CollectionCapacity; i++)
				Roster.Add(player, "diabrete_luz");
			Roster.Store(player, id);
			Roster.Add(player, "diabrete_luz");
			Assert.False(Roster.Retrieve(player, id), "coleção cheia: fica no Baú");
		}

		[Test]
		private static void FusingTheSameVariantGivesAnEcho()
		{
			var player = TestData.PlayerWith("fenix_fogo", "fenix_fogo", "fenix_agua");
			var (target, copy, other) = (player.Monsters[0], player.Monsters[1], player.Monsters[2]);
			var rune = RuneInventory.Create(new Random(1), player, 3);
			RuneInventory.Equip(player, rune, copy.Id);

			Assert.False(Fusion.CanFuse(player, target.Id, other.Id), "variante diferente não funde");
			Assert.True(Fusion.Fuse(player, target.Id, copy.Id), "funde a cópia");
			Assert.Equal(1, target.Echoes, "+1 Eco");
			Assert.Equal(null, player.Monster(copy.Id), "a cópia some");
			Assert.Equal(null, rune.EquippedOn, "as runas da cópia voltam ao inventário");
			Assert.False(Teams.Of(player, Teams.Campaign).Contains(copy.Id), "e ela sai da equipe");

			target.Echoes = 5;
			var more = Roster.Add(player, "fenix_fogo");
			Assert.False(Fusion.Fuse(player, target.Id, more.Id), "com 5 Ecos não funde mais");
		}

		[Test]
		private static void ReleasingGivesFragmentsByRarity()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("fenix_fogo", "diabrete_fogo");
			Assert.Equal(20, Fusion.Release(player, database, player.Monsters[0].Id), "5★ vale 20");
			Assert.Equal(5, Fusion.Release(player, database, player.Monsters[0].Id), "3★ vale 5");
			Assert.Equal(25, player.Fragments, "Fragmentos na conta");
			Assert.Equal(0, player.Monsters.Count, "os dois liberados");
		}

		[Test]
		private static void SelectingManyFusesUpToFiveAndReleasesAll()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("fenix_fogo");
			var target = player.Monsters[0];
			var copies = Enumerable.Range(0, 7).Select(_ => Roster.Add(player, "fenix_fogo").Id).ToList();

			Assert.Equal(Core.Progression.Growth.MaxEchoes, Fusion.FuseMany(player, target.Id, copies), "funde até 5 Ecos");
			Assert.Equal(Core.Progression.Growth.MaxEchoes, target.Echoes, "5 Ecos");
			var left = copies.Where(id => player.Monster(id) != null).ToList();
			Assert.Equal(2, left.Count, "as que não couberam ficam");

			Assert.Equal(2 * Fusion.FragmentsFor(5), Fusion.ReleaseMany(player, database, left), "libera as que sobraram");
			Assert.Equal(1, player.Monsters.Count, "só o alvo fica");
		}

		[Test]
		private static void RunesOnChestMonstersDoNotFillTheInventory()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("fenix_fogo");
			player.HighestStage = database.Stages.Count;
			var random = new Random(3);
			for (var i = 0; i < RuneInventory.Capacity; i++)
				RuneInventory.Create(random, player, 1);

			var golem = database.Dungeon("golem");
			var forge = database.Dungeons.First(d => d.Kind == Core.Content.DungeonKind.Tools);
			Assert.Equal(EntryProblem.RunesFull, Campaign.Check(player, database.Stage(1)), "cheio: a Campanha espera");
			Assert.Equal(EntryProblem.RunesFull, Dungeons.Check(player, golem, 1), "e a Masmorra de runas também");
			Assert.Equal(EntryProblem.None, Dungeons.Check(player, forge, 1), "a Forja não solta runa");

			var holder = Roster.Add(player, "diabrete_fogo");
			Roster.Store(player, holder.Id);
			Assert.True(RuneInventory.Equip(player, player.Runes[0], holder.Id), "monstro do Baú recebe runa");
			Assert.Equal(RuneInventory.Capacity - 1, RuneInventory.Count(player), "e ela sai do inventário");
			Assert.Equal(EntryProblem.None, Campaign.Check(player, database.Stage(1)), "com vaga, a Campanha abre");

			RuneInventory.Create(random, player, 1);
			Assert.False(RuneInventory.Unequip(player, player.Runes[0]), "cheio: não tira runa de monstro");
		}

		[Test]
		private static void EachContentHasItsOwnTeam()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var extra = Roster.Add(player, "cavaleiro_fogo");

			Assert.False(Teams.Toggle(player, Teams.Campaign, extra.Id), $"a Campanha já tem {PlayerState.TeamSize}");
			Assert.True(Teams.Toggle(player, "golem", extra.Id), "a equipe do Golem é outra");
			Assert.True(Teams.MakeLeader(player, Teams.Campaign, player.Monsters[3].Id), "troca a Líder");
			Assert.Equal(player.Monsters[3].Id, Teams.Of(player, Teams.Campaign)[0], "a Líder vai para a frente");

			Assert.Equal(5, PlayerTeam.Build(player, database, Teams.Campaign).Members.Count, "cinco na Campanha");
			Assert.Equal("cavaleiro_fogo", PlayerTeam.Build(player, database, "golem").Members.Single().Summon.Id, "um no Golem");
		}
	}
}
