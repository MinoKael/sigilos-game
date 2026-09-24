using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	internal static class StatusTests
	{
		private static void Give(BattleSession session, BattleUnit caster, BattleUnit target, StatusKind status, int turns = 1)
		{
			var effect = new EffectDefinition { Kind = EffectKind.Status, Target = target.Side == caster.Side ? TargetKind.Self : TargetKind.Target, Status = status, Turns = turns };
			new EffectResolver(session).Resolve(Caster.Of(target.Side == caster.Side ? target : caster), new[] { effect }, target);
		}

		[Test]
		private static void HiddenCannotBeChosen()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var a = TestData.Unit("a", Side.Enemies);
			var b = TestData.Unit("b", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { a, b });

			Give(session, a, a, StatusKind.Hidden);
			var choosable = session.ChoosableTargets(hero);
			Assert.Equal(1, choosable.Count, "só um alvo visível");
			Assert.Equal<object>(b, choosable[0], "o visível");
		}

		[Test]
		private static void TauntForcesTheTarget()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var a = TestData.Unit("a", Side.Enemies);
			var b = TestData.Unit("b", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { a, b });

			Give(session, b, hero, StatusKind.Taunt);
			var choosable = session.ChoosableTargets(hero);
			Assert.Equal(1, choosable.Count, "provocado tem um alvo só");
			Assert.Equal<object>(b, choosable[0], "quem provocou");
		}

		[Test]
		private static void StunSkipsOneTurn()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			Give(session, foe, hero, StatusKind.Stun);

			var turn = session.BeginTurn();
			Assert.Equal<object>(hero, turn.Actor, "o herói é o mais rápido");
			Assert.False(turn.NeedsDecision, "atordoado não decide");
			Assert.False(hero.Has(StatusKind.Stun), "o atordoamento de 1 turno acaba no turno perdido");

			turn = session.BeginTurn();
			Assert.True(turn.NeedsDecision, "no turno seguinte age normalmente");
		}

		[Test]
		private static void SelfBuffOnOwnTurnLastsThroughNextTurn()
		{
			var hide = TestData.Strike with
			{
				Effects = new[] { new EffectDefinition { Kind = EffectKind.Status, Target = TargetKind.Self, Status = StatusKind.Hidden } },
			};
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: hide);
			var foe = TestData.Unit("inimigo", Side.Enemies, attack: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, null));
			Assert.True(hero.Has(StatusKind.Hidden), "Oculto logo depois de usar");

			TestData.RunUntilTurnOf(session, hero);
			Assert.True(hero.Has(StatusKind.Hidden), "ainda Oculto no começo do turno seguinte");
			session.Act(new UnitAction(SkillSlot.Basic, false, null));
		}

		[Test]
		private static void WardCancelsOneHit()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300);
			var foe = TestData.Unit("inimigo", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			Give(session, foe, foe, StatusKind.Ward);

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Near(1000, foe.Health, "primeiro golpe anulado");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Near(900, foe.Health, "segundo golpe passa");
		}

		[Test]
		private static void KnightShieldsAlliesWhenFalling()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.ShieldOnDeath, Value = 0.15 };
			var knight = TestData.Unit("cavaleiro", Side.Allies, health: 1000, passive: passive);
			var friend = TestData.Unit("amigo", Side.Allies);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, attack: 10_000);
			var session = TestData.Session(new[] { knight, friend }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, foe);
			session.Act(new UnitAction(SkillSlot.Basic, false, knight));
			Assert.False(knight.IsAlive, "o cavaleiro caiu");
			Assert.Near(150, friend.Find(StatusKind.Shield)?.Value ?? 0, "escudo de 15% da Vida do cavaleiro");
		}

		[Test]
		private static void PhoenixRisesOnceOnItsNextTurn()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.RebirthOnce, Value = 0.4 };
			var phoenix = TestData.Unit("fênix", Side.Allies, health: 1000, passive: passive);
			var friend = TestData.Unit("amigo", Side.Allies, health: 1_000_000, attack: 1);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, attack: 10_000, health: 1_000_000);
			var session = TestData.Session(new[] { phoenix, friend }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, foe);
			session.Act(new UnitAction(SkillSlot.Basic, false, phoenix));
			Assert.True(phoenix.PendingRebirth, "caída, esperando renascer");

			while (!phoenix.IsAlive && !session.IsOver)
			{
				var turn = session.BeginTurn();
				if (turn.NeedsDecision && turn.Actor is BattleUnit unit)
					session.Act(new UnitAction(SkillSlot.Basic, false, unit.Side == Side.Enemies ? friend : foe));
				else if (turn.NeedsDecision)
					session.Act(ConjurerAction.Channel);
			}

			Assert.Near(400, phoenix.Health, "renasce com 40% da Vida");
			Assert.True(phoenix.RebirthUsed, "renascimento gasto");
		}
	}
}
