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
		private static void StarterContentExists()
		{
			var database = TestData.LoadReal();
			foreach (var id in NewGame.StarterSummons)
				Assert.True(database.HasSummon(id), $"invocação inicial {id} não existe");
		}
	}
}
