using System;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	/// <summary>O começo do jogo: a luta de treino e as partes que a Campanha abre aos poucos.</summary>
	internal static class OnboardingTests
	{
		[Test]
		private static void NewAccountOpensThingsOneAtATime()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), database);
			Assert.False(Features.IsOpen(player, database, Feature.Monsters), "sem invocar, Monstros não aparece");
			Assert.False(Features.IsOpen(player, database, Feature.Runes), "nem Runas");

			Roster.Add(player, database.Summon("imp_fire"));
			player.TotalPulls = 1;
			Assert.True(Features.IsOpen(player, database, Feature.Monsters), "a primeira invocação abre Monstros");
			Assert.True(Features.IsOpen(player, database, Feature.Teams), "e Equipes");

			player.HighestStage = 1;
			Assert.True(Features.IsOpen(player, database, Feature.Runes), "a fase 1 abre as Runas (a primeira runa cai nela)");
			Assert.True(Features.IsNew(player, database, Feature.Runes), "e as Runas pulsam até a próxima fase");
			Assert.False(Features.IsOpen(player, database, Feature.Shop), "a Loja ainda não");
			player.HighestStage = 2;
			Assert.False(Features.IsNew(player, database, Feature.Runes), "na fase seguinte, as Runas já não são novas");

			Assert.True(Features.OpenedBy(database, 1).SequenceEqual(new[] { Feature.Runes }), "a vitória da fase 1 avisa as Runas");
			var dungeons = database.Dungeons.Min(d => d.UnlockStage);
			Assert.True(Features.OpenedBy(database, dungeons).Contains(Feature.Dungeons), "as Masmorras abrem com a primeira delas");
		}

		[Test]
		private static void ADungeonAlreadyBeatenNeverClosesAgain()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith(TestData.TypicalTeam);
			var crypt = database.Dungeons.Single(d => d.Id == "crypt");
			player.HighestStage = 20;
			Assert.False(Dungeons.IsUnlocked(player, crypt), "a Cripta abre depois da fase dela");
			player.DungeonFloors[crypt.Id] = 2;
			Assert.True(Dungeons.IsUnlocked(player, crypt), "mas quem já venceu um andar (de antes da mudança) segue com ela");
			Assert.True(Dungeons.IsFloorUnlocked(player, crypt, 3), "e com o andar seguinte");
			Assert.True(Features.IsOpen(player, database, Feature.Dungeons), "a porta das Masmorras também fica aberta");
		}

		[Test]
		private static void TrainingFightOnlyOpensOnANewAccount()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), database);
			Assert.True(Tutorial.ShouldStart(player), "conta nova começa pela luta de treino");
			player.TutorialDone = true;
			Assert.False(Tutorial.ShouldStart(player), "feita (ou pulada), não volta sozinha");

			var veteran = TestData.PlayerWith(TestData.TypicalTeam);
			veteran.TotalPulls = 10;
			Assert.False(Tutorial.ShouldStart(veteran), "quem já invocou não vê a luta de treino ao abrir");
		}

		/// <summary>A luta de treino não se perde: nem no automático, nem só com o básico no primeiro alvo.</summary>
		[Test]
		private static void TrainingFightIsAlwaysWon()
		{
			var database = TestData.LoadReal();
			var team = Tutorial.Team(database);
			Assert.Equal(3, team.Members.Count, "três monstros emprestados");
			for (var seed = 1; seed <= 30; seed++)
			{
				Assert.True(AutoBattle.Run(BattleFactory.Create(database, team, Tutorial.Encounter(), seed)), $"automático, semente {seed}");

				var session = BattleFactory.Create(database, team, Tutorial.Encounter(), seed);
				session.Start();
				while (!session.IsOver)
				{
					var turn = session.BeginTurn();
					if (!turn.NeedsDecision)
						continue;
					session.Act(turn.Actor.Side == Side.Allies
						? new UnitAction(0, session.ChoosableTargets(turn.Actor).FirstOrDefault())
						: AutoPilot.ForEnemy(session, turn.Actor));
				}

				Assert.True(session.Victory == true, $"só o básico, semente {seed}");
				Assert.True(session.Allies.All(a => a.IsAlive), $"ninguém cai, semente {seed}");
			}
		}
	}
}
