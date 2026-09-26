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

		private static PlayerState NewPlayer() => NewGame.Create(Start, new Random(1), TestData.Database);

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
		private static void ExperienceFollowsTheStarTable()
		{
			Assert.Equal(662, Leveling.ExperienceToNext(3, 1), "3★ nível 1 da tabela");
			Assert.Equal(112046, Leveling.ExperienceToNext(6, 39), "6★ nível 39 da tabela");
			Assert.Equal(82182, Leveling.TotalFor(3), "3★ do 1 ao 25");
			Assert.Equal(1005420, Leveling.TotalFor(6), "6★ do 1 ao 40");

			var monster = new OwnedSummon { Stars = 3 };
			Assert.Equal(1, Leveling.AddExperience(monster, 662), "um nível exato");
			Assert.Equal(2, monster.Level, "nível 2");

			Leveling.AddExperience(monster, 10_000_000);
			Assert.Equal(25, monster.Level, "o 3★ para no 25");
			Assert.Equal(0, monster.Experience, "no máximo a experiência para");
			Assert.Equal(0, Leveling.MissingToMax(monster), "nada falta no máximo");
		}

		[Test]
		private static void InfusingEssenceStopsAtTheStarMax()
		{
			var player = NewPlayer();
			player.Essence = 1_000_000;
			var monster = Roster.Add(player, TestData.Summon("imp_fire"));
			var needed = Leveling.EssenceFor(Leveling.MissingToMax(monster));

			Assert.Equal(needed, Leveling.Infuse(player, monster, int.MaxValue), "gasta só o que falta até o máximo");
			Assert.Equal(25, monster.Level, "3★ no nível 25");
			Assert.Equal(1_000_000 - needed, player.Essence, "o resto da Essência fica");
			Assert.Equal((82182 + Leveling.ExperiencePerEssence - 1) / Leveling.ExperiencePerEssence, needed, "cada Essência vale 10 de experiência");
		}

		[Test]
		private static void EvolutionNeedsMaxLevelEssenceAndFragments()
		{
			var player = NewPlayer();
			var monster = Roster.Add(player, TestData.Summon("imp_fire"));
			player.Essence = 1_000_000;
			Assert.Equal(3, monster.Stars, "nasce nas estrelas naturais");
			Assert.False(Evolution.IsReady(monster), "precisa do nível máximo");

			monster.Level = 25;
			Assert.False(Evolution.CanEvolve(player, monster), "sem Fragmentos não evolui");
			var (essence, fragments) = Evolution.Cost(3);
			player.Fragments = fragments;
			Assert.True(Evolution.Evolve(player, monster), "evolui");
			Assert.Equal(4, monster.Stars, "ganha uma estrela");
			Assert.Equal(1, monster.Level, "volta ao nível 1");
			Assert.Equal(30, Leveling.MaxLevel(monster), "o máximo sobe para 30");
			Assert.Equal(1_000_000 - essence, player.Essence, "paga a Essência");
			Assert.Equal(0, player.Fragments, "e os Fragmentos");

			monster.Stars = 6;
			monster.Level = 40;
			Assert.False(Evolution.IsReady(monster), "6★ é o máximo");
		}

		[Test]
		private static void AwakeningCostsEssenceAndChangesTheSheet()
		{
			var database = TestData.Database;
			Assert.Equal(25_000, Awakening.Cost(3), "3★ natural");
			Assert.Equal(50_000, Awakening.Cost(4), "4★ natural");
			Assert.Equal(75_000, Awakening.Cost(5), "5★ natural");

			var player = NewPlayer();
			var summon = database.Summon("phoenix_fire");
			var monster = Roster.Add(player, summon);
			player.Essence = Awakening.Cost(summon.Rarity) - 1;
			Assert.False(Awakening.Awaken(player, monster, summon), "sem Essência não desperta");

			player.Essence = Awakening.Cost(summon.Rarity);
			var before = SummonStats.For(database.Roles[summon.Role], summon, 5, 10, false, Array.Empty<Core.Runes.Rune>()).Total;
			Assert.True(Awakening.Awaken(player, monster, summon), "desperta");
			Assert.True(monster.Awakened, "marcado como desperto");
			Assert.False(Awakening.Awaken(player, monster, summon), "desperta uma vez só");

			var after = SummonStats.For(database.Roles[summon.Role], summon, 5, 10, true, Array.Empty<Core.Runes.Rune>()).Total;
			var stat = summon.Awakening.Stat!.Value;
			Assert.Near(Math.Round(before.Health * (1 + Awakening.HealthBonus)), after.Health, "+20% de Vida");
			Assert.Near(Math.Round(before.Attack * (1 + Awakening.AttackDefenseBonus)), after.Attack, "+7% de Ataque");
			Assert.Near(before.Get(stat) + Awakening.Bonus(stat), after.Get(stat), "o bônus da variante", 1e-9);
			Assert.Equal(summon.Awakening.Name, summon.NameFor(true), "nome próprio");
		}

		[Test]
		private static void FirstVictoryPaysScrollsRuneAndExperience()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("imp_fire");
			var stage = database.Stage(1);

			var first = Campaign.ApplyVictory(new Random(1), player, stage);
			Assert.True(first.FirstClear, "primeira vitória");
			Assert.Equal(stage.FirstClearScrolls, first.Scrolls, "Pergaminhos da primeira vitória");
			Assert.True(first.Rune != null && player.Runes.Count == 1, "a primeira vitória sempre solta runa");
			// A fase 1 dá menos experiência do que o nível 1 do 3★ pede: fica guardada.
			var leader = player.Monster(Teams.Of(player, Teams.Campaign)[0])!;
			Assert.Equal(1, leader.Level, "ainda no nível 1");
			Assert.Equal(stage.Experience, leader.Experience, "a experiência fica");
			Assert.Equal(0, first.LevelUps.Count, "ninguém subiu");
			Assert.True(Campaign.IsUnlocked(player, 2), "fase 2 abre");

			var again = Campaign.ApplyVictory(new Random(1), player, stage);
			Assert.Equal(0, again.Scrolls, "repetir não dá Pergaminhos");
			Assert.Equal(stage.Essence, again.Essence, "repetir dá Essência");
		}

		[Test]
		private static void GrowthStartsByNaturalStars()
		{
			Assert.Near(0.221, Growth.Fraction(3, 1), "3★ no nível 1");
			Assert.Near(0.398, Growth.Fraction(3, 25), "3★ no nível 25");
			Assert.Near(0.318, Growth.Fraction(4, 1), "evoluir volta abaixo do máximo anterior");
			Assert.Near(0.433, Growth.Fraction(5, 1), "5★ no nível 1");
			Assert.Near(1.0, Growth.Fraction(6, 40), "6★ no nível 40");
			Assert.Equal(25, Growth.MaxLevel(3), "3★ vai até o 25");
			Assert.Equal(40, Growth.MaxLevel(6), "6★ vai até o 40");
			Assert.Near(0.85, Growth.RarityFactor(3), "3★ natural no 6★ nível 40");

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
			var player = TestData.PlayerWith("imp_fire", "phoenix_fire");
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
