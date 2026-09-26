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
		private static void AwakeningFollowsNaturalStars()
		{
			// 3★ ganham habilidade nova, 4★ uma habilidade mais forte, 5★ um atributo.
			foreach (var summon in TestData.Database.Summons)
			{
				switch (summon.Rarity)
				{
					case 3:
						Assert.True(summon.Awakening.Skill != null, $"{summon.Id}: 3★ ganha habilidade no Despertar");
						break;
					case 4:
						Assert.True(summon.Skills.Any(s => s.ChangesOnAwakening), $"{summon.Id}: 4★ melhora uma habilidade");
						break;
					default:
						Assert.True(summon.Awakening.Stat != null && summon.Skills.All(s => !s.ChangesOnAwakening), $"{summon.Id}: 5★ só ganha atributo");
						break;
				}
			}
		}

		[Test]
		private static void EveryActiveSkillCanLevelUp()
		{
			foreach (var summon in TestData.Database.Summons)
			{
				foreach (var skill in summon.AllSkills.Where(s => !s.IsPassive))
					Assert.True(skill.MaxLevel > 1, $"{summon.Id}: '{skill.Name}' sobe de nível com cópias");
			}
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
