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
		private static void ShieldAbsorbsBeforeHealth()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 200);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			new EffectResolver(session).GiveShield(foe, 60, 2);

			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			Assert.Near(960, foe.Health, "Vida depois de 100 de dano contra escudo de 60");
			Assert.False(foe.Has(StatusKind.Shield), "escudo gasto some");
		}
	}
}
