using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;

namespace Sigilos.Tests
{
	/// <summary>O chefe em batalha: quem é chefe e o foco do automático nele.</summary>
	internal static class BossTests
	{
		/// <summary>Uma onda com um Lobo de Vento (o Diabrete de Fogo tem vantagem nele) e o Mestre de Correntes.</summary>
		private static (BattleSession Session, BattleUnit Hero, BattleUnit Wolf, BattleUnit Boss) Fight()
		{
			var database = TestData.LoadReal();
			var team = PlayerTeam.Build(TestData.PlayerWith("imp_fire"), database, Teams.Campaign);
			var encounter = new Encounter(4, 20, new IReadOnlyList<StageEnemy>[]
			{
				new[] { new StageEnemy { Summon = "wolf_wind" }, new StageEnemy { Enemy = "chain_master", Element = Element.Dark } },
			});
			var session = BattleFactory.Create(database, team, encounter, 1);
			session.Start();
			return (session, session.Allies.Single(), session.Enemies.Single(e => !e.IsBoss), session.Enemies.Single(e => e.IsBoss));
		}

		[Test]
		private static void OnlyUniqueEnemiesAreBosses()
		{
			var (session, hero, wolf, boss) = Fight();
			Assert.Equal("chain_master", boss.DefinitionId, "o inimigo único de Data/enemies.json é o chefe");
			Assert.False(wolf.IsBoss, "invocação inimiga nunca é chefe");
			Assert.False(hero.IsBoss, "nem aliado");
			Assert.True(session.HasBoss, "a luta sabe que tem chefe (a pausa oferece o foco)");
		}

		[Test]
		private static void AutoFocusesTheBossOnlyWhenAsked()
		{
			var (session, hero, wolf, boss) = Fight();
			Assert.Equal(wolf, AutoPilot.ForAlly(session, hero).Target, "sem foco: a vantagem elemental (Fogo no Vento)");
			Assert.Equal(boss, AutoPilot.ForAlly(session, hero, focusBoss: true).Target, "com foco: o chefe, mesmo sem vantagem");
		}

		[Test]
		private static void MarkedEnemyComesBeforeTheBoss()
		{
			var (session, hero, wolf, boss) = Fight();
			Assert.Equal(wolf, AutoPilot.ForAlly(session, hero, focusBoss: true, focus: wolf).Target, "o inimigo marcado na tela vem antes do chefe");
			Assert.Equal(boss, AutoPilot.ForAlly(session, hero, focusBoss: false, focus: boss).Target, "e antes da vantagem elemental");
		}

		/// <summary>
		/// Um Rei Ossudo de teste (volta com 30% da Vida, escudo de 10% depois de agir) contra um herói
		/// rápido: a básica dele empurra o Ímpeto do alvo e o do próprio time, a especial derruba qualquer um.
		/// </summary>
		private static (BattleSession Session, BattleUnit Hero, BattleUnit King) Crypt()
		{
			var passive = new PassiveDefinition
			{
				Kind = PassiveKind.Undying,
				Value = 0.3,
				Effects = new[] { new EffectDefinition { Kind = EffectKind.Shield, Target = TargetKind.Self, Power = 0.1, Turns = 2 } },
			};
			var stats = new StatBlock { Health = 1000, Attack = 1, Speed = 100 };
			var king = new BattleUnit("rei", "rei", "", Side.Enemies, Element.Dark, 1, false, stats, new[] { TestData.Strike }, passive, Core.Runes.RuneSetEffects.None) { IsBoss = true };
			var push = new SkillDefinition
			{
				Name = "Empurrão",
				Effects = new[]
				{
					new EffectDefinition { Kind = EffectKind.Impeto, Power = 40 },
					new EffectDefinition { Kind = EffectKind.Impeto, Target = TargetKind.AllAllies, Power = 40 },
				},
			};
			var hero = TestData.Unit("herói", Side.Allies, speed: 300, attack: 100_000, basic: push, special: TestData.Strike);
			var session = TestData.Session(new[] { hero }, new[] { king });
			session.Start();
			return (session, hero, king);
		}

		[Test]
		private static void BoneKingShrugsImpetoShieldsAfterActingAndComesBack()
		{
			var (session, hero, king) = Crypt();
			TestData.RunUntilTurnOf(session, hero);
			var impeto = king.Impeto;
			session.Act(new UnitAction(0, king));
			Assert.Equal(impeto, king.Impeto, "com o Rei em campo, o inimigo não ganha Ímpeto");
			Assert.Equal(0.0, hero.Impeto, "nem o aliado");

			TestData.RunUntilTurnOf(session, king);
			session.Act(new UnitAction(0, hero));
			Assert.True(king.Has(StatusKind.Shield), "escudo depois de agir");

			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, king));
			Assert.True(king.Reviving, "caiu, mas vai voltar");
			Assert.False(session.IsOver, "o Rei que vai voltar segura a onda");
			TestData.RunUntilTurnOf(session, king);
			Assert.Near(300, king.Health, "volta com 30% da Vida");
		}

		[Test]
		private static void ForgottenBoneKingStaysDown()
		{
			var (session, hero, king) = Crypt();
			king.AddStatus(new StatusEffect(StatusKind.Oblivion, 2, 0, hero));
			TestData.RunUntilTurnOf(session, hero);
			var impeto = king.Impeto;
			session.Act(new UnitAction(0, king));
			Assert.Near(impeto + 40, king.Impeto, "esquecido, o Rei destrava o Ímpeto do inimigo");
			Assert.Near(40, hero.Impeto, "e do aliado");
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(1, king));
			Assert.False(king.Reviving, "com Esquecimento, não volta");
			Assert.True(session.Victory == true, "vitória");
		}

		[Test]
		private static void AfflictionTakesLessFromABoss()
		{
			var (session, hero, king) = Crypt();
			king.AddStatus(new StatusEffect(StatusKind.Affliction, 3, 0, hero));
			TestData.RunUntilTurnOf(session, king);
			Assert.Near(1000 * (1 - BattleRules.AfflictionFraction * BattleRules.BossAfflictionShare), king.Health, "no chefe, só uma parte da Aflição");
		}

		[Test]
		private static void GolemPillarsAreMinionsNotBosses()
		{
			var database = TestData.LoadReal();
			var floor = database.Dungeon("golem").Floor(5).Encounter;
			var foes = floor.Waves[^1].Select(slot => BattleFactory.Foe(database, slot, floor)).ToList();
			Assert.Equal(1, foes.Count(foe => foe.IsBoss), "um chefe só: o Golem");
			Assert.Equal("runic_golem", foes.Single(foe => foe.IsBoss).DefinitionId, "o Golem é o chefe");
			Assert.True(foes.Count(foe => foe.DefinitionId == "golem_bastion") == 2, "dois Bastiões ao lado dele no andar 5");
		}

		[Test]
		private static void TauntStillBeatsTheBossFocus()
		{
			var (session, hero, wolf, _) = Fight();
			hero.AddStatus(new StatusEffect(StatusKind.Taunt, 1, 0, wolf));
			Assert.Equal(wolf, AutoPilot.ForAlly(session, hero, focusBoss: true).Target, "provocado, ataca quem provocou");
		}
	}
}
