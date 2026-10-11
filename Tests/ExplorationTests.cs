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

		/// <summary>Em muitas constelações o desafio são as habilidades de uma invocação: ela guarda a constelação.</summary>
		[Test]
		private static void ManyGuardiansAreSummons()
		{
			var exploration = TestData.Database.Exploration;
			var guardians = exploration.Constellations.SelectMany(c => Enumerable.Range(0, 3).Select(c.Guardian)).ToList();
			Assert.True(guardians.All(g => g != null), "todo desafio tem guardião");
			Assert.True(guardians.Count(g => g!.Summon != null) > guardians.Count / 2, "a maioria dos guardiões é invocação");
		}

		[Test]
		private static void TheExplorationRotatesEveryMonth()
		{
			Assert.Equal(0, Exploration.VariationOf(October), "outubro de 2026: a primeira");
			Assert.Equal(1, Exploration.VariationOf(November), "novembro: a segunda");
			Assert.Equal(2, Exploration.VariationOf(new DateTime(2026, 12, 31)), "dezembro: a terceira");
			Assert.Equal(0, Exploration.VariationOf(new DateTime(2027, 1, 1)), "janeiro volta à primeira");
			Assert.Equal(new DateTime(2026, 11, 1), Exploration.NextRotation(October), "a troca é no dia 1");
			Assert.Equal(new DateTime(2027, 1, 1), Exploration.NextRotation(new DateTime(2026, 12, 20)), "e vira o ano");
		}

		[Test]
		private static void TheJourneyOpensAfterItsStageAndGoesInOrder()
		{
			var exploration = TestData.Database.Exploration;
			var player = TestData.PlayerWith("phoenix_fire");
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
			var player = TestData.PlayerWith("phoenix_fire");
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
			var player = TestData.PlayerWith("phoenix_fire");
			player.HighestStage = exploration.UnlockStage;
			for (var number = 1; number <= 4; number++)
				Exploration.ApplyVictory(player, exploration, number, October);
			Assert.Equal(4, Exploration.Cleared(player, October), "quatro em outubro");

			var late = Exploration.ApplyVictory(player, exploration, 5, November);
			Assert.False(late.FirstClear, "a 5 não estava aberta em novembro");
			Assert.Equal(0, player.ExplorationCleared, "o percurso recomeçou");
			Assert.Equal(4, player.ExplorationBest, "o recorde é o de outubro");
		}

		[Test]
		private static void TheMonthlyRewardsMatchTheBudget()
		{
			var total = Exploration.MonthlyTotal(TestData.Database.Exploration);
			Assert.Equal(3, total.Legendary, "um Pergaminho Lendário no fim de cada faixa");
			Assert.Equal(1, total.LightDark, "um de Luz e Trevas no Cruzeiro do Sul");
			Assert.Equal(8, total.Cores, "um Núcleo de Infusão a cada 11 constelações");
			Assert.Equal(22, total.Scrolls, "um Pergaminho Místico a cada 4");
		}

		/// <summary>Cada regra da Influência entra nas unidades do lado dela, e só nelas.</summary>
		[Test]
		private static void InfluenceRulesLandOnTheirSide()
		{
			var database = TestData.Database;
			var team = ReferenceTeams.AtStage(database, 30);
			foreach (var constellation in database.Exploration.Constellations)
			{
				var session = BattleFactory.Create(database, team, constellation.Encounter(0), 1);
				var foes = Waves(session).ToList();
				foreach (var side in Enum.GetValues<InfluenceSide>())
				{
					var rules = constellation.Influence.Rules.Count(r => r.Side == side);
					if (rules == 0)
						continue;
					var bearers = side switch
					{
						InfluenceSide.Allies => session.Allies,
						InfluenceSide.Guardian => foes.Where(u => u.IsBoss).ToList(),
						InfluenceSide.Everyone => session.Allies.Concat(foes).ToList(),
						_ => foes,
					};
					Assert.True(bearers.Count > 0, $"{constellation.Id}: alguém leva a regra de {side}");
				}

				var count = constellation.Influence.Rules.Count;
				Assert.True(session.Allies.All(u => u.Influences.Count == constellation.Influence.Rules.Count(r => r.Side is InfluenceSide.Allies or InfluenceSide.Everyone)), $"{constellation.Id}: as regras do time");
				Assert.True(foes.All(u => u.Influences.Count <= count), $"{constellation.Id}: nenhum inimigo leva regra a mais");
			}
		}

		/// <summary>O guardião que é invocação luta desperto, com as habilidades no máximo e a Vida de chefe.</summary>
		[Test]
		private static void ASummonGuardianFightsAwakenedAndMaxed()
		{
			var database = TestData.Database;
			var draco = database.Exploration.Constellations.First(c => c.Id == "draco");
			var encounter = draco.Encounter(0);
			var slot = draco.Guardian(0)!;
			var summon = database.Summon(slot.Summon!);
			var guardian = BattleFactory.Foe(database, slot, encounter);
			var common = BattleFactory.Foe(database, slot with { Guardian = false }, encounter);

			Assert.True(guardian.IsBoss, "o guardião é o chefe da luta");
			Assert.True(guardian.Awakened, "desperto");
			Assert.Equal(summon.Awakening.Name, guardian.Name, "com o nome do Despertar");
			Assert.True(guardian.Stats.Health > common.Stats.Health * BattleFactory.GuardianHealth, "Vida de chefe");
			Assert.False(common.IsBoss, "a mesma invocação sem a marca é inimigo comum");
		}

		/// <summary>O Esquecimento cala a Passiva da unidade, não a Influência do céu.</summary>
		[Test]
		private static void OblivionDoesNotSilenceTheInfluence()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.DamageReduction, Value = 0.5 };
			var unit = TestData.Unit("Guardião", Side.Enemies, passive: passive);
			unit.AddInfluence(new PassiveDefinition { Kind = PassiveKind.DamageReduction, Value = 0.2 });
			Assert.Near(0.5 * 0.8, unit.DamageTaken(), "a Passiva e a Influência");

			unit.AddStatus(new StatusEffect(StatusKind.Oblivion, 2));
			Assert.True(unit.PassiveSuppressed, "a Passiva calou");
			Assert.Near(0.8, unit.DamageTaken(), "a Influência continua");
		}

		/// <summary>Uma regra para todos vale nos dois lados: a Canícula queima o time do jogador também.</summary>
		[Test]
		private static void AnEveryoneRuleReachesBothSides()
		{
			var database = TestData.Database;
			var dogDays = database.Exploration.Constellations.First(c => c.Id == "canis_major");
			Assert.True(dogDays.Influence.Rules.All(r => r.Side == InfluenceSide.Everyone), "a Canícula é de todos");
			var session = BattleFactory.Create(database, ReferenceTeams.AtStage(database, 30), dogDays.Encounter(1), 1);
			Assert.True(session.Allies.All(u => u.Influences.Count == 1), "no time do jogador");
			Assert.True(Waves(session).All(u => u.Influences.Count == 1), "e nos inimigos");
		}

		private static IEnumerable<BattleUnit> Waves(BattleSession session) => session.AllFoes;
	}
}
