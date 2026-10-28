using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>O check-in diário: o dia avança uma vez por dia UTC, faltar não tira nada, o ciclo fecha em 28, e o prêmio chega por carta.</summary>
	internal static class CheckInTests
	{
		private static readonly DateTime Monday = new(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc);

		private static GameDatabase Database => TestData.Database;

		private static ActionDelta? Check(PlayerState player, DateTime now) =>
			ServerActions.Apply(new Random(1), Database, player, new ActionRequest { Kind = ServerActionKind.CheckIn }, new ActionContext(now, _ => null));

		[Test]
		private static void TheFirstCheckInGivesDayOneByLetter()
		{
			var player = new PlayerState();
			Assert.True(DailyCheckIn.Due(player, Database, Monday), "a conta nova pode fazer o check-in");
			Assert.True(DailyCheckIn.Open(player, Database, Monday) is { Day: 1 }, "o primeiro dia é o 1");

			var letter = player.Inbox.Single();
			Assert.Equal(Letters.CheckIn, letter.Kind, "vai por carta de check-in");
			Assert.Equal("1", letter.Args.Single(), "com o dia");
			Assert.Equal(Database.CheckIn[0].Rewards.Count, letter.Rewards.Count, "com o prêmio do dia 1 dos dados");
			Assert.Equal(Monday.AddDays(Letters.Days), letter.ExpiresAt, "que vale sete dias");
			Assert.Equal(0, player.Gold, "e não cai direto na conta");
		}

		[Test]
		private static void OpeningTwiceInTheSameUtcDayCountsOnce()
		{
			var player = new PlayerState();
			DailyCheckIn.Open(player, Database, Monday);

			Assert.False(DailyCheckIn.Due(player, Database, Monday.AddHours(10)), "no mesmo dia, não vale de novo");
			Assert.True(DailyCheckIn.Open(player, Database, Monday.AddHours(10)) == null, "e não muda nada");
			Assert.Equal(1, player.Inbox.Count, "uma carta só");
			Assert.Equal(1, player.CheckIn.Day, "no dia 1 ainda");
			Assert.True(Check(player, Monday.AddHours(10)) == null, "a ação do servidor recusa");
		}

		[Test]
		private static void TheNextUtcDayAdvancesAndMissedDaysLoseNothing()
		{
			var player = new PlayerState();
			DailyCheckIn.Open(player, Database, Monday.AddHours(15));
			var midnight = Monday.Date.AddDays(1);

			Assert.True(DailyCheckIn.Due(player, Database, midnight), "à meia-noite UTC o dia virou");
			Assert.True(DailyCheckIn.Open(player, Database, midnight) is { Day: 2 }, "o dia seguinte é o 2");
			Assert.True(DailyCheckIn.Open(player, Database, midnight.AddDays(3)) is { Day: 3 }, "faltar três dias não perde nem recomeça: o 3");
			Assert.Equal(3, player.Inbox.Count, "uma carta por entrada");
		}

		[Test]
		private static void TheCycleRestartsAfterTwentyEight()
		{
			var player = new PlayerState();
			for (var i = 0; i < CheckInDay.Cycle; i++)
				Assert.True(DailyCheckIn.Open(player, Database, Monday.AddDays(i)) is { } day && day.Day == i + 1, $"o dia {i + 1} na ordem");

			Assert.Equal(28, player.CheckIn.Day, "terminou no 28");
			Assert.True(DailyCheckIn.Open(player, Database, Monday.AddDays(28)) is { Day: 1 }, "depois do 28 volta o 1");
		}

		[Test]
		private static void TheBigDaysAndTheirPrizesComeFromTheData()
		{
			var player = new PlayerState();
			for (var i = 0; i < 7; i++)
				DailyCheckIn.Open(player, Database, Monday.AddDays(i));

			var seventh = player.Inbox.Single(l => l.Args.Single() == "7");
			var expected = DailyCheckIn.Of(Database, 7)!;
			Assert.True(expected.Big, "o dia 7 é grande nos dados");
			Assert.True(seventh.Rewards.OrderBy(r => r.Key).SequenceEqual(expected.Rewards.OrderBy(r => r.Key)), "e a carta traz o que os dados dizem");
		}

		[Test]
		private static void TheNextBigPrizeWrapsToTheNextCycle()
		{
			var player = new PlayerState();
			Assert.Equal(7, DailyCheckIn.NextBig(player, Database)!.Value.Day.Day, "no começo, o 7");
			Assert.Equal(7, DailyCheckIn.NextBig(player, Database)!.Value.Away, "daqui a sete check-ins");

			player.CheckIn.Day = 7;
			Assert.Equal(14, DailyCheckIn.NextBig(player, Database)!.Value.Day.Day, "no dia grande, o próximo, não ele");

			player.CheckIn.Day = 28;
			var next = DailyCheckIn.NextBig(player, Database)!.Value;
			Assert.Equal(7, next.Day.Day, "no 28, o 7 do ciclo seguinte");
			Assert.Equal(7, next.Away, "daqui a sete");
		}

		[Test]
		private static void TheLetterPaysWhatTheDayPromises()
		{
			var player = new PlayerState();
			DailyCheckIn.Open(player, Database, Monday);
			DailyCheckIn.Open(player, Database, Monday.AddDays(1));

			var mana = player.Inbox.Single(l => l.Args.Single() == "2");
			var delta = ServerActions.Apply(new Random(1), Database, player, new ActionRequest { Kind = ServerActionKind.ClaimMail, Text = Letters.MailId(mana) }, new ActionContext(Monday.AddDays(2), _ => null));
			Assert.True(delta != null, "a carta do check-in se coleta");
			Assert.Equal(Database.CheckIn[1].Rewards["mana"], player.Mana, "a Mana do dia 2 entra");
		}

		[Test]
		private static void TheDailyCheckInClearsExpiredLetters()
		{
			var player = new PlayerState();
			var old = Letters.Send(player, Letters.ArenaSeason, Monday.AddDays(-10), new Dictionary<string, int> { ["gold"] = 1 }, "grade")!;
			var recent = Letters.Send(player, Letters.ArenaSeason, Monday.AddDays(-2), new Dictionary<string, int> { ["gold"] = 1 }, "grade")!;

			Assert.True(Check(player, Monday) != null, "o check-in conta");
			Assert.False(player.Inbox.Contains(old), "a carta vencida sai do save");
			Assert.True(player.Inbox.Contains(recent), "a que ainda vale fica");
			Assert.Equal(1, player.Inbox.Count(l => l.Kind == Letters.CheckIn), "e a do dia chega");
		}

		[Test]
		private static void TheCheckInTravelsInTheSave()
		{
			var player = new PlayerState { Version = PlayerState.CurrentVersion };
			DailyCheckIn.Open(player, Database, Monday);
			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player))!;

			Assert.Equal(1, loaded.CheckIn.Day, "o dia vai no save");
			Assert.False(DailyCheckIn.Due(loaded, Database, Monday.AddHours(3)), "e o de hoje já contou");
		}

		[Test]
		private static void TheServerActionSendsTheLetterThroughThePatch()
		{
			var server = new PlayerState { Version = PlayerState.CurrentVersion };
			var game = PlayerSave.FromJson(PlayerSave.ToJson(server))!;

			var delta = Check(server, Monday);
			Assert.True(delta?.Patch != null, "a ação devolve o que mudou");
			StatePatch.Apply(game, delta!.Patch!);
			Assert.Equal(1, game.CheckIn.Day, "a cópia sabe o dia");
			Assert.Equal(1, game.Inbox.Count, "e recebe a carta");
		}

		[Test]
		private static void TheRealDataHasTwentyEightDaysAndTheAgreedTotals()
		{
			Assert.Equal(CheckInDay.Cycle, Database.CheckIn.Count, "o arquivo tem o ciclo inteiro");
			Assert.True(Database.CheckIn.Any(d => d.Big), "e dias grandes");
			Assert.Empty(Database.Validate().Where(p => p.StartsWith("Check-in")), "sem erro de dados");

			var total = Database.CheckIn.SelectMany(d => d.Rewards).GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.Sum(r => r.Value));
			var agreed = new Dictionary<string, int>
			{
				["gold"] = 1500, ["mana"] = 800, ["scrolls"] = 10, ["essence"] = 10000, ["fragments"] = 400, ["reappraisalGems"] = 1, ["legendaryScrolls"] = 1,
			};
			Assert.True(total.OrderBy(r => r.Key).SequenceEqual(agreed.OrderBy(r => r.Key)), "o ciclo rende o combinado: " + string.Join(", ", total.OrderBy(r => r.Key).Select(r => $"{r.Key} {r.Value}")));
		}

		[Test]
		private static void BadDataIsRefused()
		{
			string Problems(string json) => string.Join("; ", TestData.LoadReal(json).Validate().Where(p => p.StartsWith("Check-in")));

			var days = Enumerable.Range(1, CheckInDay.Cycle).Select(d => $"{{\"day\":{d},\"big\":{(d == 7 ? "true" : "false")},\"rewards\":{{\"gold\":10}}}}").ToList();
			Assert.Equal("", Problems($"[{string.Join(",", days)}]"), "o arquivo certo passa");

			Assert.True(Problems($"[{string.Join(",", days.Take(27))}]").Contains("1 a 28"), "faltando um dia");
			Assert.True(Problems($"[{string.Join(",", days.Select(d => d.Replace("\"gold\"", "\"scroll\"")))}]").Contains("não é uma moeda"), "chave que o correio não conhece");
			Assert.True(Problems($"[{string.Join(",", days.Select(d => d.Replace("\"gold\":10", "\"lightDarkScrolls\":1")))}]").Contains("prêmio de marco"), "Luz e Trevas é de marco");
			Assert.True(Problems($"[{string.Join(",", days.Select(d => d.Replace("\"gold\":10", "\"infusionCores\":1")))}]").Contains("prêmio de marco"), "Núcleo de Infusão é de marco");
			Assert.Equal("", Problems($"[{string.Join(",", days.Select(d => d.Replace("\"gold\":10", "\"legendaryScrolls\":1")))}]"), "o Pergaminho Lendário pode");
			Assert.True(Problems($"[{string.Join(",", days.Select(d => d.Replace("\"gold\":10", "\"gold\":0")))}]").Contains("nada a pagar"), "dia sem nada");
			Assert.True(Problems($"[{string.Join(",", days.Select(d => d.Replace("true", "false")))}]").Contains("nenhum dia grande"), "sem dia grande");
		}
	}
}
