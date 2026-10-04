using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Tests
{
	/// <summary>
	/// As Passivas das famílias: as que vieram dos inimigos (Limos, Goblins, Lobos, Bandidos, Trolls e
	/// Dragões) e as de Magos, Paladinos, Druidas, Gárgulas, Vampiros, Corvos, Pássaros e Pixies.
	/// </summary>
	internal static class SignatureTests
	{
		private static PassiveDefinition Passive(PassiveKind kind, double value) => new() { Kind = kind, Value = value };

		/// <summary>Uma Passiva genérica que sempre dispara, com os efeitos dados.</summary>
		private static PassiveDefinition Generic(PassiveKind kind, params EffectDefinition[] effects) => new() { Kind = kind, Effects = effects };

		private static EffectDefinition Inflict(StatusKind status, int turns) => new() { Kind = EffectKind.Status, Status = status, Chance = 1, Turns = turns };

		private static double Hit(BattleUnit attacker, BattleUnit target) => DamageFormula.Compute(attacker, target, 1, 0, false);

		[Test]
		private static void SlimesTakeLessDamage()
		{
			var hero = TestData.Unit("herói", Side.Allies);
			var plain = TestData.Unit("comum", Side.Enemies);
			var slime = TestData.Unit("limo", Side.Enemies, passive: Passive(PassiveKind.DamageReduction, 0.2));
			Assert.Near(Hit(hero, plain) * 0.8, Hit(hero, slime), "20% a menos", 1);
		}

		[Test]
		private static void GoblinsHitDebuffedTargetsHarder()
		{
			var goblin = TestData.Unit("goblin", Side.Allies, passive: new PassiveDefinition { Kind = PassiveKind.BonusVsStatusOrEffect, Value = 0.3, Scope = StatusScope.Debuffs });
			var target = TestData.Unit("alvo", Side.Enemies);
			var clean = Hit(goblin, target);
			target.AddStatus(new StatusEffect(StatusKind.AttackDown, 2, 0, null));
			Assert.Near(clean * 1.3, Hit(goblin, target), "+30% com efeito negativo", 1);
		}

		[Test]
		private static void WolvesHitWoundedTargetsHarder()
		{
			var wolf = TestData.Unit("lobo", Side.Allies, passive: Passive(PassiveKind.BonusVsWounded, 0.25));
			var target = TestData.Unit("alvo", Side.Enemies, health: 1000);
			var healthy = Hit(wolf, target);
			target.Health = 400;
			Assert.Near(healthy * 1.25, Hit(wolf, target), "+25% abaixo da metade da Vida", 1);
		}

		[Test]
		private static void BanditsStartEachWaveWithImpeto()
		{
			var bandit = TestData.Unit("bandido", Side.Allies, speed: 1, passive: Passive(PassiveKind.ImpetoAtWaveStart, 0.3));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1);
			var session = TestData.Session(new[] { bandit }, new[] { foe });
			session.Start();
			Assert.Near(0.3 * BattleRules.FullImpeto, bandit.Impeto, "começa com 30% de Ímpeto");
			Assert.Near(0, foe.Impeto, "quem não tem a Assinatura começa do zero");
		}

		[Test]
		private static void TrollsRegenerateAtTheStartOfTheirTurn()
		{
			var troll = TestData.Unit("troll", Side.Allies, speed: 300, health: 1000, passive: Generic(PassiveKind.StatusOrEffectEachTurn, new EffectDefinition { Kind = EffectKind.Heal, Target = TargetKind.Self, Power = 0.05 }));
			var foe = TestData.Unit("inimigo", Side.Enemies, attack: 1);
			var session = TestData.Session(new[] { troll }, new[] { foe });
			session.Start();
			troll.Health = 500;

			var turn = session.BeginTurn();
			Assert.Equal(troll, turn.Actor, "o troll é o mais rápido");
			Assert.Near(550, troll.Health, "recupera 5% da Vida máxima");
		}

		[Test]
		private static void DragonsSetTargetsOnFire()
		{
			var burned = 0;
			for (var seed = 1; seed <= 20; seed++)
			{
				var dragon = TestData.Unit("dragão", Side.Allies, speed: 300, passive: Generic(PassiveKind.StatusOrEffectOnHit, Inflict(StatusKind.Affliction, 2)));
				var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
				var session = TestData.Session(new[] { dragon }, new[] { foe }, seed);
				session.Start();
				TestData.RunUntilTurnOf(session, dragon);
				var events = session.Act(new UnitAction(0, foe));
				Assert.True(events.OfType<StatusApplied>().Count(e => e.Status == StatusKind.Affliction) + events.OfType<Resisted>().Count() == 1, "um sorteio por alvo");
				if (foe.Has(StatusKind.Affliction))
					burned++;
			}

			Assert.True(burned >= 15, $"com 100% de chance quase sempre queima (só a Resistência mínima barra): {burned}/20");
		}

		[Test]
		private static void WizardsShortenTheirCooldowns()
		{
			var special = TestData.Strike with { Name = "Especial", Cooldown = 3, Effects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 2 } } };
			foreach (var flows in new[] { true, false })
			{
				var wizard = TestData.Unit("mago", Side.Allies, speed: 300, special: special, passive: flows ? Passive(PassiveKind.CooldownEachTurn, 1) : null);
				var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1);
				var session = TestData.Session(new[] { wizard }, new[] { foe });
				session.Start();

				TestData.RunUntilTurnOf(session, wizard);
				session.Act(new UnitAction(1, foe));
				Assert.Equal(2, wizard.Cooldown(1), "recarga 3 conta o turno de uso");

				// Segundo turno: com a Passiva, a recarga cai mais um no começo (2 → 1) e outro no fim (→ 0).
				TestData.RunUntilTurnOf(session, wizard);
				session.Act(new UnitAction(0, foe));
				TestData.RunUntilTurnOf(session, wizard);
				Assert.Equal(flows, wizard.IsReady(1), flows ? "com a Passiva, pronta um turno antes" : "sem a Passiva, ainda em recarga");
			}
		}

		[Test]
		private static void PaladinsMendTheMostInjuredAlly()
		{
			var paladin = TestData.Unit("paladino", Side.Allies, speed: 300, passive: Generic(PassiveKind.StatusOrEffectEachTurn, new EffectDefinition { Kind = EffectKind.Heal, Target = TargetKind.LowestAlly, Power = 0.06 }));
			var friend = TestData.Unit("amigo", Side.Allies, speed: 1, health: 1000);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1, attack: 1);
			var session = TestData.Session(new[] { paladin, friend }, new[] { foe });
			session.Start();
			friend.Health = 500;

			var turn = session.BeginTurn();
			Assert.Equal(paladin, turn.Actor, "o paladino é o mais rápido");
			Assert.Near(560, friend.Health, "o mais ferido recupera 6% da Vida máxima dele");
			Assert.Near(1000, paladin.Health, "quem está inteiro não ganha nada");
		}

		[Test]
		private static void DruidsHurtWhoeverHitsThem()
		{
			var druid = TestData.Unit("druida", Side.Enemies, health: 10_000, passive: Passive(PassiveKind.Thorns, 0.25));
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, health: 1000);
			var session = TestData.Session(new[] { hero }, new[] { druid });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, druid));
			Assert.Near(9900, druid.Health, "o golpe de 100 entra");
			Assert.Near(975, hero.Health, "e 25% dele voltam para quem bateu");
		}

		[Test]
		private static void ThornsCanStopASkillHalfway()
		{
			var flurry = TestData.Strike with
			{
				Effects = new[]
				{
					new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Hits = 3 },
					new EffectDefinition { Kind = EffectKind.Heal, Target = TargetKind.AllAllies, Power = 0.5 },
				},
			};
			var druid = TestData.Unit("druida", Side.Enemies, health: 10_000, attack: 1, passive: Passive(PassiveKind.Thorns, 0.25));
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, health: 20, basic: flurry);
			var friend = TestData.Unit("amigo", Side.Allies, speed: 1, health: 1000);
			var session = TestData.Session(new[] { hero, friend }, new[] { druid });
			session.Start();
			friend.Health = 100;

			TestData.RunUntilTurnOf(session, hero);
			var events = session.Act(new UnitAction(0, druid));
			Assert.False(hero.IsAlive, "os espinhos derrubam quem atacou");
			Assert.Equal(1, events.OfType<Damaged>().Count(e => e.Target == druid), "os golpes que faltavam não acontecem");
			Assert.Near(100, friend.Health, "nem o resto da habilidade");
			Assert.False(session.IsOver, "e a luta segue com quem sobrou");
		}

		[Test]
		private static void GargoylesStunWhoeverHitsThem()
		{
			var triple = TestData.Strike with { Effects = new[] { new EffectDefinition { Kind = EffectKind.Damage, Power = 1, Hits = 3 } } };
			var stunned = 0;
			for (var seed = 1; seed <= 20; seed++)
			{
				var gargoyle = TestData.Unit("gárgula", Side.Enemies, health: 1_000_000, attack: 1, passive: Generic(PassiveKind.StatusOrEffectOnAttacker, Inflict(StatusKind.Stun, 1)));
				var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: triple);
				var session = TestData.Session(new[] { hero }, new[] { gargoyle }, seed);
				session.Start();

				TestData.RunUntilTurnOf(session, hero);
				var events = session.Act(new UnitAction(0, gargoyle));
				var rolls = events.OfType<StatusApplied>().Count(e => e.Status == StatusKind.Stun && e.Target == hero) + events.OfType<Resisted>().Count(e => e.Target == hero);
				Assert.Equal(1, rolls, "um sorteio por habilidade, não por golpe");
				if (!hero.Has(StatusKind.Stun))
					continue;

				stunned++;
				var next = session.BeginTurn();
				Assert.True(next.Actor == hero && !next.NeedsDecision, "quem foi atordoado perde o turno seguinte");
			}

			Assert.True(stunned >= 15, $"com 100% de chance quase sempre atordoa (só a Resistência mínima barra): {stunned}/20");
		}

		[Test]
		private static void VampiresDrainWhatTheyDeal()
		{
			var vampire = TestData.Unit("vampiro", Side.Allies, speed: 300, health: 1000, passive: Passive(PassiveKind.Lifesteal, 0.2));
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1);
			var session = TestData.Session(new[] { vampire }, new[] { foe });
			session.Start();
			vampire.Health = 500;

			TestData.RunUntilTurnOf(session, vampire);
			session.Act(new UnitAction(0, foe));
			Assert.Near(520, vampire.Health, "drena 20% de um golpe de 100");
		}

		[Test]
		private static void CrowsCurseWhatTheyHit()
		{
			var cursed = 0;
			for (var seed = 1; seed <= 20; seed++)
			{
				var crow = TestData.Unit("corvo", Side.Allies, speed: 300, passive: Generic(PassiveKind.StatusOrEffectOnHit, Inflict(StatusKind.Curse, 2)));
				var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
				var session = TestData.Session(new[] { crow }, new[] { foe }, seed);
				session.Start();
				TestData.RunUntilTurnOf(session, crow);
				var events = session.Act(new UnitAction(0, foe));
				Assert.True(events.OfType<StatusApplied>().Count(e => e.Status == StatusKind.Curse) + events.OfType<Resisted>().Count() == 1, "um sorteio por alvo");
				if (foe.Has(StatusKind.Curse))
					cursed++;
			}

			Assert.True(cursed >= 15, $"com 100% de chance quase sempre amaldiçoa: {cursed}/20");
		}

		[Test]
		private static void BirdsDodgeHits()
		{
			var bird = TestData.Unit("pássaro", Side.Enemies, health: 1000, attack: 1, passive: Passive(PassiveKind.Dodge, 1));
			var hero = TestData.Unit("herói", Side.Allies, speed: 300);
			var session = TestData.Session(new[] { hero }, new[] { bird });
			session.Start();

			TestData.RunUntilTurnOf(session, hero);
			var events = session.Act(new UnitAction(0, bird));
			Assert.True(events.OfType<Missed>().Any(e => e.Target == bird), "o golpe erra");
			Assert.False(events.OfType<Damaged>().Any(), "e não causa dano");
			Assert.Near(1000, bird.Health, "a Vida fica inteira");
		}

		[Test]
		private static void PixiesCleanseAnAllyAtTheStartOfTheirTurn()
		{
			var pixie = TestData.Unit("pixie", Side.Allies, speed: 300, passive: Passive(PassiveKind.CleanseAllyEachTurn, 1));
			var friend = TestData.Unit("amigo", Side.Allies, speed: 1);
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1, attack: 1);
			var session = TestData.Session(new[] { pixie, friend }, new[] { foe });
			session.Start();
			friend.AddStatus(new StatusEffect(StatusKind.AttackDown, 3));
			friend.AddStatus(new StatusEffect(StatusKind.AttackUp, 3));

			var turn = session.BeginTurn();
			Assert.Equal(pixie, turn.Actor, "a pixie é a mais rápida");
			Assert.False(friend.Has(StatusKind.AttackDown), "o efeito negativo do aliado sai");
			Assert.True(friend.Has(StatusKind.AttackUp), "o positivo fica");
			session.Act(new UnitAction(0, foe));

			// Atordoada, ela limpa a si mesma antes de perder o turno.
			pixie.AddStatus(new StatusEffect(StatusKind.Stun, 1));
			var freed = session.BeginTurn();
			Assert.True(freed.Actor == pixie && freed.NeedsDecision, "a pixie sai do próprio atordoamento e age");
		}

		[Test]
		private static void CommonEnemiesAreBuffedSummons()
		{
			var database = TestData.LoadReal();
			var troll = database.Summon("troll_water");
			var encounter = new Encounter(4, 20, new[] { new[] { new StageEnemy { Summon = troll.Id } } }, Scale: 1.5);
			var team = PlayerTeam.Build(TestData.PlayerWith("imp_fire"), database, Teams.Campaign);
			var session = BattleFactory.Create(database, team, encounter, 1);
			session.Start();

			var foe = session.Enemies.Single();
			var basis = Core.Progression.Growth.Stats(troll.Stats, 4, 20);
			var (health, attack) = BattleFactory.FoeScale(troll.Rarity);
			Assert.Equal(troll.Name, foe.Name, "a mesma variante que o jogador invoca");
			Assert.Equal(troll.Element, foe.Element, "o elemento vem da variante");
			// A Passiva da luta é a da variante no nível 1: os mesmos efeitos, numa lista nova (SkillDefinition.At).
			var passive = troll.Skills.Single(s => s.IsPassive);
			Assert.Equal(passive.Passive!.Kind, foe.Passive?.Kind, "com a Passiva da variante");
			Assert.True(passive.Effects.SequenceEqual(foe.Passive!.Effects), "e os efeitos dela");
			Assert.Near(basis.Health * health * 1.5 * BattleFactory.FoeBoost, foe.MaxHealth, "Vida reforçada pelas estrelas, pelo encontro e pela força dos inimigos", 1e-6);
			Assert.Near(basis.Attack * attack * 1.5 * BattleFactory.FoeBoost, foe.Stats.Attack, "Ataque também", 1e-6);
			Assert.Near(basis.Defense * BattleFactory.FoeBoost, foe.Stats.Defense, "Defesa só pela força dos inimigos", 1e-6);
		}

		[Test]
		private static void EnemiesGrowPastLevelForty()
		{
			var forty = Core.Progression.Growth.FoeFraction(6, 40);
			var sixty = Core.Progression.Growth.FoeFraction(6, 60);
			Assert.Near(1.0, forty, "no 6★ nível 40, o mesmo do jogador", 1e-9);
			Assert.True(sixty > 1.2 && sixty < 1.22, $"no nível 60, a reta do 6★ continua (veio {sixty})");
			Assert.Near(Core.Progression.Growth.Fraction(5, 35), Core.Progression.Growth.FoeFraction(5, 50), "abaixo do 6★, o nível para no máximo da estrela", 1e-9);
		}

		[Test]
		private static void OnlyBossesAreUniqueEnemies()
		{
			var database = TestData.LoadReal();
			foreach (var enemy in database.Enemies)
				Assert.False(database.Families.Any(f => f.Id == enemy.Id), $"{enemy.Id} é família de invocação: vira \"summon\" nas ondas");

			var commons = database.Stages.SelectMany(s => s.Waves)
				.Concat(database.Dungeons.SelectMany(d => d.Floors).SelectMany(f => f.Waves))
				.SelectMany(w => w)
				.Count(slot => slot.Summon != null);
			Assert.True(commons > 0, "as ondas usam invocações");
		}
	}
}
