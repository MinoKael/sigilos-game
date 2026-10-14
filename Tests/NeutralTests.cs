using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>
	/// Homúnculo de 2★ não tem variantes: uma só, de elemento Neutro (<c>{família}_neutral</c>). As de elemento
	/// das famílias de antes ficam no arquivo, aposentadas (<c>"active": false</c>), e ainda resolvem os ids.
	/// </summary>
	internal static class NeutralTests
	{
		[Test]
		private static void TwoStarHomunculiHaveOneNeutralVariantAndOnlyThey()
		{
			var database = TestData.Database;
			var twoStar = database.Summons.Where(s => s.Rarity == 2).ToList();
			Assert.True(twoStar.Count > 0, "há homúnculos de 2★ em jogo");
			Assert.True(twoStar.All(s => s.Element == Element.Neutral && s.Id.EndsWith("_neutral")), "os de 2★ são Neutros");
			Assert.Equal(twoStar.Count, twoStar.Select(s => s.FamilyId).Distinct().Count(), "uma variante por família de 2★");
			Assert.True(database.Summons.Where(s => s.Rarity != 2).All(s => s.Element != Element.Neutral), "só os de 2★ são Neutros");
		}

		[Test]
		private static void RetiredVariantsStayInTheFilesAndStillResolve()
		{
			var database = TestData.Database;
			Assert.True(database.Knows("slime_fire"), "o id antigo ainda resolve (saves, inimigos)");
			Assert.False(database.HasSummon("slime_fire"), "mas está fora do jogo");
			Assert.False(database.Summons.Any(s => s.Id == "slime_fire"), "e fora das listas");
			Assert.False(database.Summon("slime_fire").Active, "aposentada");
			Assert.True(database.HasSummon("slime_neutral"), "a Neutra está em jogo");
			Assert.Equal("Gosma", database.Summon("slime_neutral").Name, "o nome é o da família");
		}

		[Test]
		private static void NeutralHasNeitherAdvantageNorDisadvantage()
		{
			foreach (var element in new[] { Element.Fire, Element.Water, Element.Wind, Element.Light, Element.Dark, Element.Neutral })
			{
				Assert.Near(1, ElementChart.Multiplier(Element.Neutral, element), $"Neutro contra {element}");
				Assert.Near(1, ElementChart.Multiplier(element, Element.Neutral), $"{element} contra Neutro");
			}
		}

		[Test]
		private static void NeutralComesFromTheCommonScrollsNotFromLightAndDark()
		{
			Assert.True(SummonRates.Allows(ScrollKind.Mystic, Element.Neutral), "o Místico sorteia Neutros");
			Assert.False(SummonRates.Allows(ScrollKind.LightDark, Element.Neutral), "o de Luz e Trevas, não");
			Assert.False(SummonRates.MysticLightDarkEligible(TestData.Summon("slime_neutral")), "e a fatia de Luz e Trevas do Místico também não");
		}

		[Test]
		private static void ValidationRejectsTwoStarFamiliesThatBreakTheRule()
		{
			var data = Path.Combine(Program.ProjectRoot, "Data");
			string Read(string file) => File.ReadAllText(Path.Combine(data, file));
			var slime = Read("summons/slime_family.json");

			var elemental = slime.Replace("\"element\": \"Neutral\"", "\"element\": \"Wind\"");
			var database = GameDatabase.FromJson(Read("stat_model.json"), new[] { elemental }, Read("enemies.json"), "[]", "[]", "[]");
			Assert.True(database.Validate().Any(p => p.Contains("slime_neutral") && p.Contains("só as de 2★ são Neutras")), "2★ com elemento comum é apontada");

			var revived = new Regex(Regex.Escape("\"active\": false,")).Replace(slime, "", 1);
			database = GameDatabase.FromJson(Read("stat_model.json"), new[] { revived }, Read("enemies.json"), "[]", "[]", "[]");
			Assert.True(database.Validate().Any(p => p.Contains("não tem variantes")), "2★ com duas variantes em jogo é apontada");

			database = GameDatabase.FromJson(Read("stat_model.json"), new[] { slime }, Read("enemies.json"), "[]", "[]", "[]");
			Assert.False(database.Validate().Any(p => p.Contains("slime")), "o arquivo de verdade passa");
		}
	}
}
