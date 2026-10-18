using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Battle.Passives;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	internal static class StatusTests
	{
		/// <summary>Põe o efeito direto, sem sorteio: a Resistência mínima de 15% não entra no teste.</summary>
		private static void Give(BattleUnit caster, BattleUnit target, StatusKind status, int turns = 1) =>
			target.AddStatus(new StatusEffect(status, turns, 0, caster));

		[Test]
		private static void TauntForcesTheTarget()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var a = TestData.Unit("a", Side.Enemies);
			var b = TestData.Unit("b", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { a, b });

			Give(b, b, StatusKind.Taunt);
			var choosable = session.ChoosableTargets(hero);
			Assert.Equal(1, choosable.Count, "com um inimigo Provocando, o herói tem um alvo só");
			Assert.Equal(b, choosable[0], "quem tem Provocar");
			Assert.False(BattleRules.IsNegative(StatusKind.Taunt), "Provocar é um efeito positivo");
		}

		[Test]
		private static void StunSkipsOneTurn()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			Give(foe, hero, StatusKind.Stun);

			var turn = session.BeginTurn();
			Assert.Equal(hero, turn.Actor, "o herói é o mais rápido");
			Assert.False(turn.NeedsDecision, "atordoado não decide");
			Assert.False(hero.Has(StatusKind.Stun), "o atordoamento de 1 turno acaba no turno perdido");

			turn = session.BeginTurn();
			Assert.True(turn.NeedsDecision, "no turno seguinte age normalmente");
		}

		[Test]
		private static void BombCutShortExplodesAtOnceAndShowsTheCut()
		{
			// Ignição Maestral: a cada Bomba posta, a contagem dela cai 2 (a de 2 turnos chega a 0 e explode na hora).
			var passive = new PassiveDefinition
			{
				Kind = PassiveKind.ForEachStatusOrEffectAppliedDo,
				Statuses = new[] { StatusKind.Bomb },
				Effects = new[] { new EffectDefinition { Kind = EffectKind.ChangeDuration, Target = TargetKind.Target, Scope = StatusScope.Debuffs, OnlyStatus = StatusKind.Bomb, Chance = 1, Turns = -2 } },
			};
			var bomb = new SkillDefinition { Name = "bomba", Cooldown = 4, Effects = new[] { new EffectDefinition { Kind = EffectKind.Status, Status = StatusKind.Bomb, Target = TargetKind.Target, Chance = 1, Turns = 2 } } };
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, attack: 100, special: bomb, passive: passive);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);

			var events = session.Act(new UnitAction(1, foe));
			Assert.True(events.OfType<DurationChanged>().Any(e => e.Status == StatusKind.Bomb && e.Turns == -2), "o corte da contagem aparece");
			Assert.True(foe.Health < foe.MaxHealth, "a Bomba explodiu na hora");
			Assert.False(foe.Has(StatusKind.Bomb), "e saiu");
			Assert.True(foe.Has(StatusKind.Stun), "quem sobrevive fica atordoado");
		}

		[Test]
		private static void BombTriggerDetonatesEveryBombOfTheTargetAtOnce()
		{
			var bomb = new EffectDefinition { Kind = EffectKind.Status, Status = StatusKind.Bomb, Target = TargetKind.Target, Chance = 1, Turns = 3 };
			var skill = new SkillDefinition { Name = "ignição", Cooldown = 4, Effects = new[] { bomb, new EffectDefinition { Kind = EffectKind.BombTrigger, Target = TargetKind.Target } } };
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, attack: 100, special: skill);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);

			var events = session.Act(new UnitAction(1, foe));
			Assert.True(foe.Health < foe.MaxHealth, "a Bomba explodiu na hora, sem esperar a contagem");
			Assert.False(foe.Has(StatusKind.Bomb), "e saiu");
			Assert.True(events.OfType<DurationChanged>().Any(e => e.Status == StatusKind.Bomb), "o ícone aparece");
		}
	}
}
