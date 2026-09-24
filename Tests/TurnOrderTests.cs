using System.Linq;
using Sigilos.Core.Battle;

namespace Sigilos.Tests
{
	internal static class TurnOrderTests
	{
		[Test]
		private static void FasterUnitActsFirst()
		{
			var slow = TestData.Unit("lento", Side.Allies, speed: 90);
			var fast = TestData.Unit("rápido", Side.Enemies, speed: 130);
			var session = TestData.Session(new[] { slow }, new[] { fast }, conjurerSpeed: 50);
			session.Start();

			Assert.Equal<object>(fast, session.BeginTurn().Actor, "primeiro a agir");
		}

		[Test]
		private static void DoubleSpeedActsTwiceAsOften()
		{
			var normal = TestData.Unit("normal", Side.Allies, speed: 100);
			var fast = TestData.Unit("dobro", Side.Enemies, speed: 200, health: 1_000_000);
			var session = TestData.Session(new[] { normal }, new[] { fast }, conjurerSpeed: 100);

			var order = session.PredictOrder(12);
			Assert.Equal(6, order.Count(t => ReferenceEquals(t, fast)), "turnos do rápido em 12");
			Assert.Equal(3, order.Count(t => ReferenceEquals(t, normal)), "turnos do normal em 12");
		}

		[Test]
		private static void PushingImpetoMovesUnitUpTheOrder()
		{
			var ally = TestData.Unit("aliado", Side.Allies, speed: 100);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 110);
			var session = TestData.Session(new[] { ally }, new[] { foe }, conjurerSpeed: 50);

			Assert.Equal<object>(foe, session.PredictOrder(1)[0], "sem empurrão, o inimigo age antes");
			ally.Impeto = 50;
			Assert.Equal<object>(ally, session.PredictOrder(1)[0], "com +50% de Ímpeto, o aliado passa na frente");
		}

		[Test]
		private static void ConjurerActsOncePerRound()
		{
			var ally = TestData.Unit("aliado", Side.Allies, speed: 100, health: 1_000_000, attack: 1);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 100, health: 1_000_000, attack: 1);
			var session = TestData.Session(new[] { ally }, new[] { foe });
			session.Start();

			var conjurerTurns = 0;
			while (session.Round <= 5)
			{
				var turn = session.BeginTurn();
				if (turn.Actor is ConjurerSeat)
				{
					conjurerTurns++;
					session.Act(ConjurerAction.Channel);
				}
				else
				{
					session.Act(new UnitAction(SkillSlot.Basic, false, null));
				}
			}

			Assert.Equal(5, conjurerTurns, "turnos do Conjurador em 5 rodadas");
		}

		[Test]
		private static void BattleIsLostAfterRoundLimit()
		{
			var ally = TestData.Unit("aliado", Side.Allies, health: 1_000_000, attack: 1);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1);
			var session = TestData.Session(new[] { ally }, new[] { foe });

			var victory = AutoBattle.Run(session, Posture.Balanced);
			Assert.False(victory, "ninguém cai: a luta acaba em derrota");
			Assert.True(session.Time >= BattleRules.RoundLimit, $"acabou no tempo {session.Time}");
		}
	}
}
