using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Player;

namespace Sigilos.Tests
{
	internal static class DataTests
	{
		[Test]
		private static void RealDataHasNoProblems()
		{
			Assert.Empty(TestData.LoadReal().Validate(), "Data/ tem problemas");
		}

		[Test]
		private static void EveryFamilyHasFiveElements()
		{
			var database = TestData.LoadReal();
			foreach (var family in database.Families)
			{
				var elements = database.Summons.Where(s => s.FamilyId == family.Id).Select(s => s.Element).Distinct().Count();
				Assert.Equal(5, elements, $"elementos da família {family.Id}");
			}
		}

		[Test]
		private static void AwakenedNamesAreUnique()
		{
			var database = TestData.LoadReal();
			var names = database.Summons.Select(s => s.Awakening.Name).ToList();
			Assert.Equal(names.Count, names.Distinct().Count(), "nomes de Despertar repetidos");
		}

		[Test]
		private static void NoEnhancementIsCheaperThanTheMinimum()
		{
			// Sem isto volta o ciclo de usa-e-ganha: aprimorar com o Éter que o próprio turno rende.
			var database = TestData.LoadReal();
			var skills = database.Summons.SelectMany(s => new[] { s.Basic, s.Special }).Where(s => s.CanEnhance);
			foreach (var skill in skills)
				Assert.True(skill.EnhanceCost >= BattleRules.MinEnhanceCost, $"'{skill.Name}' custa {skill.EnhanceCost} Éter");
		}

		[Test]
		private static void StarterContentExists()
		{
			var database = TestData.LoadReal();
			foreach (var id in NewGame.StarterSummons)
				Assert.True(database.HasSummon(id), $"invocação inicial {id} não existe");
		}
	}
}
