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

	}
}
