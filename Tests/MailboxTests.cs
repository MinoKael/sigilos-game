using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>O correio: coletar soma as recompensas e anota a carta, uma vez só; as cartas do jogo vencem e viajam no save.</summary>
	internal static class MailboxTests
	{
		private static Mail Letter(string id, params (MailItem Item, int Amount)[] rewards)
		{
			var map = new Dictionary<MailItem, int>();
			foreach (var (item, amount) in rewards)
				map[item] = amount;
			return new Mail(id, "Presente", "Obrigado por jogar.", map, DateTimeOffset.UnixEpoch, null);
		}

		[Test]
		private static void TheSameLetterIsNeverClaimedTwice()
		{
			var player = new PlayerState();
			var letter = Letter("a", (MailItem.Gold, 100));

			Mailbox.Claim(player, letter);
			Assert.False(Mailbox.Claim(player, letter), "a segunda vez não vale");
			Assert.Equal(100, player.Gold, "nem soma de novo");
		}

		[Test]
		private static void ClaimedLettersSurviveTheSave()
		{
			var player = new PlayerState { Version = PlayerState.CurrentVersion };
			Mailbox.Claim(player, Letter("a", (MailItem.Gold, 1)));

			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player))!;
			Assert.True(loaded.ClaimedMail.Contains("a"), "a carta coletada vai no save");
		}

		private static readonly DateTime Sent = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

		private static ActionDelta? Claim(PlayerState player, Letter letter, DateTime now) =>
			ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.ClaimMail, Text = Letters.MailId(letter) }, new ActionContext(now, _ => null));

		[Test]
		private static void GameLetterPaysOnceAndTheCopyFollows()
		{
			var server = new PlayerState { Version = PlayerState.CurrentVersion };
			var letter = Letters.Send(server, Letters.ArenaSeason, Sent, new Dictionary<string, int> { ["gold"] = 500, ["legendaryScrolls"] = 1 }, "grade")!;
			var game = PlayerSave.FromJson(PlayerSave.ToJson(server))!;
			Assert.Equal(1, game.Inbox.Count, "a carta vai no save");

			var delta = Claim(server, letter, Sent.AddDays(1));
			Assert.True(delta != null, "coleta");
			Assert.Equal(500, server.Gold, "o Ouro entra");
			Assert.Equal(1, server.LegendaryScrolls, "o pergaminho entra");
			Assert.Equal(0, server.Inbox.Count, "a carta sai do correio");
			Assert.True(Claim(server, letter, Sent.AddDays(1)) == null, "a segunda vez não vale");

			StatePatch.Apply(game, delta!.Patch!);
			Assert.Equal(500, game.Gold, "a cópia recebe o Ouro");
			Assert.Equal(0, game.Inbox.Count, "e perde a carta");
		}

		[Test]
		private static void GameLetterExpiresAfterSevenDays()
		{
			var player = new PlayerState();
			var late = Letters.Send(player, Letters.ArenaSeason, Sent, new Dictionary<string, int> { ["gold"] = 500 }, "grade")!;
			var fresh = Letters.Send(player, Letters.ArenaSeason, Sent.AddDays(3), new Dictionary<string, int> { ["gold"] = 1 }, "grade")!;
			Assert.True(Letters.Send(player, Letters.ArenaSeason, Sent, new Dictionary<string, int> { ["gold"] = 0 }) == null, "sem prêmio, sem carta");
			Assert.True(fresh.Id != late.Id, "cada carta tem o seu número");

			var expired = Sent.AddDays(Letters.Days);
			Assert.True(Claim(player, late, expired) == null, "vencida não paga");
			Assert.Equal(0, player.Gold, "nada entra");

			Account.Open(player, TestData.Database, expired);
			Assert.True(player.Inbox.Single() == fresh, "abrir a conta tira só a vencida");
		}

		[Test]
		private static void AccountMilestoneArrivesByMail()
		{
			var player = new PlayerState { AccountLevel = 19 };
			Account.GiveExperience(player, Account.ExperienceToNext(19), Sent);

			Assert.Equal(20, player.AccountLevel, "subiu ao 20");
			Assert.Equal(0, player.LightDarkScrolls, "o marco não cai direto");
			var letter = player.Inbox.Single(l => l.Kind == Letters.AccountLevel);
			Assert.Equal(1, letter.Rewards["lightDarkScrolls"], "vai por carta");
			Assert.Equal("20", letter.Args.Single(), "com o nível");

			Claim(player, letter, Sent);
			Assert.Equal(1, player.LightDarkScrolls, "coletar entrega");
			Assert.Equal(1, player.Monsters.Count(m => m.SummonId == InfusionCore.Id), "e o Núcleo de Infusão do 20");
		}

		[Test]
		private static void RuneKitsOfAMilestoneSkipTheMail()
		{
			var player = new PlayerState();
			var letter = Letters.Send(player, Letters.AccountLevel, Sent, new Prize(LegendaryScrolls: 1, RuneKits: 2), "50");

			Assert.Equal(2, player.PendingRuneKits, "os kits entram direto");
			Assert.False(letter!.Rewards.ContainsKey("runeKits"), "e não vão na carta");
			Assert.Equal(1, letter.Rewards["legendaryScrolls"], "o resto vai por carta");
		}
	}
}
