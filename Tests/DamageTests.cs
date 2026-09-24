using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	internal static class DamageTests
	{
		[Test]
		private static void DefenseHalvesDamageAtTheConstant()
		{
			var attacker = TestData.Unit("a", Side.Allies, element: Element.Light);
			var target = TestData.Unit("b", Side.Enemies, defense: BattleRules.DefenseConstant, element: Element.Light);
			Assert.Near(50, DamageFormula.Compute(attacker, target, 1, 0, false), "dano com DEF = K");
		}

		[Test]
		private static void DefenseFollowsTheSummonersWarCurve()
		{
			// Summoners War: 1000 / (1140 + 3,5 × DEF). A nossa é a mesma curva com Defesa 0 = golpe cheio.
			var attacker = TestData.Unit("a", Side.Allies, attack: 1000, element: Element.Light);
			foreach (var defense in new[] { 300.0, 700, 1500 })
			{
				var target = TestData.Unit("b", Side.Enemies, defense: defense, element: Element.Light);
				var summonersWar = 1000 * 1000 / (1140 + 3.5 * defense);
				Assert.Near(summonersWar * 1.14, DamageFormula.Compute(attacker, target, 1, 0, false), $"DEF {defense}", 1);
			}
		}

		[Test]
		private static void ResistanceHasAFloorOfFifteenPercent()
		{
			Assert.Near(0.15, BattleRules.ResistChance(new StatBlock(), new StatBlock { Accuracy = 0.5 }), "sem Resistência ainda barra 15%");
			Assert.Near(0.60, BattleRules.ResistChance(new StatBlock { Resistance = 0.85 }, new StatBlock { Accuracy = 0.25 }), "85% − 25%");
			Assert.Near(0.90, BattleRules.ResistChance(new StatBlock { Resistance = 1.3 }, new StatBlock { Accuracy = 0.1 }), "Resistência para em 100%");
		}

		[Test]
		private static void ElementAdvantageAndDisadvantage()
		{
			Assert.Near(1.25, ElementChart.Multiplier(Element.Fire, Element.Wind), "Fogo vence Vento");
			Assert.Near(1.25, ElementChart.Multiplier(Element.Wind, Element.Water), "Vento vence Água");
			Assert.Near(1.25, ElementChart.Multiplier(Element.Water, Element.Fire), "Água vence Fogo");
			Assert.Near(0.75, ElementChart.Multiplier(Element.Wind, Element.Fire), "Vento perde para Fogo");
			Assert.Near(1.25, ElementChart.Multiplier(Element.Light, Element.Dark), "Luz vence Trevas");
			Assert.Near(1.25, ElementChart.Multiplier(Element.Dark, Element.Light), "Trevas vence Luz");
			Assert.Near(1, ElementChart.Multiplier(Element.Fire, Element.Light), "Fogo contra Luz é neutro");
		}

		[Test]
		private static void CritAndCurseMultiply()
		{
			var attacker = TestData.Unit("a", Side.Allies, element: Element.Light);
			var target = TestData.Unit("b", Side.Enemies, element: Element.Light);
			Assert.Near(150, DamageFormula.Compute(attacker, target, 1, 0, true), "crítico: 1 + Dano crítico de 50%");

			target.AddStatus(new StatusEffect(StatusKind.Curse, 2));
			Assert.Near(125, DamageFormula.Compute(attacker, target, 1, 0, false), "Maldição");
		}

		[Test]
		private static void ShieldAbsorbsBeforeHealth()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 200);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			new EffectResolver(session).GiveShield(foe, 60, 2);

			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Near(960, foe.Health, "Vida depois de 100 de dano contra escudo de 60");
			Assert.False(foe.Has(StatusKind.Shield), "escudo gasto some");
		}
	}
}
