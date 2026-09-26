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
			player.Gold = 0;
			player.HighestStage = 5;

			// 2,5 de Ouro por hora na fase 5: 5 horas dão 12,5; mais 5 horas fecham 13 com a fração.
			Assert.Equal(12, Idle.Collect(player, Start.AddHours(5)).Gold, "primeira coleta");
			Assert.Equal(13, Idle.Collect(player, Start.AddHours(10)).Gold, "a fração guardada fecha mais um");
			Assert.Equal(25, player.Gold, "Ouro na conta");
			Assert.True(player.Essence > NewGame.StartingEssence, "a ociosidade também rende Essência");
			Assert.Equal(0, player.Scrolls - NewGame.StartingScrolls, "e não dá Pergaminhos");
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
			var monster = Roster.Add(player, "diabrete_fogo");
			var needed = Leveling.MissingToMax(monster);

			Assert.Equal(needed, Leveling.Infuse(player, monster, int.MaxValue), "gasta só o que falta até o 40");
			Assert.Equal(40, monster.Level, "nível 40");
			Assert.Equal(1_000_000 - needed, player.Essence, "o resto da Essência fica");
		}

		[Test]
		private static void AwakeningCostsEssenceAndChangesTheSheet()
		{
			var database = TestData.LoadReal();
			var player = NewPlayer();
			var summon = database.Summon("diabrete_fogo");
			var monster = Roster.Add(player, summon.Id);
			player.Essence = Awakening.Cost(summon.Rarity) - 1;
			Assert.False(Awakening.Awaken(player, monster, summon), "sem Essência não desperta");

			player.Essence = Awakening.Cost(summon.Rarity);
			var before = SummonStats.For(database.Roles[summon.Role], summon, 10, 0, false, Array.Empty<Core.Runes.Rune>()).Total;
			Assert.True(Awakening.Awaken(player, monster, summon), "desperta");
			Assert.True(monster.Awakened, "marcado como desperto");
			Assert.False(Awakening.Awaken(player, monster, summon), "desperta uma vez só");

			var after = SummonStats.For(database.Roles[summon.Role], summon, 10, 0, true, Array.Empty<Core.Runes.Rune>()).Total;
			Assert.Near(Math.Round(before.Health * (1 + Awakening.HealthBonus)), after.Health, "+20% de Vida");
			Assert.Near(Math.Round(before.Attack * (1 + Awakening.AttackDefenseBonus)), after.Attack, "+7% de Ataque");
			Assert.Near(before.Get(summon.Awakening.Stat) + Awakening.Bonus(summon.Awakening.Stat), after.Get(summon.Awakening.Stat), "o bônus da variante", 1e-9);
			Assert.Equal(summon.Awakening.Name, summon.NameFor(true), "nome próprio");
		}

		[Test]
		private static void FirstVictoryPaysScrollsRuneAndExperience()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("diabrete_fogo");
			var stage = database.Stage(1);

			var first = Campaign.ApplyVictory(new Random(1), player, stage);
			Assert.True(first.FirstClear, "primeira vitória");
			Assert.Equal(stage.FirstClearScrolls, first.Scrolls, "Pergaminhos da primeira vitória");
			Assert.True(first.Rune != null && player.Runes.Count == 1, "a primeira vitória sempre solta runa");
			// A fase 1 dá mais experiência do que o nível 1 pede: a Líder vai ao nível 2 e sobra o resto.
			var leader = player.Monster(Teams.Of(player, Teams.Campaign)[0])!;
			Assert.Equal(2, leader.Level, "subiu de nível");
			Assert.Equal(stage.Experience - Leveling.ExperienceToNext(1), leader.Experience, "o resto da experiência fica");
			Assert.Equal(leader.Id, first.LevelUps.Single(), "quem subiu");
			Assert.True(Campaign.IsUnlocked(player, 2), "fase 2 abre");

			var again = Campaign.ApplyVictory(new Random(1), player, stage);
			Assert.Equal(0, again.Scrolls, "repetir não dá Pergaminhos");
			Assert.Equal(stage.Essence, again.Essence, "repetir dá Essência");
		}

		[Test]
		private static void GrowthStartsByNaturalStars()
		{
			Assert.Near(0.22, Growth.LevelFactor(3, 1), "3★ no nível 1");
			Assert.Near(0.43, Growth.LevelFactor(5, 1), "5★ no nível 1");
			Assert.Near(1.0, Growth.LevelFactor(3, 40), "nível 40");
			Assert.Near(0.85, Growth.RarityFactor(3), "3★ no nível 40");

			var roles = TestData.LoadReal().Roles;
			foreach (var role in roles.Values)
			{
				Assert.Near(0.15, role.Crit, "Crítico de base 15%");
				Assert.Near(0.5, role.CritDamage, "Dano crítico de base 50%");
				Assert.Near(0, role.Accuracy, "Precisão de base 0%");
			}
		}

		[Test]
		private static void SaveRoundTripsAndOldSavesAreRefused()
		{
			var player = TestData.PlayerWith("diabrete_fogo", "fenix_fogo");
			var monster = player.Monsters[0];
			monster.Level = 12;
			var rune = RuneInventory.Create(new Random(1), player, 3);
			RuneInventory.Upgrade(new Random(1), player, rune, 3);
			RuneInventory.Equip(player, rune, monster.Id);
			Roster.Store(player, player.Monsters[1].Id);
			player.Tools.Add(new Core.Runes.RuneTool(Core.Runes.RuneToolKind.Gem, Core.Runes.RuneStat.Speed, RuneRarity.Hero));

			var loaded = PlayerSave.FromJson(PlayerSave.ToJson(player));
			Assert.True(loaded != null, "lê o formato atual");
			Assert.Equal(12, loaded!.Monster(monster.Id)!.Level, "nível");
			Assert.Equal(1, loaded.RunesOn(monster.Id).Count, "runa equipada");
			Assert.Equal(rune.Substats.Sum(s => s.Value), loaded.Runes[0].Substats.Sum(s => s.Value), "a história dos sorteios volta igual");
			Assert.True(loaded.Monster(player.Monsters[1].Id)!.Stored, "o Baú fica");
			Assert.Equal(monster.Id, Teams.Of(loaded, Teams.Campaign).Single(), "a equipe da Campanha fica");
			Assert.Equal(player.LastIdleCollect, loaded.LastIdleCollect, "relógio da ociosidade");
			Assert.Equal(player.Tools[0], loaded.Tools.Single(), "pedra guardada");

			Assert.True(PlayerSave.FromJson("{ \"Scrolls\": 3 }") == null, "save sem versão é recusado");
		}

		[Test]
		private static void IdleRefillsManaUpToTheMax()
		{
			var player = NewPlayer();
			Assert.Equal(Mana.Max(player), player.Mana, "a conta começa com a Mana cheia");
			Assert.Equal(NewGame.StartingMana, Mana.Max(player), "e a cheia do nível 1 é a do NewGame");

			player.Mana = 0;
			Assert.Equal((int)(Mana.PerHour * 2), Idle.Collect(player, Start.AddHours(2)).Mana, "12 por hora");
			Idle.Collect(player, Start.AddHours(2 + Idle.CapHours));
			Assert.Equal(Mana.Max(player), player.Mana, "para no máximo");

			player.Mana = Mana.Max(player) + 40;
			Assert.Equal(0, Idle.Collect(player, Start.AddHours(4 + Idle.CapHours)).Mana, "acima do máximo não recarrega");
			Assert.Equal(Mana.Max(player) + 40, player.Mana, "mas não tira o que passou");
		}
	}
}
