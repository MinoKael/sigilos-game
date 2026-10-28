using System.IO;
using System.Linq;
using System.Text.Json;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>A forma desperta da habilidade sai dos campos "awakened" dos efeitos, sem trocar a lista inteira.</summary>
	internal static class AwakenedFormTests
	{
		private static SkillDefinition Parse(string effects) =>
			JsonSerializer.Deserialize<SkillDefinition>($"{{\"name\": \"x\", \"cooldown\": 3, \"effects\": {effects}}}", GameDatabase.JsonOptions)!;

		[Test]
		private static void AwakenedChangesOnlyTheValuesNamed()
		{
			var skill = Parse("[{\"kind\": \"Damage\", \"target\": \"AllEnemies\", \"power\": 1.96, \"awakened\": {\"power\": 2.94, \"ignoreDefense\": 0.3}}, {\"kind\": \"Impeto\", \"target\": \"AllAllies\", \"power\": 20}]");
			Assert.Equal(2, skill.Effects.Count, "a forma normal tem os dois efeitos");
			Assert.Near(1.96, skill.Effects[0].Power, "normal");
			Assert.Near(0, skill.Effects[0].IgnoreDefense, "normal sem Ignorar Defesa");
			Assert.Near(2.94, skill.AwakenedEffects[0].Power, "desperto troca o power");
			Assert.Near(0.3, skill.AwakenedEffects[0].IgnoreDefense, "e soma Ignorar Defesa");
			Assert.Equal(TargetKind.AllEnemies, skill.AwakenedEffects[0].Target, "o resto do efeito fica");
			Assert.Near(20, skill.AwakenedEffects[1].Power, "o efeito que não muda continua igual");
			Assert.True(skill.ChangesOnAwakening, "a habilidade muda no Despertar");
		}

		[Test]
		private static void OnlyAwakenedAndLostAfterAwakenShapeTheTwoForms()
		{
			var skill = Parse("[{\"kind\": \"Damage\", \"target\": \"Target\", \"power\": 1, \"lostAfterAwaken\": true}, {\"kind\": \"Heal\", \"target\": \"Self\", \"power\": 0.1}, {\"kind\": \"Cleanse\", \"target\": \"Self\", \"onlyAwakened\": true}]");
			Assert.Equal("Damage,Heal", string.Join(",", skill.Effects.Select(e => e.Kind)), "normal: sem o que só existe desperto");
			Assert.Equal("Heal,Cleanse", string.Join(",", skill.AwakenedEffects.Select(e => e.Kind)), "desperto: sem o que sai, com o que entra");
		}

		[Test]
		private static void AwakenedWithoutMarksLeavesNoAwakenedForm()
		{
			var skill = Parse("[{\"kind\": \"Damage\", \"target\": \"Target\", \"power\": 1}]");
			Assert.Equal(0, skill.AwakenedEffects.Count, "sem marcas, o Despertar não muda a habilidade");
			Assert.False(skill.ChangesOnAwakening, "não muda");
		}

		[Test]
		private static void AwakenedRejectsKindAndUnknownFields()
		{
			var skill = Parse("[{\"kind\": \"Damage\", \"target\": \"Target\", \"power\": 1, \"awakened\": {\"kind\": \"Heal\", \"poder\": 2}}]");
			Assert.Equal(2, GameDatabase.ValidateSkill(skill).Count(), "trocar o tipo e um campo desconhecido são erros");
		}

		[Test]
		private static void DataNoLongerUsesAwakenedEffectsLists()
		{
			var folder = Path.Combine(Program.ProjectRoot, "Data", "summons");
			var old = Directory.GetFiles(folder, "*.json").Where(file => File.ReadAllText(file).Contains("\"awakenedEffects\"")).Select(Path.GetFileName).ToList();
			Assert.Empty(old.Select(file => $"{file} ainda tem awakenedEffects (use \"awakened\" nos efeitos)"), "dados");
		}
	}
}
