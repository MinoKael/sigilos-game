using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
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
		private static void EveryPageHasAFormula()
		{
			var database = TestData.LoadReal();
			foreach (var page in database.Pages)
				Assert.True(PageFormula.Effects(page).Count > 0, $"página {page.Id} sem efeitos");
		}

		[Test]
		private static void StarterContentExists()
		{
			var database = TestData.LoadReal();
			foreach (var id in NewGame.StarterSummons)
				Assert.True(database.HasSummon(id), $"invocação inicial {id} não existe");
			foreach (var id in NewGame.StarterPages)
				Assert.True(database.HasPage(id), $"página inicial {id} não existe");
			Assert.True(database.Conjurers.Any(c => c.Id == new PlayerState().ConjurerId), "Conjurador padrão não existe");
		}

		[Test]
		private static void StarterPagesResonateWithStarterTeam()
		{
			var database = TestData.LoadReal();
			var glyphs = NewGame.StarterSummons.Select(id => database.Summon(id).Glyph).ToHashSet();
			foreach (var id in NewGame.StarterPages)
				Assert.True(glyphs.Contains(database.Page(id).Glyph), $"página inicial {id} não tem Glifo no time inicial");
		}
	}
}
