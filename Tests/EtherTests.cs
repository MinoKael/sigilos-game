using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	internal static class EtherTests
	{
		private static readonly SkillDefinition Enhanceable = TestData.Strike with
		{
			EnhanceCost = 2,
			EnhancedEffects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 2 } },
		};

		private static readonly SkillDefinition SpecialStrike = TestData.Strike with { Name = "Especial", Cooldown = 3 };

		[Test]
		private static void BasicGivesNothingSpecialGivesOne()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, special: SpecialStrike);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Equal(0, session.Ether, "Éter depois do básico");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Special, false, foe));
			Assert.Equal(1, session.Ether, "Éter depois da especial");
			Assert.Equal(2, hero.SpecialCooldown, "recarga 3 conta o turno de uso");
		}

		[Test]
		private static void KillGivesEtherAndEtherCapsAtTen()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, attack: 10_000, special: SpecialStrike with { Cooldown = 1 });
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 10);
			var other = TestData.Unit("outro", Side.Enemies, health: 1_000_000_000, attack: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe, other });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Equal(1, session.Ether, "+1 pela queda, nada pelo básico");

			for (var i = 0; i < 20 && !session.IsOver; i++)
			{
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(SkillSlot.Special, false, other));
			}

			Assert.Equal(BattleRules.MaxEther, session.Ether, "Éter não passa de 10");
		}

		[Test]
		private static void EnhancementCostsEtherAndChangesEffects()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Enhanceable, special: SpecialStrike with { Cooldown = 1 });
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, true, foe));
			Assert.Near(1_000_000 - 100, foe.Health, "sem Éter, o aprimoramento não acontece");

			for (var i = 0; i < 2; i++)
			{
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(SkillSlot.Special, false, foe));
			}

			Assert.Equal(2, session.Ether, "duas especiais");
			TestData.RunUntilTurnOf(session, hero);
			var before = foe.Health;
			session.Act(new UnitAction(SkillSlot.Basic, true, foe));
			Assert.Near(before - 200, foe.Health, "aprimorado, o dano dobra");
			Assert.Equal(0, session.Ether, "pagou 2, o básico não devolve nada");
		}

		[Test]
		private static void AutoPilotNeverSpendsEther()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Enhanceable);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			Assert.False(AutoPilot.ForAlly(session, hero).Enhance, "o automático não aprimora");
		}
	}
}
