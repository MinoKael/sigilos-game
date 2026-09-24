using System;
using System.Linq;
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
			Assert.Near(Math.Round(before.Health * (1 + Awakening.HealthBonus)), after.Health, "+20% de Vida");
			Assert.Near(Math.Round(before.Attack * (1 + Awakening.AttackDefenseBonus)), after.Attack, "+7% de Ataque");
			Assert.Near(before.Get(summon.Awakening.Stat) + Awakening.Bonus(summon.Awakening.Stat), after.Get(summon.Awakening.Stat), "o bônus de Summoners War da variante", 1e-9);
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
		private static void GrowthMatchesSummonersWar()
		{
			// Diabrete de Fogo de Summoners War: 3★ nível 1 tem 1425 de Vida; 6★ nível 40, 6420 (22%).
			Assert.Near(0.22, Growth.LevelFactor(3, 1), "3★ no nível 1");
			// Fênix de Fogo: 5★ nível 1 tem 3990; 6★ nível 40, 9225 (43%).
			Assert.Near(0.43, Growth.LevelFactor(5, 1), "5★ no nível 1");
			Assert.Near(1.0, Growth.LevelFactor(3, 40), "nível 40");
			Assert.Near(0.85, Growth.RarityFactor(3), "3★ no nível 40");

			var roles = TestData.LoadReal().Roles;
			foreach (var role in roles.Values)
			{
				Assert.Near(0.15, role.Crit, "Crítico de base 15%");
				Assert.Near(0.5, role.CritDamage, "Dano crítico de base 50%");
				Assert.Near(0.15, role.Resistance, "Resistência de base 15%");
				Assert.Near(0, role.Accuracy, "Precisão de base 0%");
			}
		}

		[Test]
		private static void SaveRoundTripsAndOldSavesAreRefused()
		{
			var player = NewPlayer();
			player.Summon(NewGame.StarterSummons[0]).Level = 12;
			player.Runes[0].EquippedOn = NewGame.StarterSummons[0];

			player.Tools.Add(new Core.Runes.RuneTool(Core.Runes.RuneToolKind.Gem, Core.Runes.RuneStat.Speed, Core.Runes.RuneRarity.Hero));

			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player), new Random(1));
			Assert.True(loaded != null, "lê o formato atual");
			Assert.Equal(12, loaded!.Summon(NewGame.StarterSummons[0]).Level, "nível");
			Assert.Equal(NewGame.StarterRunes, loaded.Runes.Count, "runas");
			Assert.Equal(1, loaded.RunesOn(NewGame.StarterSummons[0]).Count, "runa equipada");
			Assert.Equal(Start, loaded.LastIdleCollect, "relógio da ociosidade");
			Assert.Equal(player.Tools[0], loaded.Tools.Single(), "pedra guardada");

			Assert.True(PlayerSave.FromJson("{ \"Scrolls\": 3 }", new Random(1)) == null, "save sem versão é recusado");
		}

		[Test]
		private static void VersionTwoSaveKeepsEverythingButTheOldRunes()
		{
			const string version2 = """
				{
				  "Version": 2, "Scrolls": 7, "Essence": 900, "Dust": 450, "HighestStage": 6,
				  "Summons": { "diabrete_fogo": { "Level": 21, "Experience": 5, "Echoes": 1, "Awakened": true } },
				  "Team": [ "diabrete_fogo" ],
				  "Runes": [
				    { "Id": 1, "Set": "Spiral", "Slot": 2, "Grade": 4, "Level": 9, "Main": "Focus", "Substats": [], "EquippedOn": "diabrete_fogo" },
				    { "Id": 2, "Set": "Door", "Slot": 5, "Grade": 2, "Level": 0, "Main": "HealthFlat", "Substats": [] }
				  ],
				  "NextRuneId": 3
				}
				""";

			var player = PlayerSave.FromJson(version2, new Random(1));
			Assert.True(player != null, "o formato 2 é convertido");
			Assert.Equal(PlayerState.CurrentVersion, player!.Version, "versão atual");
			Assert.Equal(21, player.Summon("diabrete_fogo").Level, "nível fica");
			Assert.True(player.Summon("diabrete_fogo").Awakened, "Despertar fica");
			Assert.Equal(450, player.Dust, "Pó fica");
			Assert.Equal(6, player.HighestStage, "fases ficam");
			Assert.Equal("4,2", string.Join(",", player.Runes.Select(r => r.Grade)), "uma runa nova por runa antiga, com as mesmas estrelas");
			Assert.Equal("2,5", string.Join(",", player.Runes.Select(r => r.Slot)), "no mesmo espaço");
			Assert.Equal("diabrete_fogo", player.Runes[0].EquippedOn, "na mesma invocação");
			Assert.Equal(null, player.Runes[1].EquippedOn, "a do inventário continua no inventário");
			Assert.True(player.Runes.All(r => r.Level == 0), "em +0");
		}
	}
}
