using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>Mana, nível da conta, Ouro e Loja.</summary>
	internal static class EconomyTests
	{
		[Test]
		private static void CampaignChargesManaOnlyOnVictory()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("wizard_fire");
			player.AccountLevel = Account.MaxLevel;
			var stage = database.Stage(1);
			var mana = player.Mana;

			Assert.Equal(EntryProblem.None, Campaign.Check(player, stage), "a fase 1 abre");
			Assert.Equal(mana, player.Mana, "começar não cobra: a derrota não custa nada");
			Assert.Equal(stage.Mana, Campaign.ApplyVictory(new Random(1), player, stage, database, DateTime.UnixEpoch).Mana, "a vitória cobra a Mana da fase");
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
