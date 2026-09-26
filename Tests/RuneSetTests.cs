using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>O que os conjuntos de runas fazem em combate.</summary>
	internal static class RuneSetTests
	{
		[Test]
		private static void VampireDrainsAndViolentGivesAnExtraTurn()
		{
			var vampire = TestData.Unit("vampiro", Side.Allies, speed: 300, health: 1000, runeEffects: TestData.Effects(drain: 0.5, extraTurn: 1));
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { vampire }, new[] { foe });
			session.Start();
			vampire.Health = 500;

			TestData.RunUntilTurnOf(session, vampire);
			var events = session.Act(new UnitAction(0, foe));
			Assert.Near(550, vampire.Health, "drena 50% de 100");
			Assert.True(events.OfType<ExtraTurn>().Any(), "100% de turno extra");
			Assert.Equal(vampire, session.BeginTurn().Actor, "age de novo em seguida");

			var extra = session.Act(new UnitAction(0, foe));
			Assert.False(extra.OfType<ExtraTurn>().Any(), "o turno extra não dá outro: um por turno");

			TestData.RunUntilTurnOf(session, vampire);
			var next = session.Act(new UnitAction(0, foe));
			Assert.True(next.OfType<ExtraTurn>().Any(), "o turno normal seguinte sorteia de novo");
		}

		[Test]
		private static void DespairStunsThroughResistance()
		{
			var jailer = TestData.Unit("carcereiro", Side.Allies, speed: 300, runeEffects: TestData.Effects(stun: 1));
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, resistance: 1);
			var session = TestData.Session(new[] { jailer }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, jailer);
			session.Act(new UnitAction(0, foe));
			Assert.True(foe.Has(StatusKind.Stun), "Desespero não passa pela Resistência");
		}

		[Test]
		private static void WillGivesImmunityAtTheStartOfEachWave()
		{
			var stubborn = TestData.Unit("teimoso", Side.Allies, health: 1_000_000, runeEffects: TestData.Effects(immunity: 1));
			var jailer = TestData.Unit("carcereiro", Side.Enemies, speed: 300, runeEffects: TestData.Effects(stun: 1));
			var session = TestData.Session(new[] { stubborn }, new[] { jailer });
			session.Start();
			Assert.True(stubborn.Has(StatusKind.Immunity), "Imune desde o começo");

			TestData.RunUntilTurnOf(session, jailer);
			var events = session.Act(new UnitAction(0, stubborn));
			Assert.True(events.OfType<Immune>().Any(), "a Imunidade barrou");
			Assert.False(stubborn.Has(StatusKind.Stun), "nem o Desespero passa");
		}

		[Test]
		private static void ShieldSetShieldsEveryAlly()
		{
			var guard = TestData.Unit("guarda", Side.Allies, runeEffects: TestData.Effects(shield: 300));
			var friend = TestData.Unit("amigo", Side.Allies, runeEffects: TestData.Effects(shield: 200));
			var session = TestData.Session(new[] { guard, friend }, new[] { TestData.Unit("inimigo", Side.Enemies) });
			session.Start();

			foreach (var ally in new[] { guard, friend })
			{
				Assert.Near(500, ally.Find(StatusKind.Shield)?.Value ?? 0, "os escudos dos donos somam");
				Assert.Equal(RuneSets.ShieldTurns, ally.Find(StatusKind.Shield)?.Turns ?? 0, "3 turnos");
			}
		}

		[Test]
		private static void RevengeCountersWithThreeQuartersOfTheBasic()
		{
			var avenger = TestData.Unit("vingador", Side.Allies, health: 1_000_000, runeEffects: TestData.Effects(counter: 1));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, health: 1000);
			var session = TestData.Session(new[] { avenger }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, foe);
			var events = session.Act(new UnitAction(0, avenger));
			Assert.True(events.OfType<Counterattack>().Any(), "contra-atacou");
			Assert.Near(925, foe.Health, "75% de um golpe de 100");
		}

		[Test]
		private static void NemesisFillsTheBarWhenHurt()
		{
			var nemesis = TestData.Unit("nêmesis", Side.Allies, health: 1000, runeEffects: TestData.Effects(nemesis: 0.04));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 300, attack: 150, health: 1_000_000);
			var session = TestData.Session(new[] { nemesis }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, foe);
			var events = session.Act(new UnitAction(0, nemesis));
			var gain = events.OfType<ImpetoChanged>().Single(e => e.Target == nemesis);
			Assert.Near(8, gain.Amount, "150 de dano = 2 × 7% da Vida: +8% de Ímpeto");
		}

		[Test]
		private static void DestroyTakesMaxHealth()
		{
			var destroyer = TestData.Unit("destruidor", Side.Allies, speed: 300, runeEffects: TestData.Effects(destroy: 0.04));
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1000);
			var session = TestData.Session(new[] { destroyer }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, destroyer);
			session.Act(new UnitAction(0, foe));
			Assert.Near(970, foe.MaxHealth, "30% de 100 de dano");
			Assert.Near(900, foe.Health, "a Vida atual não cai de novo");
		}
	}
}
