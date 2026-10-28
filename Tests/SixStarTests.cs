using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>O marco de chegar a 6★: o primeiro de cada elemento paga o prêmio grande, os outros o pequeno, por carta.</summary>
	internal static class SixStarTests
	{
		private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

		/// <summary>Um monstro no nível máximo das estrelas dadas, pronto para evoluir.</summary>
		private static OwnedSummon Ready(PlayerState player, string summonId, int stars)
		{
			var monster = Roster.Add(player, TestData.Summon(summonId));
			monster.Stars = stars;
			monster.Level = Growth.MaxLevel(stars);
			return monster;
		}

		private static ActionDelta? Evolve(PlayerState player, OwnedSummon monster)
		{
			player.Fragments += 1000;
			return ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.Evolve, Id = monster.Id }, new ActionContext(Now, _ => null));
		}

		private static Letter? SixStarLetter(PlayerState player) => player.Inbox.LastOrDefault(l => l.Kind is Letters.SixStarFirst or Letters.SixStar);

		[Test]
		private static void TheFirstSixStarOfAnElementPaysTheBigPrize()
		{
			var player = new PlayerState();
			var lich = Ready(player, "lich_fire", 5);

			Assert.True(Evolve(player, lich) != null, "evolui");
			var letter = SixStarLetter(player)!;
			Assert.Equal(Letters.SixStarFirst, letter.Kind, "é o primeiro do Fogo");
			Assert.Equal("lich_fire,Fire", string.Join(",", letter.Args), "com o monstro e o elemento");
			Assert.True(letter.Rewards.OrderBy(r => r.Key).SequenceEqual(Milestones.FirstSixStar.OrderBy(r => r.Key)), "e o prêmio grande");
			Assert.Equal(0, player.Mana, "por carta, não direto");

			var claim = ServerActions.Apply(new Random(1), TestData.Database, player, new ActionRequest { Kind = ServerActionKind.ClaimMail, Text = Letters.MailId(letter) }, new ActionContext(Now, _ => null));
			Assert.True(claim != null, "coleta");
			Assert.Equal(200, player.Mana, "200 de Mana");
			Assert.Equal(1000, player.Essence, "1.000 de Essência");
			Assert.Equal(1, player.Monsters.Count(m => m.IsInfusionCore), "um Núcleo de Infusão");
			var rune = player.Runes.Single();
			Assert.Equal(6, rune.Grade, "um sigilo 6★");
			Assert.Equal(RuneRarity.Legendary, rune.Rarity, "Lendário");
		}

		[Test]
		private static void EachSixStarAfterTheFirstOfTheElementPaysTheSmallPrize()
		{
			var player = new PlayerState();
			Evolve(player, Ready(player, "lich_fire", 5));
			Evolve(player, Ready(player, "knight_fire", 5));

			var letter = SixStarLetter(player)!;
			Assert.Equal(Letters.SixStar, letter.Kind, "o segundo do Fogo é comum");
			Assert.Equal("knight_fire", letter.Args.Single(), "com o monstro");
			Assert.True(letter.Rewards.OrderBy(r => r.Key).SequenceEqual(Milestones.NextSixStar.OrderBy(r => r.Key)), "300 de Mana e 1.000 de Essência");
			Assert.False(letter.Rewards.Keys.Any(k => k.StartsWith("rune")), "sem sigilo");
		}

		[Test]
		private static void AnotherElementIsFirstAgain()
		{
			var player = new PlayerState();
			Evolve(player, Ready(player, "lich_fire", 5));
			Evolve(player, Ready(player, "pirate_water", 5));

			Assert.Equal(Letters.SixStarFirst, SixStarLetter(player)!.Kind, "a Água tem o primeiro dela");
			Assert.Equal(2, player.Inbox.Count(l => l.Kind == Letters.SixStarFirst), "duas cartas grandes");
			Assert.Equal("Fire,Water", string.Join(",", player.SixStarElements), "e os dois elementos anotados");
		}

		[Test]
		private static void ReleasingTheFirstSixStarDoesNotMakeTheNextOneFirst()
		{
			var player = new PlayerState();
			var first = Ready(player, "lich_fire", 5);
			Evolve(player, first);
			player.Monsters.Remove(first);
			Evolve(player, Ready(player, "knight_fire", 5));

			Assert.Equal(Letters.SixStar, SixStarLetter(player)!.Kind, "o Fogo já teve o primeiro");
			Assert.Equal(1, player.Inbox.Count(l => l.Kind == Letters.SixStarFirst), "só uma carta grande");
		}

		[Test]
		private static void SixStarsThatAlreadyExistAreNotPaidBackwards()
		{
			var player = new PlayerState();
			Ready(player, "lich_fire", 6);
			Evolve(player, Ready(player, "knight_fire", 5));

			Assert.Equal(Letters.SixStar, SixStarLetter(player)!.Kind, "quem já tinha um 6★ de Fogo não leva o primeiro");
			Evolve(player, Ready(player, "pirate_water", 5));
			Assert.Equal(Letters.SixStarFirst, SixStarLetter(player)!.Kind, "mas o elemento sem 6★ leva");
		}

		[Test]
		private static void EvolvingBelowSixStarsSendsNothing()
		{
			var player = new PlayerState();
			Assert.True(Evolve(player, Ready(player, "lich_fire", 3)) != null, "evolui");
			Assert.Equal(0, player.Inbox.Count, "sem carta antes do 6★");
			Assert.Equal(0, player.SixStarElements.Count, "e sem elemento anotado");
		}

		[Test]
		private static void TheSixStarLetterTravelsInTheSave()
		{
			var server = new PlayerState { Version = PlayerState.CurrentVersion };
			var game = PlayerSave.FromJson(PlayerSave.ToJson(server))!;
			var lich = Ready(server, "lich_fire", 5);
			game.Monsters.Add(new OwnedSummon { Id = lich.Id, SummonId = lich.SummonId, Stars = 5, Level = lich.Level });
			var delta = Evolve(server, lich);

			Assert.True(delta?.Patch != null, "a ação devolve o que mudou");
			StatePatch.Apply(game, delta!.Patch!);
			Assert.Equal(1, game.Inbox.Count(l => l.Kind == Letters.SixStarFirst), "a cópia recebe a carta");
			Assert.Equal("Fire", string.Join(",", game.SixStarElements), "e o elemento");
		}
	}
}
