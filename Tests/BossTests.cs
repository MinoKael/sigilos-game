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

		[Test]
		private static void TauntStillBeatsTheBossFocus()
		{
			var (session, hero, wolf, _) = Fight();
			hero.AddStatus(new StatusEffect(StatusKind.Taunt, 1, 0, wolf));
			Assert.Equal(wolf, AutoPilot.ForAlly(session, hero, focusBoss: true).Target, "provocado, ataca quem provocou");
		}
	}
}
