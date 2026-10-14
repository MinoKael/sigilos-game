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

		private static PassiveDefinition GuardPassive(Comparison compare = Comparison.Greater) => new()
		{
			Kind = PassiveKind.Conditional,
			Condition = new PassiveCondition { Stat = Stat.Defense, Compare = compare, Against = ConditionAgainst.Opponent },
			Actions = new[] { PassiveAction.Force },
		};

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
			var guard = GuardPassive();
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
		private static void LessAndEqualConditionsCompareTheStatAgainstTheOpponent()
		{
			var less = GuardPassive(Comparison.Less);
			var tank = TestData.Unit("guarda", Side.Enemies, defense: 100, passive: less);
			var high = TestData.Unit("alto", Side.Allies, defense: 500);
			var low = TestData.Unit("baixo", Side.Allies, defense: 10);
			var other = TestData.Unit("outro", Side.Enemies, defense: 0);
			Fight(new[] { high, low }, new[] { tank, other });
			var foes = new[] { tank, other };

			Assert.Equal("guarda", string.Join(",", Targeting.Choosable(high, foes).Select(u => u.Name)), "Menor: obriga quem tem Defesa maior");
			Assert.Equal(2, Targeting.Choosable(low, foes).Count, "Menor: quem tem Defesa menor escolhe livre");

			var equal = TestData.Unit("igual", Side.Enemies, defense: 100, passive: GuardPassive(Comparison.Equal));
			var same = TestData.Unit("mesmo", Side.Allies, defense: 100);
			Fight(new[] { same, low }, new[] { equal, other });
			var pair = new[] { equal, other };
			Assert.Equal("igual", string.Join(",", Targeting.Choosable(same, pair).Select(u => u.Name)), "Igual: só a Defesa idêntica obriga");
			Assert.Equal(2, Targeting.Choosable(low, pair).Count, "Igual: Defesa diferente escolhe livre");
		}

		[Test]
		private static void StatusCountAndHealthFractionConditionsGateTheAction()
		{
			var marked = new PassiveDefinition
			{
				Kind = PassiveKind.Conditional,
				Condition = new PassiveCondition { Status = StatusKind.Oblivion, Of = ConditionSide.Opponent, Compare = Comparison.Greater, Value = 0 },
				Actions = new[] { PassiveAction.Force },
			};
			var tank = TestData.Unit("guarda", Side.Enemies, passive: marked);
			var other = TestData.Unit("outro", Side.Enemies);
			var forgetful = TestData.Unit("esquecido", Side.Allies);
			var clear = TestData.Unit("limpo", Side.Allies);
			Fight(new[] { forgetful, clear }, new[] { tank, other });
			forgetful.AddStatus(new StatusEffect(StatusKind.Oblivion, 3));
			var foes = new[] { tank, other };

			Assert.Equal("guarda", string.Join(",", Targeting.Choosable(forgetful, foes).Select(u => u.Name)), "com o efeito, obriga");
			Assert.Equal(2, Targeting.Choosable(clear, foes).Count, "sem o efeito, não obriga");

			var wounded = new PassiveDefinition
			{
				Kind = PassiveKind.Conditional,
				Condition = new PassiveCondition { Stat = Stat.Health, Compare = Comparison.Less, Against = ConditionAgainst.Value, Value = 0.5 },
				Actions = new[] { PassiveAction.Force },
			};
			var brave = TestData.Unit("bravo", Side.Enemies, health: 1000, passive: wounded);
			Fight(new[] { clear }, new[] { brave, other });
			Assert.Equal(2, Targeting.Choosable(clear, new[] { brave, other }).Count, "Vida cheia: não obriga");
			brave.Health = 400;
			Assert.Equal("bravo", string.Join(",", Targeting.Choosable(clear, new[] { brave, other }).Select(u => u.Name)), "abaixo da metade: obriga");
		}

		[Test]
		private static void OpponentConditionIsFalseWhenThereIsNoOtherSide()
		{
			var owner = TestData.Unit("dono", Side.Allies, defense: 100);
			var against = new PassiveCondition { Stat = Stat.Defense, Compare = Comparison.Greater };
			Assert.False(ConditionRules.Holds(against, owner, null), "sem o outro lado, a comparação não vale");
			Assert.True(ConditionRules.Holds(null, owner, null), "sem condição, vale sempre");

			var own = new PassiveCondition { Stat = Stat.Defense, Compare = Comparison.Greater, Against = ConditionAgainst.Value, Value = 50 };
			Assert.True(ConditionRules.Holds(own, owner, null), "o atributo do próprio dono contra um número vale sem outro lado");
		}

		[Test]
		private static void ForceStillYieldsToTaunt()
		{
			var tank = TestData.Unit("guarda", Side.Enemies, defense: 100, passive: GuardPassive());
			var taunter = TestData.Unit("provocador", Side.Enemies);
			var weak = TestData.Unit("fraco", Side.Allies, defense: 10);
			Fight(new[] { weak }, new[] { tank, taunter });
			taunter.AddStatus(new StatusEffect(StatusKind.Taunt, 3));

			Assert.Equal("provocador", string.Join(",", Targeting.Choosable(weak, new[] { tank, taunter }).Select(u => u.Name)), "a Provocação vem antes da guarda");
		}

		[Test]
		private static void PartnerTurnsExtendBuffsOnlyWhenTheScopeSaysSo()
		{
			int Duration(StatusScope? scope, bool partnerPresent)
			{
				var passive = new PassiveDefinition { Kind = PassiveKind.Conditional, Partner = "irmao", PartnerTurns = 2, PartnerScope = scope };
				var buff = Skill(new EffectDefinition { Kind = EffectKind.Status, Status = StatusKind.AttackUp, Target = TargetKind.Self, Chance = 1, Turns = 2 });
				var hero = TestData.Unit("herói", Side.Allies, speed: 200, passive: passive, basic: buff);
				var team = partnerPresent ? new[] { hero, TestData.Unit("irmao", Side.Allies, speed: 1) } : new[] { hero };
				var foe = TestData.Unit("alvo", Side.Enemies, health: 1e9, attack: 1, speed: 1);
				var session = Fight(team, new[] { foe });
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(0, foe));
				return hero.Find(StatusKind.AttackUp)!.Turns;
			}

			var baseline = Duration(StatusScope.Buffs, partnerPresent: false);
			Assert.Equal(baseline + 2, Duration(StatusScope.Buffs, partnerPresent: true), "com o parceiro, o positivo dura mais");
			Assert.Equal(baseline, Duration(null, partnerPresent: true), "o padrão são os negativos: o positivo fica igual");
			Assert.Equal(baseline + 2, Duration(StatusScope.All, partnerPresent: true), "Todos inclui os positivos");
		}

		[Test]
		private static void PartnerKeepsTargetsAsleepOnlyWhileItStands()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.BonusVsStatusOrEffect, Value = 0.5, Statuses = new[] { StatusKind.Sleep }, Partner = "irmao", PartnerActions = new[] { PassiveAction.KeepAsleep } };
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
			var passive = new PassiveDefinition { Kind = PassiveKind.BonusVsStatusOrEffect, Value = 0.5, Statuses = new[] { StatusKind.Sleep }, Partner = "irmao", PartnerActions = new[] { PassiveAction.KeepAsleep } };
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
			var guard = GuardPassive() with { Partner = "irmao", PartnerActions = new[] { PassiveAction.Counter } };
			var tank = TestData.Unit("guarda", Side.Allies, speed: 1, health: 1e9, defense: 100, passive: guard);
			var brother = TestData.Unit("irmao", Side.Allies, speed: 1, health: 1e9);
			var foe = TestData.Unit("agressor", Side.Enemies, speed: 200, health: 1e9, attack: 10, defense: 0);
			var session = Fight(new[] { tank, brother }, new[] { foe });

			var turn = session.BeginTurn();
			Assert.Equal(foe, turn.Actor, "o agressor é o mais rápido");
			var events = session.Act(new UnitAction(0, null));
			Assert.Equal(2, events.OfType<Counterattack>().Count(), "o guarda e o irmão revidam, uma vez cada");
		}

		[Test]
		private static void WolvesAndCrowsOfLightAndDarkarePairedPassives()
		{
			PassiveDefinition Of(string id) => TestData.Summon(id).Awakening.Skill!.Passive!;

			var guard = Of("wolf_light");
			Assert.Equal(PassiveKind.Conditional, guard.Kind, "Lobo de Luz: condicional");
			Assert.Equal(Stat.Defense, guard.Condition!.Stat, "a condição olha a Defesa");
			Assert.Equal(Comparison.Greater, guard.Condition.Compare, "maior que a do alvo");
			Assert.True(guard.Actions.Contains(PassiveAction.Force), "e obriga o alvo a mirar nele");
			Assert.True(guard.PartnerActions.Contains(PassiveAction.Counter), "com o irmão, revide conjunto");
			Assert.Equal("wolf_dark", guard.Partner, "e o irmão é o Lobo de Trevas");

			var sleep = Of("wolf_dark");
			Assert.Equal(PassiveKind.BonusPerStatusOrEffect, sleep.Kind, "Lobo de Trevas: mais dano");
			Assert.True(sleep.Statuses.SequenceEqual(new[] { StatusKind.Sleep }), "em quem dorme");
			Assert.True(sleep.PartnerActions.Contains(PassiveAction.KeepAsleep), "com o irmão, não acorda");
			Assert.Equal("wolf_light", sleep.Partner, "irmão: Lobo de Luz");

			var forget = TestData.Summon("crow_light").Awakening.Skill!;
			Assert.Equal(PassiveKind.StatusOrEffectOnHit, forget.Passive!.Kind, "Corvo de Luz: ao atingir");
			Assert.True(forget.Effects.Any(e => e.Status == StatusKind.Oblivion && e.Chance >= 1), "põe Esquecimento sempre");
			Assert.Equal("crow_dark", forget.Passive.Partner, "parceiro: Corvo de Trevas");
			Assert.True(forget.Passive.PartnerEffects.Any(e => e.Status == StatusKind.Karma), "com ele, põe Karma também");

			var debuffs = Of("crow_dark");
			Assert.Equal(StatusScope.Debuffs, debuffs.Scope, "Corvo de Trevas: conta só os negativos");
			Assert.Equal("crow_light", debuffs.Partner, "parceiro: Corvo de Luz");
			Assert.True(debuffs.PartnerTurns > 0, "com ele, os negativos duram mais");
		}

		[Test]
		private static void TheRealPairsFightWithTheirPassives()
		{
			var database = TestData.Database;
			var encounter = database.Stages.First().Encounter;

			var events = new System.Collections.Generic.List<BattleEvent>();
			AutoBattle.Run(BattleFactory.Create(database, BattleDigest.Team(database, new[] { "crow_light", "crow_dark" }, 6, 40, true, false), encounter, 7), events);
			Assert.True(events.OfType<StatusApplied>().Any(e => e.Status == StatusKind.Oblivion), "o Corvo de Luz põe Esquecimento nos que atinge");
			Assert.True(events.OfType<StatusApplied>().Any(e => e.Status == StatusKind.Karma), "e, com o irmão, Karma");

			events.Clear();
			AutoBattle.Run(BattleFactory.Create(database, BattleDigest.Team(database, new[] { "wolf_light", "wolf_dark" }, 6, 40, true, false), encounter, 7), events);
			Assert.True(events.OfType<Counterattack>().Any(), "os lobos revidam juntos quando o de Luz é atacado");
		}
	}
}
