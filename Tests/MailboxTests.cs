using System;
using System.Collections.Generic;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>O correio: coletar soma as recompensas e anota a carta, uma vez só.</summary>
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
		private static void ClaimGivesEveryRewardAndRemembersTheLetter()
		{
			var player = new PlayerState { Gold = 10, Mana = 200 };
			var letter = Letter("a", (MailItem.Gold, 100), (MailItem.Mana, 50), (MailItem.Scrolls, 3), (MailItem.Essence, 1000), (MailItem.Fragments, 7), (MailItem.ReappraisalGems, 2));

			Assert.True(Mailbox.Claim(player, letter), "coleta");
			Assert.Equal(110, player.Gold, "Ouro");
			Assert.Equal(250, player.Mana, "Mana passa do máximo, como a da Loja");
			Assert.Equal(3, player.Scrolls, "Pergaminhos");
			Assert.Equal(1000, player.Essence, "Essência");
			Assert.Equal(7, player.Fragments, "Fragmentos");
			Assert.Equal(2, player.ReappraisalGems, "Gemas de Reavaliação");
			Assert.True(player.ClaimedMail.Contains("a"), "a carta fica anotada no save");
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
		private static void UnclaimedLeavesOutWhatThisSaveAlreadyCollected()
		{
			var player = new PlayerState();
			var first = Letter("a", (MailItem.Gold, 1));
			var second = Letter("b", (MailItem.Gold, 1));
			Mailbox.Claim(player, first);

			var left = Mailbox.Unclaimed(player, new[] { first, second });
			Assert.Equal(1, left.Count, "só a que falta");
			Assert.Equal("b", left[0].Id, "a segunda");
		}

		[Test]
		private static void ClaimedLettersSurviveTheSave()
		{
			var player = new PlayerState { Version = PlayerState.CurrentVersion };
			Mailbox.Claim(player, Letter("a", (MailItem.Gold, 1)));

			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player))!;
			Assert.True(loaded.ClaimedMail.Contains("a"), "a carta coletada vai no save");
		}
	}
}
