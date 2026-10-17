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
	}
}
