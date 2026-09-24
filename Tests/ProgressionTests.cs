using System;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;

namespace Sigilos.Tests
{
	internal static class ProgressionTests
	{
		private static readonly DateTime Start = new(2026, 9, 23, 8, 0, 0);

		private static PlayerState NewPlayer() => NewGame.Create(Start, new Random(1));

		[Test]
		private static void IdleStopsAtTwelveHours()
		{
			var player = NewPlayer();
			Assert.Near(Idle.CapHours, Idle.PendingHours(player, Start.AddDays(3)), "três dias valem 12 horas");
		}

		[Test]
		private static void IdleKeepsTheFractionForNextTime()
		{
			var player = NewPlayer();
			player.Scrolls = 0;

			// 0,12 Pergaminho por hora na fase 0: 5 horas dão 0,6; mais 5 horas fecham 1,2.
			Assert.Equal(0, Idle.Collect(player, Start.AddHours(5)).Scrolls, "primeira coleta");
			Assert.Equal(1, Idle.Collect(player, Start.AddHours(10)).Scrolls, "a fração guardada fecha um Pergaminho");
			Assert.Equal(1, player.Scrolls, "Pergaminhos na conta");
			Assert.True(player.Dust > NewGame.StartingDust, "a ociosidade também rende Pó de Sigilo");
		}

		[Test]
		private static void QuickChannelOncePerDay()
		{
			var player = NewPlayer();
			var essence = player.Essence;

			var first = Idle.QuickChannel(player, Start);
			Assert.Near(Idle.QuickChannelHours, first.Hours, "horas da Canalização Rápida");
			Assert.True(player.Essence > essence, "entregou Essência");
			Assert.True(Idle.QuickChannel(player, Start.AddHours(3)).IsEmpty, "segunda vez no mesmo dia");
			Assert.False(Idle.QuickChannel(player, Start.AddDays(1)).IsEmpty, "no dia seguinte volta");
		}

		[Test]
		private static void ExperienceRaisesLevelUpToForty()
		{
			var summon = new OwnedSummon();
			Assert.Equal(1, Leveling.AddExperience(summon, Leveling.ExperienceToNext(1)), "um nível exato");
			Assert.Equal(2, summon.Level, "nível 2");

			Leveling.AddExperience(summon, 1_000_000);
			Assert.Equal(Leveling.MaxLevel, summon.Level, "teto 40");
			Assert.Equal(0, Leveling.MissingToMax(summon), "nada falta no teto");
		}

		[Test]
		private static void InfusingEssenceNeverGoesPastForty()
		{
			var player = NewPlayer();
			player.Essence = 1_000_000;
			var id = NewGame.StarterSummons[0];
			var needed = Leveling.MissingToMax(player.Summon(id));

			Assert.Equal(needed, Leveling.Infuse(player, id, int.MaxValue), "gasta só o que falta até o 40");
			Assert.Equal(40, player.Summon(id).Level, "nível 40");
			Assert.Equal(1_000_000 - needed, player.Essence, "o resto da Essência fica");
		}

		[Test]
		private static void AwakeningCostsEssenceAndChangesTheSheet()
		{
			var database = TestData.LoadReal();
			var player = NewPlayer();
			var summon = database.Summon(NewGame.StarterSummons[0]);
			Assert.False(Awakening.Awaken(player, summon), "sem Essência não desperta");

			player.Essence = Awakening.Cost(summon.Rarity);
			var before = SummonStats.For(database.Roles[summon.Role], summon, 10, 0, false, Array.Empty<Core.Runes.Rune>()).Total;
			Assert.True(Awakening.Awaken(player, summon), "desperta");
			Assert.True(player.Summon(summon.Id).Awakened, "marcado como desperto");

			var after = SummonStats.For(database.Roles[summon.Role], summon, 10, 0, true, Array.Empty<Core.Runes.Rune>()).Total;
			Assert.Near(before.Attack * (1 + Awakening.StatBonus), after.Attack, "+15% de Ataque", 1e-6);
			Assert.True(after.Get(summon.Awakening.Stat) > before.Get(summon.Awakening.Stat), "o atributo extra da variante sobe");
			Assert.Equal(summon.Awakening.Name, summon.NameFor(true), "nome próprio");
		}

		[Test]
		private static void FirstVictoryPaysScrollsRuneAndExperience()
		{
			var database = TestData.LoadReal();
			var player = NewPlayer();
			var runes = player.Runes.Count;
			var stage = database.Stage(1);

			var first = Campaign.ApplyVictory(new Random(1), player, stage);
			Assert.True(first.FirstClear, "primeira vitória");
			Assert.Equal(stage.FirstClearScrolls, first.Scrolls, "Pergaminhos da primeira vitória");
			Assert.True(first.Rune != null && player.Runes.Count == runes + 1, "a primeira vitória sempre solta runa");
			// A fase 1 dá mais experiência do que o nível 1 pede: a Líder vai ao nível 2 e sobra o resto.
			var leader = player.Summon(player.Team[0]);
			Assert.Equal(2, leader.Level, "subiu de nível");
			Assert.Equal(stage.Experience - Leveling.ExperienceToNext(1), leader.Experience, "o resto da experiência fica");
			Assert.True(Campaign.IsUnlocked(player, 2), "fase 2 abre");

			var again = Campaign.ApplyVictory(new Random(1), player, stage);
			Assert.Equal(0, again.Scrolls, "repetir não dá Pergaminhos");
			Assert.Equal(stage.Essence, again.Essence, "repetir dá Essência");
		}

		[Test]
		private static void GrowthMatchesTheAppendix()
		{
			Assert.Near(0.25, Growth.LevelFactor(1), "nível 1");
			Assert.Near(1.0, Growth.LevelFactor(40), "nível 40");
			Assert.Near(0.85, Growth.RarityFactor(3), "3★");
		}

		[Test]
		private static void SaveRoundTripsAndOldSavesAreRefused()
		{
			var player = NewPlayer();
			player.Summon(NewGame.StarterSummons[0]).Level = 12;
			player.Runes[0].EquippedOn = NewGame.StarterSummons[0];

			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player));
			Assert.True(loaded != null, "lê o formato atual");
			Assert.Equal(12, loaded!.Summon(NewGame.StarterSummons[0]).Level, "nível");
			Assert.Equal(NewGame.StarterRunes, loaded.Runes.Count, "runas");
			Assert.Equal(1, loaded.RunesOn(NewGame.StarterSummons[0]).Count, "runa equipada");
			Assert.Equal(Start, loaded.LastIdleCollect, "relógio da ociosidade");

			Assert.True(PlayerSave.FromJson("{ \"Scrolls\": 3 }") == null, "save sem versão é recusado");
		}
	}
}
