using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>O Núcleo de Infusão, os marcos que pagam o que é especial e a troca de Fragmentos.</summary>
	internal static class MilestoneTests
	{
		private static PlayerState NewPlayer() => NewGame.Create(DateTime.UnixEpoch, new Random(1), TestData.Database);

		[Test]
		private static void InfusionCoreOnlyFusesButRaisesAnyFamily()
		{
			var database = TestData.Database;
			var player = TestData.PlayerWith("imp_fire");
			var imp = player.Monsters.Single();
			Milestones.Grant(player, new Prize(InfusionCores: 2));
			var cores = player.Monsters.Where(m => m.IsInfusionCore).ToList();
			Assert.Equal(2, cores.Count, "dois Núcleos na conta");
			Assert.Equal(3, player.Collection.Count(), "ocupam vaga na coleção");
			Assert.False(database.Summons.Any(s => InfusionCore.Is(s.Id)), "não é invocação: não sai em pergaminho nem no Grimório");

			var core = cores[0];
			Assert.False(Teams.Toggle(player, Teams.Campaign, core.Id), "não entra em equipe");
			Assert.False(RuneInventory.Equip(player, RuneInventory.Create(new Random(1), player, 3), core.Id), "não usa runa");
			player.Essence = 1_000_000;
			Assert.Equal(0, Leveling.Infuse(player, core, 1000), "não sobe de nível");
			Assert.False(Evolution.IsReady(core), "não evolui");
			Assert.False(Awakening.CanAwaken(player, core, database.Summon(core.SummonId)), "não desperta");
			Assert.Equal(0, Fusion.Release(player, database, core.Id), "não se solta");
			Assert.False(Fusion.CanFuse(player, database, core.Id, imp.Id), "não recebe fusão");
			Assert.True(Account.Avatars(player).All(a => !InfusionCore.Is(a.Id)), "nem vira retrato da conta");

			Assert.True(Fusion.CanFuse(player, database, imp.Id, core.Id), "funde em qualquer família");
			Assert.True(Fusion.Fuse(new Random(1), player, database, imp.Id, core.Id) >= 0, "funde");
			Assert.Equal(1, imp.SkillLevels.Sum() - imp.SkillLevels.Count, "uma habilidade subiu um nível");
			Assert.True(player.Monster(core.Id) == null, "e o Núcleo some");

			player.CollectionCapacity = player.Collection.Count();
			Milestones.Grant(player, new Prize(InfusionCores: 1));
			Assert.True(player.Monsters.Last().Stored, "coleção cheia: o Núcleo vai para o Baú");
		}

		[Test]
		private static void RegionEndsAndDungeonFloorsPayTheirMilestonesOnce()
		{
			var database = TestData.Database;
			var player = NewPlayer();
			player.HighestStage = 19;
			player.AccountLevel = Account.MaxLevel;
			var stage = database.Stage(20);
			var first = Campaign.ApplyVictory(new Random(1), player, stage, database);
			Assert.Equal(new Prize(1, 0, 2), first.Prize, "o fim da região 1 dá um Lendário e dois Núcleos");
			Assert.Equal(1, player.LegendaryScrolls, "o Lendário na conta");
			Assert.Equal(2, player.Monsters.Count(m => m.IsInfusionCore), "os Núcleos na coleção");
			Assert.Equal(Prize.None, Campaign.ApplyVictory(new Random(1), player, stage, database).Prize, "repetir não dá de novo");

			var golem = database.Dungeon("golem");
			player.HighestStage = golem.UnlockStage;
			player.DungeonFloors[golem.Id] = 3;
			var fourth = Dungeons.ApplyVictory(new Random(1), player, golem, 4);
			Assert.Equal(1, fourth.Prize!.LegendaryScrolls, "o andar 4 dá um Lendário");
			Assert.True(fourth.Prize.InfusionCores >= golem.Floor(4).FirstClearCores, "e os Núcleos dele");
			var fifth = Dungeons.ApplyVictory(new Random(1), player, golem, 5);
			Assert.Equal(1, fifth.Prize!.LightDarkScrolls, "o andar 5 dá um de Luz e Trevas");
			Assert.Equal(1, player.LightDarkScrolls, "na conta");

			var cores = 0;
			for (var seed = 0; seed < 2000; seed++)
				cores += Dungeons.ApplyVictory(new Random(seed), player, golem, 5).Prize!.InfusionCores;
			Assert.True(Math.Abs(cores / 2000.0 * 100 - golem.Floor(5).CoreChance) < 1.2, $"repetir o andar 5 dá Núcleo pela chance dele ({cores} em 2000)");
		}

		[Test]
		private static void AccountLevelsPayLightDarkScrollsAndCores()
		{
			var player = NewPlayer();
			player.AccountLevel = 19;
			player.AccountExperience = 0;
			var cores = player.Monsters.Count(m => m.IsInfusionCore);
			Account.GiveExperience(player, Account.ExperienceToNext(19));
			Assert.Equal(20, player.AccountLevel, "sobe para o 20");
			Assert.Equal(1, player.LightDarkScrolls, "o nível 20 dá um Pergaminho de Luz e Trevas");
			Assert.Equal(cores + 1, player.Monsters.Count(m => m.IsInfusionCore), "e um Núcleo (a cada 5 níveis)");
			Assert.Equal(new Prize(0, 5, 20), Milestones.ForAccountLevels(0, Account.MaxLevel), "do 1 ao 100: 5 de Luz e Trevas e 20 Núcleos");
		}

		[Test]
		private static void SpecialScrollsSummonOnlyWhatTheyPromise()
		{
			var database = TestData.Database;
			var player = NewPlayer();
			player.TotalPulls = 2;
			player.LegendaryScrolls = 10;
			player.LightDarkScrolls = 10;
			var legendary = SummonRitual.Perform(new Random(3), database, player, 10, ScrollKind.Legendary);
			Assert.Equal(10, legendary.Count, "dez Lendárias");
			Assert.True(legendary.All(r => r.Summon.Rarity >= 4 && SummonRates.Allows(ScrollKind.Legendary, r.Summon.Element)), "só 4★ e 5★ de Fogo, Água e Vento");
			var lightDark = SummonRitual.Perform(new Random(3), database, player, 10, ScrollKind.LightDark);
			Assert.True(lightDark.All(r => r.Summon.Element is Element.Light or Element.Dark), "só Luz e Trevas");
			Assert.Equal(0, player.LegendaryScrolls + player.LightDarkScrolls, "cada uma gasta o pergaminho dela");
			Assert.Equal(0, SummonRitual.Perform(new Random(3), database, player, 1, ScrollKind.Legendary).Count, "sem pergaminho, nada");
		}

		[Test]
		private static void FragmentsBuyAChosenFourStar()
		{
			var database = TestData.Database;
			var player = NewPlayer();
			var options = FragmentExchange.Options(database);
			Assert.True(options.Count > 0 && options.All(s => s.Rarity == 4 && s.Element is not (Element.Light or Element.Dark)), "as 4★ de Fogo, Água e Vento");
			var choice = options.First(s => s.Id == "gargoyle_water");
			player.Fragments = FragmentExchange.Cost - 1;
			Assert.True(FragmentExchange.Exchange(player, database, choice.Id) == null, "sem Fragmentos, não troca");
			player.Fragments = FragmentExchange.Cost;
			Assert.True(FragmentExchange.Exchange(player, database, "crow_light") == null, "Luz e Trevas não se trocam");
			Assert.True(FragmentExchange.Exchange(player, database, "dragon_fire") == null, "nem 5★");
			var result = FragmentExchange.Exchange(player, database, choice.Id);
			Assert.Equal(choice.Id, result!.Summon.Id, "a escolhida");
			Assert.Equal(0, player.Fragments, "paga os Fragmentos");
		}
	}
}
