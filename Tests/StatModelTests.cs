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

	}
}
