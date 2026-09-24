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
			var session = TestData.Session(new[] { slow }, new[] { fast });
			session.Start();

			Assert.Equal(fast, session.BeginTurn().Actor, "primeiro a agir");
		}

		[Test]
		private static void DoubleSpeedActsTwiceAsOften()
		{
			var normal = TestData.Unit("normal", Side.Allies, speed: 100);
			var fast = TestData.Unit("dobro", Side.Enemies, speed: 200, health: 1_000_000);
			var session = TestData.Session(new[] { normal }, new[] { fast });

			var order = session.PredictOrder(12);
			Assert.Equal(8, order.Count(t => t == fast), "turnos do rápido em 12");
			Assert.Equal(4, order.Count(t => t == normal), "turnos do normal em 12");
		}

		[Test]
		private static void PushingImpetoMovesUnitUpTheOrder()
		{
			var ally = TestData.Unit("aliado", Side.Allies, speed: 100);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 110);
			var session = TestData.Session(new[] { ally }, new[] { foe });

			Assert.Equal(foe, session.PredictOrder(1)[0], "sem empurrão, o inimigo age antes");
			ally.Impeto = 50;
			Assert.Equal(ally, session.PredictOrder(1)[0], "com +50% de Ímpeto, o aliado passa na frente");
		}

		[Test]
		private static void SpeedHundredActsOncePerRound()
		{
			var ally = TestData.Unit("aliado", Side.Allies, speed: 100, health: 1_000_000, attack: 1);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 50, health: 1_000_000, attack: 1);
			var session = TestData.Session(new[] { ally }, new[] { foe });
			session.Start();

			var allyTurns = 0;
			while (true)
			{
				var turn = session.BeginTurn();
				if (session.Round > 5)
					break;
				if (turn.Actor == ally)
					allyTurns++;
				session.Act(new UnitAction(SkillSlot.Basic, false, null));
			}

			Assert.Equal(5, allyTurns, "turnos de Velocidade 100 em 5 rodadas");
		}

		[Test]
		private static void BattleIsLostAfterRoundLimit()
		{
			var ally = TestData.Unit("aliado", Side.Allies, health: 1_000_000, attack: 1);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1);
			var session = TestData.Session(new[] { ally }, new[] { foe });

			var victory = AutoBattle.Run(session);
			Assert.False(victory, "ninguém cai: a luta acaba em derrota");
			Assert.True(session.Time >= BattleRules.RoundLimit, $"acabou no tempo {session.Time}");
		}
	}
}
