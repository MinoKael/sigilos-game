using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>As ações do servidor (melhora de runa, pedras, reavaliação, invocação) e a conferência do save enviado.</summary>
	internal static class ServerActionTests
	{
		private static PlayerState Player()
		{
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), TestData.Database);
			player.Essence = 1_000_000;
			player.Scrolls = 100;
			player.ReappraisalGems = 2;
			return player;
		}

		private static PlayerState Copy(PlayerState player) => PlayerSave.FromJson(PlayerSave.ToJson(player))!;

		private static Rune Fresh(PlayerState player, int slot = 1) => RuneInventory.Create(new Random(3), player, 6, null, RuneRarity.Rare, slot);

		[Test]
		private static void UpgradeChargesEssenceAndTheDeltaReproducesItOnAnotherCopy()
		{
			var server = Player();
			var rune = Fresh(server);
			var game = Copy(server);
			var essence = server.Essence;

			var delta = ServerActions.Apply(new Random(5), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = rune.Id, Target = 12 })!;
			Assert.True(delta != null, "a melhora vale");
			Assert.Equal(12, rune.Level, "chegou ao +12");
			Assert.True(server.Essence < essence, "gastou Essência");

			StatePatch.Apply(game, delta.Patch!);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(game), "a cópia do jogo ficou igual à do servidor");
		}

		[Test]
		private static void PatchCarriesRemovalsAdditionsAndFieldsToAnotherCopy()
		{
			var server = Player();
			for (var i = 0; i < 4; i++)
				Fresh(server, 1 + i % 3);
			foreach (var summon in TestData.Database.Summons.Take(3))
				Roster.Add(server, summon);

			var game = Copy(server);
			var kept = game.Runes[0];
			var after = Copy(server);
			after.Runes.RemoveAt(1);
			after.Monsters.RemoveAt(0);
			after.Runes[0].Locked = !after.Runes[0].Locked;
			Fresh(after, 2);
			after.Veil.Aster += 37;
			after.Essence += 1234;

			StatePatch.Apply(game, StatePatch.Diff(server, after));
			Assert.Equal(PlayerSave.ToJson(after), PlayerSave.ToJson(game), "a cópia ficou igual à do servidor");
			Assert.True(ReferenceEquals(kept, game.Runes[0]) && kept.Locked == after.Runes[0].Locked, "a runa que ficou foi atualizada no lugar");
		}

		[Test]
		private static void UpgradeWithoutEssenceOrWithAMissingRuneDoesNothing()
		{
			var player = Player();
			var rune = Fresh(player);
			player.Essence = 0;
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = rune.Id, Target = 12 }) == null, "sem Essência");
			Assert.Equal(0, rune.Level, "nada mudou");
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = 9999, Target = 3 }) == null, "runa que não existe");
		}

		[Test]
		private static void BulkUpgradeRaisesOnlyTheRunesBelowTheTargetAndThePatchReproducesIt()
		{
			var server = Player();
			var (first, second, above) = (Fresh(server, 1), Fresh(server, 2), Fresh(server, 3));
			RuneInventory.Upgrade(new Random(4), server, above, 9);
			var game = Copy(server);
			var essence = server.Essence;
			var cost = RuneRules.UpgradeCost(first, 6) + RuneRules.UpgradeCost(second, 6);

			var delta = ServerActions.Apply(new Random(5), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.UpgradeRunes, Ids = new() { first.Id, second.Id, above.Id }, Target = 6 })!;
			Assert.True(delta != null, "a melhora seletiva vale");
			Assert.Equal(2, delta.Count, "subiram as duas abaixo do alvo");
			Assert.True(first.Level == 6 && second.Level == 6, "as duas chegaram ao +6");
			Assert.Equal(9, above.Level, "a que já passava do alvo ficou");
			Assert.Equal(essence - cost, server.Essence, "gastou o custo das duas, nada mais");

			StatePatch.Apply(game, delta.Patch!);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(game), "a cópia do jogo ficou igual à do servidor");
		}

		[Test]
		private static void BulkUpgradeIsAllOrNothing()
		{
			var player = Player();
			var (first, second) = (Fresh(player, 1), Fresh(player, 2));
			var cost = RuneRules.UpgradeCost(first, 12) + RuneRules.UpgradeCost(second, 12);
			var request = new ActionRequest { Kind = ServerActionKind.UpgradeRunes, Ids = new() { first.Id, second.Id }, Target = 12 };

			player.Essence = cost - 1;
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, request) == null, "a Essência não paga as duas");
			Assert.True(first.Level == 0 && second.Level == 0 && player.Essence == cost - 1, "nada mudou");

			player.Essence = cost;
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, request) != null, "a Essência paga as duas");
			Assert.True(first.Level == 12 && second.Level == 12 && player.Essence == 0, "as duas subiram e a Essência acabou");
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.UpgradeRunes, Ids = new() { 9999 }, Target = 3 }) == null, "runa que não existe");
		}

		[Test]
		private static void BulkUpgradeToTheTopIsAFeatPerRune()
		{
			var player = Player();
			player.Essence = int.MaxValue / 2;
			var (first, second) = (Fresh(player, 1), Fresh(player, 2));
			var delta = ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.UpgradeRunes, Ids = new() { first.Id, second.Id }, Target = 99 })!;
			Assert.True(delta != null && first.Level == RuneRules.MaxLevel && second.Level == RuneRules.MaxLevel, "o alvo acima do teto para no +15");
			Assert.Equal(2, delta.Feats.Count, "cada runa que chegou ao +15 é um feito");
		}

		[Test]
		private static void SummonDeliversMonstersAndKeepsThePityOnTheDelta()
		{
			var server = Player();
			var game = Copy(server);
			var delta = ServerActions.Apply(new Random(9), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.Summon, Count = 10, Scroll = ScrollKind.Mystic })!;
			Assert.True(delta != null, "a invocação vale");
			Assert.Equal(10, delta.Monsters.Count, "dez monstros");
			Assert.Equal(10, server.TotalPulls, "dez invocações contadas");

			StatePatch.Apply(game, delta.Patch!);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(game), "a cópia do jogo ficou igual à do servidor");
			Assert.Equal(10, ServerActions.Results(TestData.Database, ActionDelta.FromJson(delta.ToJson())!).Count, "o resultado atravessa o texto");
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.Summon, Count = 3 }) == null, "só 1 ou 10");
		}

		[Test]
		private static void ReappraisalGoesBackToTheFreshRune()
		{
			var server = Player();
			var rune = Fresh(server);
			ServerActions.Apply(new Random(5), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.UpgradeRune, RuneId = rune.Id, Target = 12 });
			var delta = ServerActions.Apply(new Random(5), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.ReappraiseRune, RuneId = rune.Id });
			Assert.True(delta != null, "reavalia");
			Assert.Equal(0, rune.Level, "voltou a +0");
			Assert.Equal(1, server.ReappraisalGems, "gastou uma gema");
		}

		[Test]
		private static void RuneKitOpensSixRunesOfTheChosenSetOnTheServer()
		{
			var server = Player();
			var set = RuneSets.Active[0].Set;
			var request = new ActionRequest { Kind = ServerActionKind.OpenRuneKit, Text = set.ToString() };
			Assert.True(ServerActions.Apply(new Random(2), TestData.Database, server, request) == null, "sem kit não abre");

			server.PendingRuneKits = 1;
			var game = Copy(server);
			Assert.True(ServerActions.Apply(new Random(2), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.OpenRuneKit, Text = "nada" }) == null, "conjunto que não existe");
			var delta = ServerActions.Apply(new Random(2), TestData.Database, server, request)!;
			Assert.True(delta != null, "abre o kit");
			Assert.Equal(RuneRules.Slots, delta.Runes.Count, "um sigilo por espaço");
			Assert.True(delta.Runes.All(r => r.Set == set), "todos do conjunto escolhido");
			Assert.Equal(0, server.PendingRuneKits, "o kit foi gasto");

			StatePatch.Apply(game, ActionDelta.FromJson(delta.ToJson())!.Patch!);
			Assert.Equal(PlayerSave.ToJson(server), PlayerSave.ToJson(game), "a cópia do jogo ficou igual à do servidor");
		}

		[Test]
		private static void ShopPurchaseChargesAnOfferAndNoLongerSellsTheName()
		{
			var player = Player();
			player.Gold = 1_000_000;
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.BuyShop, Text = "rename" }) == null, "a troca de nome não é mais vendida");
			Assert.Equal(1_000_000, player.Gold, "nada foi cobrado");

			var offer = TestData.Database.Shop.First();
			Assert.True(ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.BuyShop, Text = offer.Id }) != null, "a oferta vale");
			Assert.Equal(1_000_000 - offer.Price, player.Gold, "cobrou o preço");
		}

		[Test]
		private static void RenameIsFreeOnceEverySevenDays()
		{
			var player = Player();
			var now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
			Assert.True(AccountRename.CanRename(player, now), "quem nunca trocou pode trocar");

			AccountRename.Mark(player, now);
			Assert.Equal<DateTime?>(now + AccountRename.Cooldown, AccountRename.NextAt(player, now), "a próxima abre em 7 dias");
			Assert.True(!AccountRename.CanRename(player, now + AccountRename.Cooldown - TimeSpan.FromSeconds(1)), "um segundo antes, ainda não");
			Assert.True(AccountRename.CanRename(player, now + AccountRename.Cooldown), "com 7 dias, abre");
			var saved = PlayerSave.FromJson(PlayerSave.ToJson(player))!.LastRename;
			Assert.Equal<DateTime?>(now, saved, "a data fica no save");
			Assert.Equal(DateTimeKind.Utc, saved!.Value.Kind, "a data volta em UTC");
		}

		[Test]
		private static void ActionsKeepTheirFeatsOnTheServer()
		{
			var server = Player();
			server.Scrolls = 1000;
			var delta = Enumerable.Range(0, 60)
				.Select(seed => ServerActions.Apply(new Random(seed), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.Summon, Count = 10, Scroll = ScrollKind.Mystic })!)
				.FirstOrDefault(d => d.Feats.Count > 0);
			Assert.True(delta != null, "alguma invocação rendeu um feito");
			Assert.True(ActionDelta.FromJson(delta!.ToJson())!.Feats.Count == 0, "o feito não viaja ao jogo");
		}
	}
}
