using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Tests
{
	/// <summary>
	/// Égide: inatingível enquanto durar (nenhum dano, de golpe ou não), ainda recebe efeitos negativos e pode ser
	/// removida. Irremovível: o efeito só acaba pelos turnos dele.
	/// </summary>
	internal static class AegisTests
	{
		private static SkillDefinition Skill(params EffectDefinition[] effects) => new() { Name = "teste", Effects = effects };

		private static EffectDefinition Hit() => new() { Kind = EffectKind.Damage, Target = TargetKind.Target, Power = 1 };

		private static EffectDefinition Inflict(StatusKind status, int turns = 3, bool unremovable = false) =>
			new() { Kind = EffectKind.Status, Status = status, Target = TargetKind.Target, Chance = 1, Turns = turns, Unremovable = unremovable };

		private static void Give(BattleUnit owner, StatusKind status, int turns, bool unremovable = false, double value = 0) =>
			owner.AddStatus(new StatusEffect(status, turns, value, owner) { Unremovable = unremovable });

		private static BattleSession Fight(BattleUnit hero, BattleUnit foe)
		{
			var session = TestData.Session(new[] { hero }, new[] { foe });
			session.Start();
			return session;
		}

		private static void Act(BattleSession session, BattleUnit hero, BattleUnit foe)
		{
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, foe));
		}

		[Test]
		private static void AegisBlocksEveryHitWhileItLastsAndStays()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Skill(Hit()));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1, health: 1000, resistance: 0);
			var session = Fight(hero, foe);
			Give(foe, StatusKind.Aegis, 5);

			Act(session, hero, foe);
			Act(session, hero, foe);
			Act(session, hero, foe);
			Assert.Near(1000, foe.Health, "três golpes, nenhum dano");
			Assert.True(foe.Has(StatusKind.Aegis), "e a Égide continua lá");
		}

		[Test]
		private static void AegisAlsoBlocksDamageThatIsNotAHit()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Skill(Hit()));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 200, health: 1000, resistance: 0);
			var session = Fight(hero, foe);
			Give(foe, StatusKind.Aegis, 6);
			Give(foe, StatusKind.Affliction, 6);

			Act(session, hero, foe);
			Act(session, hero, foe);
			Assert.Near(1000, foe.Health, "a Aflição também não tira nada");
			Assert.True(foe.Has(StatusKind.Affliction), "ela só não machuca");
		}

		[Test]
		private static void AegisStillLetsNegativeEffectsInAndCanBeStolen()
		{
			var steal = new EffectDefinition { Kind = EffectKind.StealBuff, Target = TargetKind.Target, Chance = 1 };
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Skill(Inflict(StatusKind.AttackDown), steal));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1, resistance: 0);
			var session = Fight(hero, foe);
			Give(foe, StatusKind.Aegis, 5);

			Act(session, hero, foe);
			Assert.True(foe.Has(StatusKind.AttackDown), "o efeito negativo pega");
			Assert.False(foe.Has(StatusKind.Aegis), "e a Égide pode ser roubada");
			Assert.True(hero.Has(StatusKind.Aegis), "quem roubou ficou inatingível");
		}

		[Test]
		private static void AegisEndsWithItsTurnsAndTheNextHitLands()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Skill(Hit()));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 200, health: 1000, resistance: 0);
			var session = Fight(hero, foe);
			Give(foe, StatusKind.Aegis, 1);

			Act(session, hero, foe);
			Assert.Near(1000, foe.Health, "com a Égide, nada");

			// O turno do inimigo passa: a Égide de 1 turno acaba no fim dele.
			TestData.RunUntilTurnOf(session, foe);
			session.Act(new UnitAction(0, null));
			Assert.False(foe.Has(StatusKind.Aegis), "acabou pelos turnos");

			Act(session, hero, foe);
			Assert.Near(900, foe.Health, "sem ela, o golpe passa");
		}

		[Test]
		private static void UnremovableEffectsResistCleanseStealAndShortening()
		{
			var cleanse = new EffectDefinition { Kind = EffectKind.Cleanse, Target = TargetKind.Target };
			var steal = new EffectDefinition { Kind = EffectKind.StealBuff, Target = TargetKind.Target, Chance = 1 };
			var shorten = new EffectDefinition { Kind = EffectKind.ChangeDuration, Target = TargetKind.Target, Chance = 1, Turns = -5, Scope = StatusScope.All };
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Skill(cleanse, steal, shorten));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1, resistance: 0);
			var session = Fight(hero, foe);
			Give(foe, StatusKind.AttackDown, 4, unremovable: true);
			Give(foe, StatusKind.DefenseUp, 4, unremovable: true);

			Act(session, hero, foe);
			Assert.True(foe.Has(StatusKind.AttackDown), "a Purificação não tira o irremovível");
			Assert.True(foe.Has(StatusKind.DefenseUp), "o roubo não leva o irremovível");
			Assert.False(hero.Has(StatusKind.DefenseUp), "e o ladrão não ganhou nada");
			Assert.Equal(4, foe.Find(StatusKind.AttackDown)!.Turns, "encurtar não mexe nos turnos dele");
		}

		[Test]
		private static void RemovableEffectsStillGoTheUsualWay()
		{
			var cleanse = new EffectDefinition { Kind = EffectKind.Cleanse, Target = TargetKind.Target };
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Skill(cleanse));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1, resistance: 0);
			var session = Fight(hero, foe);
			Give(foe, StatusKind.AttackDown, 4);

			Act(session, hero, foe);
			Assert.False(foe.Has(StatusKind.AttackDown), "o comum sai na Purificação");
		}

		[Test]
		private static void UnremovableEndsOnlyByItsOwnTurns()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 1, basic: Skill(Inflict(StatusKind.Blind, 2, unremovable: true)));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 100, resistance: 0, health: 1e9);
			var session = Fight(hero, foe);
			TestData.RunUntilTurnOf(session, foe);
			foe.AddStatus(new StatusEffect(StatusKind.Blind, 2, 0, hero) { Unremovable = true });
			Assert.True(foe.Find(StatusKind.Blind)!.Unremovable, "a marca vai com o efeito");
			session.Act(new UnitAction(0, null));
			TestData.RunUntilTurnOf(session, foe);
			session.Act(new UnitAction(0, null));
			Assert.False(foe.Has(StatusKind.Blind), "os turnos dele acabam com ele");
		}

		[Test]
		private static void UnremovableSleepIgnoresTheHitThatWakes()
		{
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, basic: Skill(Hit()));
			var foe = TestData.Unit("inimigo", Side.Enemies, speed: 1, health: 1e9);
			var session = Fight(hero, foe);
			Give(foe, StatusKind.Sleep, 5, unremovable: true);

			Act(session, hero, foe);
			Assert.True(foe.Has(StatusKind.Sleep), "o golpe não acorda o Sono irremovível");
		}

		[Test]
		private static void TheEffectFlagComesFromTheData()
		{
			var json = System.Text.Json.JsonSerializer.Deserialize<EffectDefinition>("{\"kind\":\"Status\",\"status\":\"Aegis\",\"unremovable\":true}", GameDatabase.JsonOptions);
			Assert.True(json!.Unremovable, "o campo do arquivo chega ao efeito");
			var skill = Skill(Inflict(StatusKind.Aegis, 2, unremovable: true));
			Assert.True(skill.Effects.Single().Unremovable, "e o efeito monta");
		}
	}
}
