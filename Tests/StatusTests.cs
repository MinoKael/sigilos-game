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
			target.AddStatus(new StatusEffect(status, turns, 0, status == StatusKind.Taunt ? caster : null));

		[Test]
		private static void HiddenCannotBeChosen()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var a = TestData.Unit("a", Side.Enemies);
			var b = TestData.Unit("b", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { a, b });

			Give(a, a, StatusKind.Hidden);
			var choosable = session.ChoosableTargets(hero);
			Assert.Equal(1, choosable.Count, "só um alvo visível");
			Assert.Equal(b, choosable[0], "o visível");
		}

		[Test]
		private static void TauntForcesTheTarget()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var a = TestData.Unit("a", Side.Enemies);
			var b = TestData.Unit("b", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { a, b });

			Give(b, hero, StatusKind.Taunt);
			var choosable = session.ChoosableTargets(hero);
			Assert.Equal(1, choosable.Count, "provocado tem um alvo só");
			Assert.Equal(b, choosable[0], "quem provocou");
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
			session.Act(new UnitAction(0, null));
			Assert.True(hero.Has(StatusKind.Hidden), "Oculto logo depois de usar");

			TestData.RunUntilTurnOf(session, hero);
			Assert.True(hero.Has(StatusKind.Hidden), "ainda Oculto no começo do turno seguinte");
			session.Act(new UnitAction(0, null));
		}

		[Test]
		private static void WardCancelsOneHit()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300);
			var foe = TestData.Unit("inimigo", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			Give(foe, foe, StatusKind.Aegis);

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			Assert.Near(1000, foe.Health, "primeiro golpe anulado");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			Assert.Near(900, foe.Health, "segundo golpe passa");
		}

		[Test]
		private static void DefenseBreakCutsDefense()
		{
			var hero = TestData.Unit("herói", Side.Allies, element: Element.Light);
			var foe = TestData.Unit("inimigo", Side.Enemies, defense: BattleRules.DefenseConstant, element: Element.Light);
			Assert.Near(50, DamageFormula.Compute(hero, foe, 1, 0, false), "com a Defesa inteira");

			Give(hero, foe, StatusKind.DefenseBreak, 2);
			Assert.Near(BattleRules.DefenseConstant * (1 - BattleRules.DefenseDownPenalty), foe.Defense, "a Defesa cai", 1e-9);
			Assert.Near(77, DamageFormula.Compute(hero, foe, 1, 0, false), "e o mesmo golpe tira mais");
			Assert.True(BattleRules.IsNegative(StatusKind.DefenseBreak), "é efeito negativo");
		}

		[Test]
		private static void PoisonStacksAndBurnDoesNot()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 50, attack: 1);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, health: 1000, attack: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();

			var resolver = new EffectResolver(session);
			for (var i = 0; i < 3; i++)
				resolver.ApplyStatus(hero, foe, StatusKind.Affliction, 1, 2, resistible: false);
			Assert.Equal(3, foe.Count(StatusKind.Affliction), "a Aflição acumula");

			var turn = session.BeginTurn();
			Assert.Equal(foe, turn.Actor, "o inimigo é o mais rápido");
			Assert.Near(1000 - 3 * Math.Round(1000 * BattleRules.AfflictionFraction), foe.Health, "cada cópia tira a sua fração no começo do turno");

			for (var i = 0; i < BattleRules.MaxStatuses + 2; i++)
				resolver.ApplyStatus(hero, foe, StatusKind.Affliction, 1, 2, resistible: false);
			Assert.Equal(BattleRules.MaxStatuses, foe.Statuses.Count, "acumula até o limite de efeitos do monstro");
			resolver.ApplyStatus(hero, foe, StatusKind.Stun, 1, 1, resistible: false);
			Assert.False(foe.Has(StatusKind.Stun), "cheio, um efeito novo não pega");
		}

		[Test]
		private static void BombExplodesOnTheNextTurnIgnoringDefense()
		{
			var hero = TestData.Unit("herói", Side.Allies, attack: 100);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, health: 10_000, attack: 1, defense: BattleRules.DefenseConstant);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			foe.AddStatus(new StatusEffect(StatusKind.Bomb, 1, 0, hero));
			new EffectResolver(session).GiveShield(foe, 100, 3);

			var turn = session.BeginTurn();
			Assert.Equal(foe, turn.Actor, "o inimigo é o mais rápido");
			var blast = turn.Events.OfType<Damaged>().Single();
			Assert.Equal(150, blast.Amount, "250% do Ataque de quem pôs, sem Defesa, menos o escudo");
			Assert.Equal(100, blast.Absorbed, "o escudo absorve a explosão");
			Assert.False(blast.Crit, "a explosão nunca é crítica");
			Assert.Near(9850, foe.Health, "a Vida depois da explosão");
			Assert.False(foe.Has(StatusKind.Bomb), "a bomba some ao explodir");
			Assert.False(turn.NeedsDecision, "e o alvo, atordoado pela explosão, perde este turno");
			Assert.Equal(StatusKind.Stun, turn.Events.OfType<TurnSkipped>().Single().Cause, "pelo Atordoamento");
			Assert.False(foe.Has(StatusKind.Stun), "que acaba no fim deste turno");
		}

		[Test]
		private static void BombWaitsItsCountdownAndCurseMakesItWorse()
		{
			var hero = TestData.Unit("herói", Side.Allies, attack: 100);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, health: 10_000, attack: 1);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			foe.AddStatus(new StatusEffect(StatusKind.Bomb, 2, 0, hero));
			foe.AddStatus(new StatusEffect(StatusKind.Curse, 5));

			var first = session.BeginTurn();
			Assert.Equal(foe, first.Actor, "o inimigo é o mais rápido");
			Assert.Near(10_000, foe.Health, "com 2 turnos, o primeiro passa em branco");
			session.Act(new UnitAction(0, hero));

			var second = session.BeginTurn();
			Assert.Equal(foe, second.Actor, "Velocidade 300 age de novo antes do herói");
			Assert.Near(10_000 - Math.Round(250 * (1 + BattleRules.CurseBonus)), foe.Health, "explode no segundo, e a Maldição aumenta");
		}

		[Test]
		private static void KnightShieldsAlliesWhenFalling()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.ShieldOnDeath, Value = 0.15 };
			var knight = TestData.Unit("knight", Side.Allies, health: 1000, passive: passive);
			var friend = TestData.Unit("amigo", Side.Allies);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, attack: 10_000);
			var session = TestData.Session(new[] { knight, friend }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, foe);
			session.Act(new UnitAction(0, knight));
			Assert.False(knight.IsAlive, "o cavaleiro caiu");
			Assert.Near(150, friend.Find(StatusKind.Shield)?.Value ?? 0, "escudo de 15% da Vida do cavaleiro");
		}

		[Test]
		private static void ImpSpeedsUpOnlyWhenStrictlyLowest()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.SpeedWhenLowest, Value = 0.15 };
			var imp = TestData.Unit("imp", Side.Allies, speed: 100, passive: passive);
			var friend = TestData.Unit("amigo", Side.Allies);
			TestData.Session(new[] { imp, friend }, new[] { TestData.Unit("inimigo", Side.Enemies) });

			Assert.Near(100, imp.TurnSpeed, "todos com Vida cheia: empate não conta");
			imp.Health = 500;
			Assert.Near(115, imp.TurnSpeed, "o mais ferido ganha 15%");
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
			session.Act(new UnitAction(0, phoenix));
			Assert.True(phoenix.Reviving, "caída, esperando renascer");

			while (!phoenix.IsAlive && !session.IsOver)
			{
				var turn = session.BeginTurn();
				if (turn.NeedsDecision)
					session.Act(new UnitAction(0, turn.Actor.Side == Side.Enemies ? friend : foe));
			}

			Assert.Near(400, phoenix.Health, "renasce com 40% da Vida");
			Assert.False(phoenix.Rules().Any(rule => rule.Behavior is RebirthOncePassive), "renascimento gasto");
		}
	}
}
