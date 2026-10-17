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
		/// <summary>
		/// Uma onda com um Lobo de Vento (o Diabrete de Fogo tem vantagem nele) e o Mestre de Correntes. A
		/// especial do Diabrete (Pacto Profano) cura o próprio time e não mira: fica em recarga, para o
		/// automático escolher a básica, que mira.
		/// </summary>
		private static (BattleSession Session, BattleUnit Hero, BattleUnit Wolf, BattleUnit Boss) Fight()
		{
			var database = TestData.LoadReal();
			var team = PlayerTeam.Build(TestData.PlayerWith("wolf_fire"), database, Teams.Campaign);
			var encounter = new Encounter(4, 20, new IReadOnlyList<StageEnemy>[]
			{
				new[] { new StageEnemy { Summon = "wolf_wind" }, new StageEnemy { Enemy = "chain_master", Element = Element.Dark } },
			});
			var session = BattleFactory.Create(database, team, encounter, 1);
			session.Start();
			var hero = session.Allies.Single();
			hero.SetCooldown(1, hero.Skill(1).Cooldown);
			return (session, hero, session.Enemies.Single(e => !e.IsBoss), session.Enemies.Single(e => e.IsBoss));
		}

		/// <summary>Um herói que derruba o chefe num golpe, ao lado de um lacaio que não cai.</summary>
		private static (BattleSession Session, BattleUnit Minion) BossDownFirst(VictoryCondition victory)
		{
			var hero = TestData.Unit("heroi", Side.Allies, speed: 200, attack: 5000);
			var minion = TestData.Unit("lacaio", Side.Enemies, health: 1e9);
			var boss = TestData.Unit("chefe", Side.Enemies, health: 100, boss: true);
			var session = TestData.Session(new[] { hero }, new[] { minion, boss }, victory: victory);
			session.Start();
			TestData.RunUntilTurnOf(session, hero);
			session.Act(new UnitAction(0, boss));
			return (session, minion);
		}

		[Test]
		private static void BossDownIsNotEnoughInTheCampaign()
		{
			var (session, _) = BossDownFirst(VictoryCondition.AllWaves);
			Assert.False(session.IsOver, "Campanha: com o lacaio em pé, a luta segue");
		}

		[Test]
		private static void DungeonFloorsWinOnTheBoss()
		{
			var database = TestData.LoadReal();
			Assert.True(database.Dungeons.All(d => d.Floors.All(f => f.Encounter.Victory == VictoryCondition.Boss)), "todo andar de Masmorra vence no chefe");
			Assert.Equal(VictoryCondition.AllWaves, database.Stage(1).Encounter.Victory, "a fase da Campanha, em todas as ondas");
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

	}
}
