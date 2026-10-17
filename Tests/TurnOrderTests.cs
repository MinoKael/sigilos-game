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
