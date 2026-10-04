using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>Mana, nível da conta, Ouro e Loja.</summary>
	internal static class EconomyTests
	{
		[Test]
		private static void AccountLevelRaisesMaxManaUpTo120()
		{
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), TestData.Database);
			Assert.Equal(60, Mana.Max(player), "nível 1");

			player.AccountLevel = 2;
			Assert.Equal(61, Mana.Max(player), "+1 por nível");
			player.AccountLevel = Account.MaxLevel - 1;
			Assert.Equal(118, Mana.Max(player), "118 no 59");
			player.AccountLevel = Account.MaxLevel;
			Assert.Equal(120, Mana.Max(player), "120 no nível 60, o máximo");
		}

		[Test]
		private static void AccountPortraitIsAMonsterTheAccountHas()
		{
			var player = TestData.PlayerWith("imp_fire", "imp_fire", "knight_fire");
			Assert.Equal(2, Account.Avatars(player).Count, "uma vez cada variante");
			Assert.False(Account.SetAvatar(player, "phoenix_fire", false), "quem a conta não tem não vira retrato");
			Assert.False(Account.SetAvatar(player, "imp_fire", true), "nem o desperto sem uma cópia desperta");

			player.Monsters[1].Awakened = true;
			Assert.True(Account.SetAvatar(player, "imp_fire", true), "com uma cópia desperta, o desperto vale");
			Assert.Equal("imp_fire", player.Avatar, "o retrato fica na conta");
			Assert.True(player.AvatarAwakened, "na forma desperta");
			Assert.True(Account.SetAvatar(player, "imp_fire", false), "e a forma comum continua valendo");

			var saved = PlayerSave.FromJson(PlayerSave.ToJson(player))!;
			Assert.Equal("imp_fire", saved.Avatar, "o retrato vai no save");
		}

		[Test]
		private static void AccountLevelUpGivesGoldAndFillsMana()
		{
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), TestData.Database);
			player.Mana = 5;
			var gold = player.Gold;

			Assert.Equal(1, Account.GiveExperience(player, Account.ExperienceToNext(1)), "um nível exato");
			Assert.Equal(2, player.AccountLevel, "nível 2");
			Assert.Equal(gold + Account.LevelUpGold, player.Gold, "Ouro do nível");
			Assert.Equal(Mana.Max(player), player.Mana, "a Mana enche");

			Account.GiveExperience(player, 10_000_000);
			Assert.Equal(Account.MaxLevel, player.AccountLevel, "para no nível 60");
			Assert.Equal(0, player.AccountExperience, "sem experiência sobrando no máximo");
			Assert.Equal(0, Account.GiveExperience(player, 1000), "no máximo não sobe mais");
		}

		[Test]
		private static void VictoryExperienceAlsoGoesToTheAccount()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			var stage = database.Stage(1);

			var reward = Campaign.ApplyVictory(new Random(1), player, stage, database);
			var total = player.AccountExperience + Enumerable.Range(1, player.AccountLevel - 1).Sum(Account.ExperienceToNext);
			Assert.Equal(stage.Experience, total, "a experiência da fase vai para a conta");
			Assert.Equal(player.AccountLevel - 1, reward.AccountLevels, "e a recompensa conta os níveis");
		}

		[Test]
		private static void CampaignChargesManaOnlyOnVictory()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("phoenix_fire");
			player.AccountLevel = Account.MaxLevel;
			var stage = database.Stage(1);
			var mana = player.Mana;

			Assert.Equal(EntryProblem.None, Campaign.Check(player, stage), "a fase 1 abre");
			Assert.Equal(mana, player.Mana, "começar não cobra: a derrota não custa nada");
			Assert.Equal(stage.Mana, Campaign.ApplyVictory(new Random(1), player, stage, database).Mana, "a vitória cobra a Mana da fase");
			Assert.Equal(mana - stage.Mana, player.Mana, "Mana na conta");
			Assert.Equal(EntryProblem.Locked, Campaign.Check(player, database.Stage(3)), "a fase 3 espera a 2");

			player.Mana = stage.Mana - 1;
			Assert.Equal(EntryProblem.NoMana, Campaign.Check(player, stage), "sem a Mana da vitória não começa");
		}

		[Test]
		private static void ShopTradesGoldForManaAndScrolls()
		{
			var database = TestData.LoadReal();
			var player = new PlayerState { Gold = 100, Mana = 60 };
			var mana = database.Shop.First(o => o.Item == ShopItem.Mana);
			var scrolls = database.Shop.OrderByDescending(o => o.Price).First(o => o.Item == ShopItem.Scrolls);

			Assert.True(Shop.Buy(player, mana), "compra Mana");
			Assert.Equal(100 - mana.Price, player.Gold, "paga em Ouro");
			Assert.Equal(60 + mana.Amount, player.Mana, "a Mana comprada passa do máximo");

			var gold = player.Gold;
			Assert.False(Shop.Buy(player, scrolls), $"{scrolls.Price} de Ouro é demais");
			Assert.Equal(gold, player.Gold, "nada muda");
			Assert.Equal(0, player.Scrolls, "nem Pergaminho");

			player.Gold = scrolls.Price;
			Assert.True(Shop.Buy(player, scrolls), "com Ouro, compra");
			Assert.Equal(scrolls.Amount, player.Scrolls, "Pergaminhos na conta");
		}

		/// <summary>
		/// A Expansão de Coleção dá vagas à coleção até o máximo da conta (Account.MaxCollectionCapacity); lá
		/// a oferta esgota e não cobra mais. Save de antes do campo existir começa com as vagas de sempre.
		/// </summary>
		[Test]
		private static void CollectionExpansionGrowsTheCollectionUpToTheMaximum()
		{
			var database = TestData.LoadReal();
			var offer = database.Shop.Single(o => o.Item == ShopItem.CollectionExpander);
			Assert.Equal(100, offer.Price, "custa 100 de Ouro");
			var player = new PlayerState { Version = PlayerState.CurrentVersion, Gold = 10_000 };
			Assert.Equal(PlayerState.StartingCollectionCapacity, player.CollectionCapacity, "conta nova: as vagas de sempre");

			Assert.True(Shop.Buy(player, offer), "compra");
			Assert.Equal(PlayerState.StartingCollectionCapacity + offer.Amount, player.CollectionCapacity, "vagas a mais");
			Assert.Equal(10_000 - offer.Price, player.Gold, "paga em Ouro");

			player.CollectionCapacity = Account.MaxCollectionCapacity - 1;
			Assert.True(Shop.Buy(player, offer), "a última compra");
			Assert.Equal(Account.MaxCollectionCapacity, player.CollectionCapacity, "para no máximo da conta");
			Assert.True(Shop.IsSoldOut(player, offer), "no máximo, esgota");
			var gold = player.Gold;
			Assert.False(Shop.Buy(player, offer), "esgotada, não compra");
			Assert.Equal(gold, player.Gold, "nem cobra");
			Assert.False(Shop.IsSoldOut(player, database.Shop.First(o => o.Item == ShopItem.Mana)), "as outras ofertas não esgotam");

			var json = PlayerSave.ToJson(player);
			Assert.Equal(Account.MaxCollectionCapacity, PlayerSave.FromJson(json)!.CollectionCapacity, "o save guarda as vagas");
			var old = System.Text.RegularExpressions.Regex.Replace(json, @"\s*""CollectionCapacity"":\s*\d+,", "");
			Assert.Equal(PlayerState.StartingCollectionCapacity, PlayerSave.FromJson(old)!.CollectionCapacity, "save sem o campo: as vagas de sempre");
		}
	}
}
