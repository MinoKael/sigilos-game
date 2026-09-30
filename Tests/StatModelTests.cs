using System;
using System.IO;
using System.Linq;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>
	/// O modelo de atributos (Data/stat_model.json): o orçamento de BVP das estrelas naturais, o papel
	/// que reparte e os vieses que nunca criam poder. Os números de Data/summons são conferidos contra ele.
	/// </summary>
	internal static class StatModelTests
	{
		private static StatModel Model => TestData.Database.StatModel;

		[Test]
		private static void BvpWeighsHealthAndSpeed()
		{
			var stats = new StatBlock { Health = 9000, Attack = 700, Defense = 500, Speed = 100 };
			Assert.Near(9000 / Model.HealthWeight + 700 + 500 + 100 * Model.SpeedWeight, Model.Bvp(stats), "BVP = Vida / peso + Ataque + Defesa + Velocidade × peso");
		}

		[Test]
		private static void EverySummonClosesItsBudget()
		{
			foreach (var summon in TestData.Database.Summons)
			{
				foreach (var awakened in new[] { false, true })
				{
					var budget = Model.Budget(summon.Rarity, awakened) ?? throw new AssertionException($"{summon.Id}: {summon.Rarity}★ sem orçamento");
					var stats = summon.StatsFor(awakened);
					Assert.Near(budget, Model.Bvp(stats), $"{summon.Id}{(awakened ? " desperto" : "")}: BVP", Model.Tolerance);
					Assert.True(stats.Health > 0 && stats.Attack > 0 && stats.Defense > 0 && stats.Speed > 0, $"{summon.Id}: atributos positivos");
				}
			}
		}

		[Test]
		private static void RoleMovesTheStatsNotThePower()
		{
			foreach (var stars in Model.Budgets.Keys)
			{
				var byRole = Enum.GetValues<Role>().ToDictionary(role => role, role => Model.Compute(stars, role, false));
				foreach (var (role, stats) in byRole)
					Assert.Near(Model.Budgets[stars], Model.Bvp(stats), $"{stars}★ {role}: o mesmo orçamento", Model.Tolerance);

				Assert.Equal(Role.Attack, byRole.MaxBy(r => r.Value.Attack).Key, $"{stars}★: o papel de Ataque tem o maior Ataque");
				Assert.Equal(Role.HP, byRole.MaxBy(r => r.Value.Health).Key, $"{stars}★: o papel de Vida tem a maior Vida");
				Assert.True(byRole[Role.Defense].Defense > byRole[Role.Attack].Defense, $"{stars}★: o papel de Defesa defende mais que o de Ataque");
			}
		}

		[Test]
		private static void BiasNeverCreatesPower()
		{
			var neutral = Model.Compute(5, Role.Attack, false);
			var fire = new StatWeights { Health = 0.97, Attack = 1.05, Defense = 0.98 };
			var biased = Model.Compute(5, Role.Attack, false, element: fire);

			Assert.True(biased.Attack > neutral.Attack, "o viés de Ataque dá mais Ataque");
			Assert.True(biased.Health < neutral.Health && biased.Defense < neutral.Defense, "tirando de Vida e Defesa");
			Assert.Near(Model.Bvp(neutral), Model.Bvp(biased), "e o BVP não muda", Model.Tolerance);

			var family = new StatWeights { Health = 1.03, Attack = 0.99, Defense = 1.02 };
			var both = Model.Compute(5, Role.Attack, false, element: fire, family: family);
			Assert.Near(Model.Bvp(neutral), Model.Bvp(both), "viés de elemento e de família juntos também não", Model.Tolerance);
		}

		[Test]
		private static void SpeedIsPaidFromTheSameBudget()
		{
			var plain = Model.Compute(4, Role.Support, false);
			var fast = Model.Compute(4, Role.Support, false, manual: new StatWeights { Speed = 10 });

			Assert.Near(plain.Speed + 10, fast.Speed, "+10 de Velocidade");
			Assert.Near(Model.Bvp(plain), Model.Bvp(fast), "o orçamento é o mesmo", Model.Tolerance);
			var paid = (plain.Health - fast.Health) / Model.HealthWeight + (plain.Attack - fast.Attack) + (plain.Defense - fast.Defense);
			Assert.Near(10 * Model.SpeedWeight, paid, "cada ponto de Velocidade custa o peso dela em Vida, Ataque e Defesa", Model.Tolerance);
		}

		[Test]
		private static void AwakeningHasItsOwnBudget()
		{
			foreach (var stars in Model.Budgets.Keys)
			{
				var plain = Model.Compute(stars, Role.Attack, false);
				var awakened = Model.Compute(stars, Role.Attack, true);
				Assert.Near(Model.AwakenedBudgets[stars], Model.Bvp(awakened), $"{stars}★ desperto fecha o orçamento desperto", Model.Tolerance);
				Assert.Near(plain.Speed + Model.AwakeningSpeed, awakened.Speed, $"{stars}★: a Velocidade do Despertar");
				Assert.True(awakened.Health > plain.Health && awakened.Attack > plain.Attack && awakened.Defense > plain.Defense, $"{stars}★: Vida, Ataque e Defesa sobem");
			}
		}

		[Test]
		private static void MoreNaturalStarsMeanABiggerBudget()
		{
			var budgets = Model.Budgets.OrderBy(b => b.Key).Select(b => b.Value).ToList();
			Assert.True(budgets.Zip(budgets.Skip(1), (low, high) => high > low).All(grows => grows), "o orçamento sobe com as estrelas naturais");
		}

		[Test]
		private static void StoredStatsThatLeaveTheModelAreReported()
		{
			// Mexer num número na mão (ou mudar o modelo sem recalcular) tem de aparecer na validação.
			var data = Path.Combine(Program.ProjectRoot, "Data");
			string Read(string file) => File.ReadAllText(Path.Combine(data, file));

			var dragon = TestData.Summon("dragon_fire");
			var tampered = Read("summons/dragon_family.json").Replace($"\"atk\": {dragon.Stats.Attack:0},", $"\"atk\": {dragon.Stats.Attack + 100:0},");
			var database = GameDatabase.FromJson(Read("stat_model.json"), new[] { tampered }, Read("enemies.json"), "[]", "[]", "[]");

			var problems = database.Validate().Where(p => p.Contains("dragon_")).ToList();
			Assert.True(problems.Any(p => p.Contains("o modelo dá")), "o atributo fora do modelo é apontado");
			Assert.True(problems.Any(p => p.Contains("fora do orçamento")), "e o BVP fora do orçamento também");
		}
	}
}
