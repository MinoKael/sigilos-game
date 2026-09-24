using System;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	internal static class ProgressionTests
	{
		private static readonly DateTime Start = new(2026, 9, 23, 8, 0, 0);

		[Test]
		private static void IdleStopsAtTwelveHours()
		{
			var player = NewGame.Create(Start);
			Assert.Near(Idle.CapHours, Idle.PendingHours(player, Start.AddDays(3)), "três dias valem 12 horas");
		}

		[Test]
		private static void IdleKeepsTheFractionForNextTime()
		{
			var player = NewGame.Create(Start);
			player.Scrolls = 0;

			// 0,12 Pergaminho por hora na fase 0: 5 horas dão 0,6; mais 5 horas fecham 1,2.
			Assert.Equal(0, Idle.Collect(player, Start.AddHours(5)).Scrolls, "primeira coleta");
			Assert.Equal(1, Idle.Collect(player, Start.AddHours(10)).Scrolls, "a fração guardada fecha um Pergaminho");
			Assert.Equal(1, player.Scrolls, "Pergaminhos na conta");
		}

		[Test]
		private static void QuickChannelOncePerDay()
		{
			var player = NewGame.Create(Start);
			var essence = player.Essence;

			var first = Idle.QuickChannel(player, Start);
			Assert.Near(Idle.QuickChannelHours, first.Hours, "horas da Canalização Rápida");
			Assert.True(player.Essence > essence, "entregou Essência");
			Assert.True(Idle.QuickChannel(player, Start.AddHours(3)).IsEmpty, "segunda vez no mesmo dia");
			Assert.False(Idle.QuickChannel(player, Start.AddDays(1)).IsEmpty, "no dia seguinte volta");
		}

		[Test]
		private static void SharedLevelCostsEssenceAndStopsAtCap()
		{
			Assert.True(SharedLevel.CanRaise(1, SharedLevel.CostToRaise(1)), "Essência exata");
			Assert.False(SharedLevel.CanRaise(1, SharedLevel.CostToRaise(1) - 1), "falta 1");
			Assert.False(SharedLevel.CanRaise(SharedLevel.RegionOneCap, int.MaxValue), "teto da região 1");
		}

		[Test]
		private static void FirstVictoryPaysScrollsOnce()
		{
			var database = TestData.LoadReal();
			var player = NewGame.Create(Start);
			var stage = database.Stage(1);

			var first = Campaign.ApplyVictory(player, stage);
			Assert.True(first.FirstClear, "primeira vitória");
			Assert.Equal(stage.FirstClearScrolls, first.Scrolls, "Pergaminhos da primeira vitória");
			Assert.True(Campaign.IsUnlocked(player, 2), "fase 2 abre");

			var again = Campaign.ApplyVictory(player, stage);
			Assert.Equal(0, again.Scrolls, "repetir não dá Pergaminhos");
			Assert.Equal(stage.Essence, again.Essence, "repetir dá Essência");
		}

		[Test]
		private static void GrowthMatchesTheAppendix()
		{
			Assert.Near(0.25, Growth.LevelFactor(1), "nível 1");
			Assert.Near(1.0, Growth.LevelFactor(60), "nível 60");
			Assert.Near(0.85, Growth.RarityFactor(3), "3★");
		}

		[Test]
		private static void SaveRoundTrips()
		{
			var player = NewGame.Create(Start);
			player.Collection["fenix_fogo"] = 2;
			player.Posture = Core.Battle.Posture.Economic;

			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player));
			Assert.Equal(2, loaded.Echoes("fenix_fogo"), "Ecos");
			Assert.Equal(player.Team.Count, loaded.Team.Count, "time");
			Assert.Equal(Core.Battle.Posture.Economic, loaded.Posture, "postura");
			Assert.Equal(Start, loaded.LastIdleCollect, "relógio da ociosidade");
		}
	}
}
