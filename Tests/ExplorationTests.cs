using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>
	/// A Exploração Estelar (GDD, seção 11): o percurso pelas 88 constelações, o rodízio de três
	/// Explorações por mês, o recomeço mensal, a recompensa fixa e as Influências na luta.
	/// </summary>
	internal static class ExplorationTests
	{
		private static readonly DateTime October = new(2026, 10, 5, 12, 0, 0);
		private static readonly DateTime November = new(2026, 11, 1, 0, 0, 1);

		[Test]
		private static void TheSkyHasTheEightyEightConstellationsInThreeBands()
		{
			var exploration = TestData.Database.Exploration;
			Assert.Equal(88, exploration.Constellations.Count, "as 88 constelações");
			Assert.Equal(21, exploration.Constellations.Count(c => c.Hemisphere == Hemisphere.Boreal), "Boreais");
			Assert.Equal(30, exploration.Constellations.Count(c => c.Hemisphere == Hemisphere.Equatorial), "Equatoriais (com as 12 do zodíaco)");
			Assert.Equal(37, exploration.Constellations.Count(c => c.Hemisphere == Hemisphere.Austral), "Austrais");
			Assert.Equal((1, 21), exploration.Range(Hemisphere.Boreal), "as Boreais abrem o percurso");
			Assert.Equal((52, 88), exploration.Range(Hemisphere.Austral), "as Austrais fecham");
			Assert.Equal(3, exploration.Explorations.Count, "três Explorações no rodízio");
			Assert.True(exploration.Constellations.All(c => c.Challenges.Count == 3), "um desafio por Exploração em cada constelação");
			Assert.True(exploration.Constellations.All(c => c.Influence.Rules.Count > 0), "toda constelação tem a sua mecânica");
			Assert.True(exploration.Constellations.Zip(exploration.Constellations.Skip(1)).All(p => p.Second.Level >= p.First.Level && p.Second.Scale >= p.First.Scale), "a força só cresce no percurso");
		}

		[Test]
		private static void TheJourneyOpensAfterItsStageAndGoesInOrder()
		{
			var exploration = TestData.Database.Exploration;
			var player = TestData.PlayerWith("wizard_fire");
			Assert.False(Exploration.IsOpen(player, exploration), "fechada antes da fase");
			Assert.Equal(EntryProblem.Locked, Exploration.Check(player, exploration, 1, October), "nem a primeira");

			player.HighestStage = exploration.UnlockStage;
			// Conta no nível máximo: a vitória não sobe o nível, que encheria a Mana.
			player.AccountLevel = Account.MaxLevel;
			Assert.Equal(EntryProblem.None, Exploration.Check(player, exploration, 1, October), "a primeira abre");
			Assert.Equal(EntryProblem.Locked, Exploration.Check(player, exploration, 2, October), "a segunda espera a primeira");
			var mana = player.Mana;
			Exploration.ApplyVictory(player, exploration, 1, October);
			Assert.Equal(mana, player.Mana, "a luta não custa Mana");
			Assert.Equal(EntryProblem.None, Exploration.Check(player, exploration, 2, October), "vencer abre a seguinte");
		}

		[Test]
		private static void TheFirstVictoryOfTheMonthPaysTheFixedReward()
		{
			var exploration = TestData.Database.Exploration;
			var player = TestData.PlayerWith("wizard_fire");
			player.HighestStage = exploration.UnlockStage;
			player.AccountLevel = Account.MaxLevel;
			var reward = exploration.Constellation(1).Reward;
			// No nível máximo, a experiência da constelação também paga Essência, um terço dela.
			var paid = reward.Essence + reward.Experience / Account.ExperiencePerEssenceAtMax;

			var essence = player.Essence;
			var gold = player.Gold;
			var first = Exploration.ApplyVictory(player, exploration, 1, October);
			Assert.True(first.FirstClear, "a primeira do mês");
			Assert.Equal(essence + paid, player.Essence, "a Essência da constelação");
			Assert.Equal(gold + reward.Gold, player.Gold, "o Ouro da constelação");

			var again = Exploration.ApplyVictory(player, exploration, 1, October);
			Assert.False(again.FirstClear, "repetir no mesmo mês");
			Assert.Equal(0, again.Essence, "não paga de novo");
			Assert.Equal(essence + paid, player.Essence, "a conta não muda");

			// No mês seguinte, outra Exploração: o percurso recomeça e paga o mesmo.
			Assert.Equal(0, Exploration.Cleared(player, November), "o mês novo começa do zero");
			var next = Exploration.ApplyVictory(player, exploration, 1, November);
			Assert.True(next.FirstClear, "a primeira de novembro");
			Assert.Equal(paid, next.Essence, "a mesma recompensa");
			Assert.Equal(1, player.ExplorationBest, "o recorde fica");
		}

		/// <summary>A vitória que termina num mês novo, numa constelação que o mês ainda não abriu, não paga.</summary>
		[Test]
		private static void AVictoryThatEndsInANewMonthDoesNotPayAClosedConstellation()
		{
			var exploration = TestData.Database.Exploration;
			var player = TestData.PlayerWith("wizard_fire");
			player.HighestStage = exploration.UnlockStage;
			for (var number = 1; number <= 4; number++)
				Exploration.ApplyVictory(player, exploration, number, October);
			Assert.Equal(4, Exploration.Cleared(player, October), "quatro em outubro");

			var late = Exploration.ApplyVictory(player, exploration, 5, November);
			Assert.False(late.FirstClear, "a 5 não estava aberta em novembro");
			Assert.Equal(0, player.ExplorationCleared, "o percurso recomeçou");
			Assert.Equal(4, player.ExplorationBest, "o recorde é o de outubro");
		}

		private static IEnumerable<BattleUnit> Waves(BattleSession session) => session.AllFoes;
	}
}
