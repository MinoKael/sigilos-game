using System;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;
using Sigilos.Core.Social;
using Sigilos.Core.Summoning;
using Sigilos.GameEntry.Account;
using Sigilos.UI;

namespace Sigilos.Tests
{
	/// <summary>
	/// O Chat global do lado do jogo: o que vira feito, as mensagens como o servidor as manda e recebe
	/// (docs/SERVIDOR_PROPRIO.md) e as linhas guardadas só na memória.
	/// </summary>
	internal static class ChatTests
	{
		private static readonly DateTimeOffset Now = new(2026, 10, 7, 14, 5, 0, TimeSpan.Zero);

		[Test]
		private static void OnlyFiveStarSummonsAndRunesReachingFifteenAreFeats()
		{
			var five = TestData.Database.Summons.First(s => s.Rarity == Feats.SummonStars);
			var four = TestData.Database.Summons.First(s => s.Rarity == Feats.SummonStars - 1);
			SummonResult Result(SummonDefinition summon) => new(summon, new OwnedSummon { SummonId = summon.Id }, false);

			var feats = Feats.Of(new[] { Result(four), Result(five), Result(four) }).ToList();
			Assert.Equal(1, feats.Count, "só a 5★ vira feito");
			Assert.Equal(new SummonFeat(five.Id), feats[0], "o feito leva o id da variante");

			var rune = new Rune { Set = RuneSet.Vigor, Slot = 2, Grade = 6, Main = RuneStat.AttackPercent, Level = RuneRules.MaxLevel };
			Assert.Equal(new RuneFeat(RuneSet.Vigor, 2, 6, RuneStat.AttackPercent), Feats.Of(rune, 12), "chegou a +15 agora");
			Assert.Equal(null, Feats.Of(rune, RuneRules.MaxLevel), "já estava em +15");
			rune.Level = 14;
			Assert.Equal(null, Feats.Of(rune, 12), "ainda não chegou");
		}

		[Test]
		private static void FeatsGoOutWithOnlyWhatShowsThem()
		{
			var summon = JsonDocument.Parse(ChatLink.Write(new SummonFeat("knight_fire"))).RootElement;
			Assert.Equal("feat", summon.GetProperty("type").GetString(), "tipo");
			Assert.Equal("summon", summon.GetProperty("feat").GetString(), "feito de invocação");
			Assert.Equal("knight_fire", summon.GetProperty("summon").GetString(), "a variante");
			Assert.Equal(3, summon.EnumerateObject().Count(), "nada além disso");

			var rune = JsonDocument.Parse(ChatLink.Write(new RuneFeat(RuneSet.Vigor, 2, 6, RuneStat.AttackPercent))).RootElement;
			Assert.Equal("Vigor", rune.GetProperty("set").GetString(), "o conjunto pelo nome");
			Assert.Equal("AttackPercent", rune.GetProperty("main").GetString(), "o principal pelo nome");
			Assert.Equal(6, rune.GetProperty("grade").GetInt32(), "as estrelas");
			Assert.Equal(6, rune.EnumerateObject().Count(), "sem subatributos nem dono");
		}

		[Test]
		private static void ReadsLinesFeatsAndRefusals()
		{
			var say = ChatLink.Read("""{"type":"say","from":"Mestre","at":"2026-10-07T14:05:00+00:00","text":"oi [b]pessoal[/b]"}""", out var error);
			Assert.Equal(new ChatLine("Mestre", Now, "oi [b]pessoal[/b]", null), say, "a fala chega como veio");
			Assert.Equal(null, error, "sem erro");

			var rune = ChatLink.Read("""{"type":"feat","feat":"rune","from":"Aprendiz","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":2,"grade":6,"main":"AttackPercent"}""", out _);
			Assert.Equal(new RuneFeat(RuneSet.Vigor, 2, 6, RuneStat.AttackPercent), rune?.Feat, "o feito da runa");

			Assert.Equal(null, ChatLink.Read("""{"type":"error","error":"rate_limited"}""", out error), "recusa não é linha");
			Assert.Equal("rate_limited", error, "o erro da recusa");

			string[] unknown =
			{
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Bogus","slot":2,"grade":6,"main":"Speed"}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"0","slot":2,"grade":6,"main":"Speed"}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":7,"grade":6,"main":"Speed"}""",
				"""{"type":"feat","feat":"pet","from":"A","at":"2026-10-07T14:05:00Z"}""",
				"""{"type":"shout","from":"A","at":"2026-10-07T14:05:00Z","text":"oi"}""",
				"""{"type":"say","at":"2026-10-07T14:05:00Z","text":"sem nome"}""",
				"não é json",
			};
			foreach (var json in unknown)
			{
				Assert.Equal(null, ChatLink.Read(json, out error), $"fica de fora: {json}");
				Assert.Equal(null, error, $"sem erro: {json}");
			}
		}

		[Test]
		private static void TheFeedKeepsOnlyTheLastLines()
		{
			var feed = new ChatFeed { Me = "Mestre" };
			for (var i = 0; i < ChatFeed.MaxLines + 5; i++)
				feed.Add(new ChatLine("Aprendiz", Now, $"linha {i}", null));
			Assert.Equal(ChatFeed.MaxLines, feed.Lines.Count, "as mais velhas saem");
			Assert.Equal("linha 5", feed.Lines[0].Text, "fica a partir da sexta");

			feed.Clear();
			Assert.Equal(0, feed.Lines.Count, "sair da conta esquece o chat");
		}
	}
}
