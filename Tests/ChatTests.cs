using System;
using System.Collections.Generic;
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

			var rune = new Rune { Set = RuneSet.Vigor, Slot = 2, Grade = 6, Main = RuneStat.AttackPercent, Level = RuneRules.MaxLevel, Substats = { RuneSubstat.Rolled(RuneStat.Speed, 0, 5) } };
			Assert.Equal(new RuneFeat(RuneSet.Vigor, 2, 6, RuneStat.AttackPercent, rune.Substats), Feats.Of(rune, 12), "chegou a +15 agora, com os subatributos");
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

			var speed = RuneSubstat.Rolled(RuneStat.Speed, 0, 5);
			speed.Rolls.Add(new RuneRoll(3, 4));
			speed.Enchanted = true;
			speed.Original = RuneSubstat.Rolled(RuneStat.DefensePercent, 0, 0.05);
			var rune = JsonDocument.Parse(ChatLink.Write(new RuneFeat(RuneSet.Vigor, 2, 6, RuneStat.AttackPercent, new List<RuneSubstat> { speed }))).RootElement;
			Assert.Equal("Vigor", rune.GetProperty("set").GetString(), "o conjunto pelo nome");
			Assert.Equal("AttackPercent", rune.GetProperty("main").GetString(), "o principal pelo nome");
			Assert.Equal(6, rune.GetProperty("grade").GetInt32(), "as estrelas");
			Assert.Equal(7, rune.EnumerateObject().Count(), "sem o nível nem o dono");
			var substat = rune.GetProperty("substats")[0];
			Assert.Equal("Speed", substat.GetProperty("stat").GetString(), "o subatributo pelo nome");
			Assert.Equal(2, substat.GetProperty("rolls").GetArrayLength(), "os sorteios");
			Assert.Equal(true, substat.GetProperty("enchanted").GetBoolean(), "encantado");
			Assert.False(substat.TryGetProperty("original", out _), "sem o que a gema trocou");
		}

		[Test]
		private static void ReadsLinesFeatsAndRefusals()
		{
			var say = ChatLink.Read("""{"type":"say","from":"Mestre","at":"2026-10-07T14:05:00+00:00","text":"oi [b]pessoal[/b]"}""", out var error);
			Assert.Equal(new ChatLine("Mestre", Now, "oi [b]pessoal[/b]", null), say, "a fala chega como veio");
			Assert.Equal(null, error, "sem erro");

			var rune = ChatLink.Read("""{"type":"feat","feat":"rune","from":"Aprendiz","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":2,"grade":6,"main":"AttackPercent","substats":[{"stat":"Speed","rolls":[{"level":0,"amount":5},{"level":3,"amount":4}],"grind":2,"enchanted":true}]}""", out _)?.Feat as RuneFeat;
			Assert.Equal((RuneSet.Vigor, 2, 6, RuneStat.AttackPercent), (rune!.Set, rune.Slot, rune.Grade, rune.Main), "o feito da runa");
			var speed = rune.Substats.Single();
			Assert.Equal((RuneStat.Speed, 9.0, 2.0, true), (speed.Stat, speed.Value, speed.Grind, speed.Enchanted), "o subatributo, com os sorteios, a pedra e a gema");

			var older = ChatLink.Read("""{"type":"feat","feat":"rune","from":"Aprendiz","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":2,"grade":6,"main":"AttackPercent"}""", out _)?.Feat as RuneFeat;
			Assert.Equal(0, older?.Substats.Count, "sem os subatributos (servidor que não os repassa), o feito vem mesmo assim");

			Assert.Equal(null, ChatLink.Read("""{"type":"error","error":"rate_limited"}""", out error), "recusa não é linha");
			Assert.Equal("rate_limited", error, "o erro da recusa");

			string[] unknown =
			{
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Bogus","slot":2,"grade":6,"main":"Speed"}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"0","slot":2,"grade":6,"main":"Speed"}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":7,"grade":6,"main":"Speed"}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":2,"grade":6,"main":"Speed","substats":[{"stat":"Bogus","rolls":[]}]}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":2,"grade":6,"main":"Speed","substats":[{"stat":5,"rolls":[]}]}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":2,"grade":6,"main":"Speed","substats":[{"stat":"Speed","rolls":null}]}""",
				"""{"type":"feat","feat":"rune","from":"A","at":"2026-10-07T14:05:00Z","set":"Vigor","slot":2,"grade":6,"main":"Speed","substats":"Speed"}""",
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
