using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>
	/// A camada de parceiro das Passivas (efeitos a mais, sono que não acorda, revide conjunto), a Passiva Guarda
	/// (obriga quem tem Defesa menor a mirar o dono) e o efeito Aumentar recarga.
	/// </summary>
	internal static class PartnerPassiveTests
	{
		private static SkillDefinition Skill(params EffectDefinition[] effects) => new() { Name = "teste", Effects = effects };

		private static EffectDefinition Status(StatusKind status, int turns = 2) =>
			new() { Kind = EffectKind.Status, Status = status, Target = TargetKind.Target, Chance = 1, Turns = turns };

		private static BattleSession Fight(BattleUnit[] allies, BattleUnit[] foes)
		{
			var session = TestData.Session(allies, foes);
			session.Start();
			return session;
		}

		[Test]
		private static void IncreaseCooldownDelaysEverySkillIncludingReadyOnes()
		{
			var raise = Skill(new EffectDefinition { Kind = EffectKind.IncreaseCooldown, Target = TargetKind.Target, Chance = 1, Turns = 1 });
			var hero = TestData.Unit("herói", Side.Allies, speed: 200, basic: raise);
			var foe = TestData.Unit("alvo", Side.Enemies, health: 1e9, attack: 1, speed: 1, special: Skill(), basic: Skill());
			var ready = TestData.Unit("pronto", Side.Enemies, health: 1e9, attack: 1, speed: 1, special: Skill(), basic: Skill());
			var boss = TestData.Unit("chefe", Side.Enemies, health: 1e9, attack: 1, speed: 1, special: Skill(), basic: Skill(), boss: true);
			var session = Fight(new[] { hero }, new[] { foe, ready, boss });
			foe.SetCooldown(1, 2);
			boss.SetCooldown(1, 2);

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			Assert.Equal(3, foe.Cooldown(1), "a que estava em recarga ganha um turno");
			Assert.Equal(0, foe.Cooldown(0), "a básica não tem recarga");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, ready));
			Assert.Equal(1, ready.Cooldown(1), "a pronta passa de 0 para 1");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, boss));
			Assert.Equal(2, boss.Cooldown(1), "o chefe é imune");
		}

		[Test]
		private static void GuardForcesOnlyEnemiesWithLowerDefenseWhileItStands()
		{
			var guard = new PassiveDefinition { Kind = PassiveKind.DefenseGuard };
			var weak = TestData.Unit("fraco", Side.Allies, defense: 10);
			var strong = TestData.Unit("forte", Side.Allies, defense: 500);
			var tank = TestData.Unit("guarda", Side.Enemies, defense: 100, passive: guard);
			var other = TestData.Unit("outro", Side.Enemies, defense: 0);
			var session = Fight(new[] { weak, strong }, new[] { tank, other });
			var foes = new[] { tank, other };

			Assert.Equal("guarda", string.Join(",", Targeting.Choosable(weak, foes).Select(u => u.Name)), "Defesa menor: só o guarda");
			Assert.Equal(2, Targeting.Choosable(strong, foes).Count, "Defesa maior: escolhe livre");

			tank.Health = 0;
			Assert.Equal("outro", string.Join(",", Targeting.Choosable(weak, foes).Select(u => u.Name)), "caído, não obriga mais");
			Assert.True(session != null, "a luta existe");
		}

		[Test]
		private static void PartnerKeepsTargetsAsleepOnlyWhileItStands()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.BonusVsStatusOrEffect, Value = 0.5, Statuses = new[] { StatusKind.Sleep }, Partner = "irmao", PartnerKeepsAsleep = true };
			var hero = TestData.Unit("herói", Side.Allies, speed: 200, passive: passive);
			var brother = TestData.Unit("irmao", Side.Allies, speed: 1);
			var foe = TestData.Unit("alvo", Side.Enemies, health: 1e9, attack: 1, speed: 1);
			var session = Fight(new[] { hero, brother }, new[] { foe });

			foe.AddStatus(new StatusEffect(StatusKind.Sleep, 5));
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			Assert.True(foe.Has(StatusKind.Sleep), "com o irmão vivo, o golpe não acorda");

			brother.Health = 0;
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			Assert.False(foe.Has(StatusKind.Sleep), "sem o irmão, o golpe acorda");
		}

		[Test]
		private static void ForgettingTheOwnerSilencesThePartnerPerks()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.BonusVsStatusOrEffect, Value = 0.5, Statuses = new[] { StatusKind.Sleep }, Partner = "irmao", PartnerKeepsAsleep = true };
			var hero = TestData.Unit("herói", Side.Allies, speed: 200, passive: passive);
			var brother = TestData.Unit("irmao", Side.Allies, speed: 1);
			var foe = TestData.Unit("alvo", Side.Enemies, health: 1e9, attack: 1, speed: 1);
			var session = Fight(new[] { hero, brother }, new[] { foe });

			hero.AddStatus(new StatusEffect(StatusKind.Oblivion, 5));
			foe.AddStatus(new StatusEffect(StatusKind.Sleep, 5));
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			Assert.False(foe.Has(StatusKind.Sleep), "com Esquecimento, a Passiva e o parceiro calam: o golpe acorda");
		}

		[Test]
		private static void PartnerEffectsFireAlongsideThePassiveOnlyWithThePartner()
		{
			var passive = new PassiveDefinition
			{
				Kind = PassiveKind.StatusOrEffectOnHit,
				Effects = new[] { Status(StatusKind.Oblivion) },
				PartnerEffects = new[] { Status(StatusKind.Karma) },
				Partner = "irmao",
			};
			var withBrother = RunHit(passive, brotherPresent: true);
			Assert.True(withBrother.Has(StatusKind.Oblivion), "o efeito da Passiva pega");
			Assert.True(withBrother.Has(StatusKind.Karma), "com o irmão, o efeito a mais também");

			var alone = RunHit(passive, brotherPresent: false);
			Assert.True(alone.Has(StatusKind.Oblivion), "sozinho, só o da Passiva");
			Assert.False(alone.Has(StatusKind.Karma), "e o a mais não vem");
		}

		private static BattleUnit RunHit(PassiveDefinition passive, bool brotherPresent)
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 200, passive: passive);
			var team = brotherPresent ? new[] { hero, TestData.Unit("irmao", Side.Allies, speed: 1) } : new[] { hero };
			var foe = TestData.Unit("alvo", Side.Enemies, health: 1e9, attack: 1, speed: 1, resistance: 0);
			var session = Fight(team, new[] { foe });
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
			return foe;
		}

		[Test]
		private static void GuardAndPartnerCounterTogetherOnceWithoutALoop()
		{
			var guard = new PassiveDefinition { Kind = PassiveKind.DefenseGuard, Partner = "irmao" };
			var tank = TestData.Unit("guarda", Side.Allies, speed: 1, health: 1e9, defense: 100, passive: guard);
			var brother = TestData.Unit("irmao", Side.Allies, speed: 1, health: 1e9);
			var foe = TestData.Unit("agressor", Side.Enemies, speed: 200, health: 1e9, attack: 10, defense: 0);
			var session = Fight(new[] { tank, brother }, new[] { foe });

			var turn = session.BeginTurn();
			Assert.Equal(foe, turn.Actor, "o agressor é o mais rápido");
			var events = session.Act(new UnitAction(0, null));
			Assert.Equal(2, events.OfType<Counterattack>().Count(), "o guarda e o irmão revidam, uma vez cada");
		}
	}
}
