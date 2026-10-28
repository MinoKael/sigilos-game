using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>
	/// As peças dos Cavaleiros: o Sacrifício (descontado, sem matar, ou destruindo Vida máxima até o teto), o que ele
	/// alimenta (cura e dano fixo pela Vida sacrificada), a imunidade passiva a efeitos e o "só outros" dos aliados.
	/// </summary>
	internal static class KnightTests
	{
		private static SkillDefinition Skill(int cooldown, params EffectDefinition[] effects) =>
			new() { Name = "teste", Cooldown = cooldown, Effects = effects };

		private static EffectDefinition Spend(double power) => new() { Kind = EffectKind.Sacrifice, Target = TargetKind.Self, Power = power };

		private static EffectDefinition DestroyHalfOfCurrent() => new()
		{
			Kind = EffectKind.Sacrifice, Target = TargetKind.Self, Power = 0.5, Destroy = true,
			Factor = new ScaleFactor { By = ScaleMeasure.HealthFraction, Base = 0, Slope = 1 },
		};

		private static EffectDefinition FixedFromSacrifice(params ScaleTerm[] plus) => new()
		{
			Kind = EffectKind.Damage, Target = TargetKind.AllEnemies, Stat = ScaleStat.Sacrificed, Power = 1, Fixed = true, Plus = plus,
		};

		private static EffectDefinition HealFromSacrifice() => new() { Kind = EffectKind.Heal, Target = TargetKind.AllAllies, Stat = ScaleStat.Sacrificed, Power = 1 };

		private static (BattleSession Session, BattleUnit Hero, BattleUnit Buddy, BattleUnit Foe) Scene(SkillDefinition special, double defense = 0)
		{
			var hero = TestData.Unit("cavaleiro", Side.Allies, speed: 300, health: 1000, defense: defense, special: special);
			var buddy = TestData.Unit("parceiro", Side.Allies, speed: 1, health: 1000, attack: 50);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero, buddy }, new[] { foe });
			session.Start();
			return (session, hero, buddy, foe);
		}

		private static void Cast(BattleSession session, BattleUnit hero, BattleUnit foe)
		{
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, foe));
		}

		[Test]
		private static void SpentSacrificeCostsLifeAndBecomesFixedDamage()
		{
			var (session, hero, _, foe) = Scene(Skill(4, Spend(0.1), FixedFromSacrifice()));
			Cast(session, hero, foe);
			Assert.Near(900, hero.Health, "perdeu 10% da Vida máxima");
			Assert.Near(100, 1_000_000 - foe.Health, "o inimigo levou o que ele perdeu, fixo");
			Assert.Near(1000, hero.MaxHealth, "a Vida máxima não mexe");
		}

		[Test]
		private static void SpentSacrificeNeverKillsTheCaster()
		{
			var (session, hero, _, foe) = Scene(Skill(4, Spend(0.5), FixedFromSacrifice()));
			TestData.RunUntilTurnOf(session, hero);
			hero.Health = 5;
			session.Act(new UnitAction(1, foe));
			Assert.True(hero.IsAlive, "o Sacrifício deixa pelo menos 1 de Vida");
			Assert.Near(1, hero.Health, "ficou em 1");
			Assert.Near(4, 1_000_000 - foe.Health, "só o que de fato saiu vira dano");
		}

		[Test]
		private static void DestroyingSacrificeLowersMaxHealthAndFeedsHealAndDamage()
		{
			var plus = new ScaleTerm { Stat = ScaleStat.Defense, Power = 0.5 };
			var (session, hero, buddy, foe) = Scene(Skill(4, DestroyHalfOfCurrent(), HealFromSacrifice(), FixedFromSacrifice(plus)), defense: 200);
			TestData.RunUntilTurnOf(session, hero);
			buddy.Health = 100;
			session.Act(new UnitAction(1, foe));
			Assert.Near(500, hero.HealthDestroyed, "metade da Vida atual virou Vida máxima destruída");
			Assert.Near(500, hero.MaxHealth, "a Vida máxima caiu");
			Assert.Near(500, hero.Health, "a Vida atual passou a nova máxima e desceu");
			Assert.Near(600, buddy.Health, "o aliado ganhou a Vida sacrificada");
			Assert.Near(600, 1_000_000 - foe.Health, "dano fixo: a Vida sacrificada mais 50% da Defesa");
		}

		[Test]
		private static void DestroyingSacrificeStopsAtSeventyPercentInTotal()
		{
			var (session, hero, _, foe) = Scene(Skill(1, DestroyHalfOfCurrent(), FixedFromSacrifice()));
			for (var i = 0; i < 4; i++)
			{
				TestData.RunUntilTurnOf(session, hero);
				session.Act(new UnitAction(1, foe));
			}

			Assert.Near(700, hero.HealthDestroyed, "o total destruído para em 70% da Vida base");
			Assert.Near(300, hero.MaxHealth, "sobra 30% da Vida máxima");
			Assert.True(hero.IsAlive, "o Cavaleiro continua de pé");
		}

		[Test]
		private static void StatusImmunityPassiveBlocksOnlyItsStatuses()
		{
			var passive = new PassiveDefinition { Kind = PassiveKind.StatusImmunity, Statuses = new[] { StatusKind.Stun, StatusKind.Sleep } };
			var hero = TestData.Unit("cavaleiro", Side.Allies, passive: passive);
			var foe = TestData.Unit("inimigo", Side.Enemies);
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			var resolver = new EffectResolver(session);
			resolver.ApplyStatus(foe, hero, StatusKind.Stun, 1, 2, resistible: false);
			resolver.ApplyStatus(foe, hero, StatusKind.Sleep, 1, 2, resistible: false);
			resolver.ApplyStatus(foe, hero, StatusKind.Affliction, 1, 2, resistible: false);
			Assert.False(hero.Has(StatusKind.Stun) || hero.Has(StatusKind.Sleep), "Atordoamento e Sono não pegam");
			Assert.True(hero.Has(StatusKind.Affliction), "os outros efeitos pegam");
		}

		[Test]
		private static void OthersOnlyKeepsTheCasterOutOfAllyTargets()
		{
			EffectDefinition Buff(bool others) => new() { Kind = EffectKind.Status, Target = TargetKind.HighestAlly, By = TargetRank.Attack, Status = StatusKind.AttackUp, Turns = 2, OthersOnly = others };

			var (session, hero, buddy, foe) = Scene(Skill(4, Buff(false)));
			Cast(session, hero, foe);
			Assert.True(hero.Has(StatusKind.AttackUp) && !buddy.Has(StatusKind.AttackUp), "sem a marca, o dono é o aliado com mais Ataque");

			(session, hero, buddy, foe) = Scene(Skill(4, Buff(true)));
			Cast(session, hero, foe);
			Assert.True(buddy.Has(StatusKind.AttackUp) && !hero.Has(StatusKind.AttackUp), "com a marca, o buff vai para outro aliado");
		}

		[Test]
		private static void JointAttackersStrikeInTheSameBeat()
		{
			var joint = new EffectDefinition { Kind = EffectKind.JointAttack, Target = TargetKind.HighestAlly, By = TargetRank.Attack, Count = 1 };
			var (session, hero, buddy, foe) = Scene(Skill(4, joint));
			TestData.RunUntilTurnOf(session, hero);
			var events = session.Act(new UnitAction(1, foe));
			var beats = Sigilos.Core.Battle.BattlePace.Beats(events);
			var approach = beats.First(b => b.Kind == Sigilos.Core.Battle.BeatKind.Approach);
			Assert.Equal(2, approach.Group?.Count ?? 0, "quem lançou e o aliado correm juntos, no mesmo momento");
			Assert.Equal(1, beats.Count(b => b.Kind == Sigilos.Core.Battle.BeatKind.Return), "e voltam juntos");
			var volley = beats.Single(b => b.Kind == Sigilos.Core.Battle.BeatKind.Volley && b.Group is { Count: 2 });
			Assert.Equal(2, volley.Events.OfType<Damaged>().Count(), "os dois danos aparecem no mesmo momento");
			Assert.True(volley.Group!.Any(a => a.Actor == hero) && volley.Group.Any(a => a.Actor == buddy), "o Cavaleiro e o aliado batem juntos");
		}

		[Test]
		private static void RankedJointAttackCallsTheTopAlliesByCount()
		{
			var joint = new EffectDefinition { Kind = EffectKind.JointAttack, Target = TargetKind.HighestAlly, By = TargetRank.Attack, Count = 2 };
			var hero = TestData.Unit("cavaleiro", Side.Allies, speed: 300, attack: 10, special: Skill(4, joint));
			var strong = TestData.Unit("forte", Side.Allies, speed: 1, attack: 90);
			var middle = TestData.Unit("medio", Side.Allies, speed: 1, attack: 50);
			var weak = TestData.Unit("fraco", Side.Allies, speed: 1, attack: 20);
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000, attack: 1, speed: 1);
			var session = TestData.Session(new[] { hero, strong, middle, weak }, new[] { foe });
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			var called = session.Act(new UnitAction(1, foe)).OfType<JointAttack>().Select(e => e.Unit.Name).ToList();
			Assert.Equal("cavaleiro,forte,medio", string.Join(",", called.OrderBy(n => n == "cavaleiro" ? 0 : n == "forte" ? 1 : 2)), "quem lança e os dois com mais Ataque");
			Assert.False(called.Contains("fraco"), "o terceiro não vem");
		}

		[Test]
		private static void SacrificeValidation()
		{
			var wrongTarget = Skill(4, new EffectDefinition { Kind = EffectKind.Sacrifice, Target = TargetKind.AllEnemies, Power = 0.1 });
			Assert.True(GameDatabase.ValidateSkill(wrongTarget).Any(), "Sacrifício só em si");
			var withoutSacrifice = Skill(4, FixedFromSacrifice());
			Assert.True(GameDatabase.ValidateSkill(withoutSacrifice).Any(), "ler a Vida sacrificada exige um Sacrifício antes");
			Assert.Empty(GameDatabase.ValidateSkill(Skill(4, Spend(0.1), FixedFromSacrifice())), "o par válido");
		}
	}
}
