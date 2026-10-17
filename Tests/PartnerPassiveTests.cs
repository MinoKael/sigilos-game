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
