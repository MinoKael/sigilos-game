using System.Collections.Generic;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	internal static class EtherTests
	{
		private static readonly SkillDefinition Enhanceable = TestData.Strike with
		{
			EnhanceCost = 1,
			EnhancedEffects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 2 } },
		};

		private static readonly SkillDefinition GlyphStrike = TestData.Strike with { Name = "Glifo", Cooldown = 3 };

		[Test]
		private static void BasicGivesOneGlyphGivesTwo()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, glyphSkill: GlyphStrike);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Equal(1, session.Ether, "Éter depois do básico");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Glyph, false, foe));
			Assert.Equal(3, session.Ether, "Éter depois do Glifo");
			Assert.Equal(2, hero.GlyphCooldown, "recarga 3 conta o turno de uso");
		}

		[Test]
		private static void EnhancementCostsEtherAndChangesEffects()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Enhanceable);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, true, foe));
			Assert.Near(1_000_000 - 100, foe.Health, "sem Éter, o aprimoramento não acontece");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, true, foe));
			Assert.Near(1_000_000 - 100 - 200, foe.Health, "com 1 Éter, o dano dobra");
			Assert.Equal(1, session.Ether, "paga 1 e ganha 1");
		}

		[Test]
		private static void KillGivesEtherAndEtherCapsAtTen()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, attack: 10_000);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 10);
			var other = TestData.Unit("outro", Side.Enemies, health: 1_000_000, attack: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe, other });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Equal(2, session.Ether, "+1 pela queda, +1 pelo básico");

			for (var i = 0; i < 20 && !session.IsOver; i++)
			{
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(SkillSlot.Basic, false, other));
			}

			Assert.Equal(BattleRules.MaxEther, session.Ether, "Éter não passa de 10");
		}

		[Test]
		private static void PageCostFollowsFormula()
		{
			var erudito = TestData.Conjurer with { CircleDiscounts = new Dictionary<int, int> { [2] = 1 } };
			PageDefinition Page(int circle, Form form) => new() { Id = "p", Glyph = Glyph.Shard, Circle = circle, Form = form };

			Assert.Equal(1, PageFormula.Cost(Page(1, Form.Single), erudito, 1), "Círculo I Único");
			Assert.Equal(4, PageFormula.Cost(Page(2, Form.All), erudito, 1), "Círculo II Todos, com desconto do Erudito");
			Assert.Equal(8, PageFormula.Cost(Page(3, Form.All), erudito, 1), "Círculo III Todos");
			Assert.Equal(7, PageFormula.Cost(Page(3, Form.All), erudito, 2), "Ressonância: dois do mesmo Glifo tiram 1");
			Assert.Equal(1, PageFormula.Cost(Page(1, Form.Single), erudito, 2), "custo mínimo 1");
		}

		[Test]
		private static void ConjurerChannelsOrCastsPage()
		{
			var page = new PageDefinition { Id = "p", Name = "Dardo", Glyph = Glyph.Shard, Form = Form.Single, Circle = 1 };
			var slot = new PageSlot(page, 1, true, PageFormula.Effects(page));
			var hero = TestData.Unit("herói", Side.Allies, speed: 10);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, speed: 10);
			var session = TestData.Session(new[] { hero }, new[] { foe }, new[] { slot });
			session.Start();

			TestData.RunUntilTurnOf(session, session.Conjurer);
			session.Act(new ConjurerAction(slot, foe));
			Assert.Equal(2, session.Ether, "sem Éter, a página vira canalização");

			TestData.RunUntilTurnOf(session, session.Conjurer);
			session.Act(new ConjurerAction(slot, foe));
			Assert.Equal(1, session.Ether, "página de custo 1 lançada");
			Assert.Near(1_000_000 - 100, foe.Health, "Poder 100 × 100% contra Defesa 0");
		}

		[Test]
		private static void PageNeedsItsGlyphInTheTeam()
		{
			var page = new PageDefinition { Id = "p", Glyph = Glyph.Shard, Form = Form.Single, Circle = 1 };
			var mute = new PageSlot(page, 1, false, PageFormula.Effects(page));
			var session = TestData.Session(new[] { TestData.Unit("a", Side.Allies) }, new[] { TestData.Unit("b", Side.Enemies) }, new[] { mute });
			Assert.False(session.CanCast(mute), "página sem Ressonância não pode ser lançada");
		}
	}
}
