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
		private static void InfusingEssenceStopsAtTheStarMax()
		{
			var player = NewPlayer();
			player.Essence = 1_000_000;
			var monster = Roster.Add(player, TestData.Summon("wolf_fire"));
			var needed = Leveling.EssenceFor(Leveling.MissingToMax(monster));

			Assert.Equal(needed, Leveling.Infuse(player, monster, int.MaxValue), "gasta só o que falta até o máximo");
			Assert.Equal(25, monster.Level, "3★ no nível 25");
			Assert.Equal(1_000_000 - needed, player.Essence, "o resto da Essência fica");
			Assert.Equal((246546 + Leveling.ExperiencePerEssence - 1) / Leveling.ExperiencePerEssence, needed, "cada Essência vale 10 de experiência");
		}

		[Test]
		private static void EvolutionNeedsMaxLevelAndFragments()
		{
			var player = NewPlayer();
			var monster = Roster.Add(player, TestData.Summon("wolf_fire"));
			player.Essence = 1_000_000;
			Assert.Equal(3, monster.Stars, "nasce nas estrelas naturais");
			Assert.False(Evolution.IsReady(monster), "precisa do nível máximo");

			monster.Level = 25;
			Assert.False(Evolution.CanEvolve(player, monster), "sem Fragmentos não evolui");
			player.Fragments = Evolution.Cost(3);
			Assert.True(Evolution.Evolve(player, monster), "evolui");
			Assert.Equal(4, monster.Stars, "ganha uma estrela");
			Assert.Equal(25, monster.Level, "mantém o nível");
			Assert.Equal(0, monster.Experience, "e começa o nível seguinte do zero");
			Assert.Equal(30, Leveling.MaxLevel(monster), "o máximo sobe para 30");
			Assert.True(Growth.Fraction(4, 25) > Growth.Fraction(3, 25), "no mesmo nível, a estrela nova tem mais atributos");
			Assert.False(Evolution.IsReady(monster), "só evolui de novo no nível 30");
			Assert.True(Leveling.ExperienceToNext(monster) > 0, "e volta a ganhar experiência");
			Assert.Equal(1_000_000, player.Essence, "não gasta Essência");
			Assert.Equal(0, player.Fragments, "paga os Fragmentos");

			monster.Stars = 6;
			monster.Level = 40;
			Assert.False(Evolution.IsReady(monster), "6★ é o máximo");
		}

		[Test]
		private static void FirstVictoryPaysScrollsRuneAndExperience()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("wolf_fire");
			var stage = database.Stage(1);

			var first = Campaign.ApplyVictory(new Random(1), player, stage, database, DateTime.UnixEpoch);
			Assert.True(first.FirstClear, "primeira vitória");
			Assert.Equal(stage.FirstClearScrolls, first.Scrolls, "Pergaminhos da primeira vitória");
			Assert.True(first.Rune != null && player.Runes.Count == 1, "a primeira vitória sempre solta runa");
			// A experiência é dividida pela equipe: sozinho, o monstro leva a parte de cinco e já sobe.
			var leader = player.Monster(Teams.Of(player, Teams.Campaign)[0])!;
			var share = stage.Experience * Teams.SizeOf(Teams.Campaign);
			Assert.Equal(share, first.Experience, "a parte de quem luta sozinho");
			Assert.True(leader.Level >= 2 && first.LevelUps.Count == 1, "sozinho, sobe de nível");
			Assert.True(Campaign.IsUnlocked(player, 2), "fase 2 abre");

			var again = Campaign.ApplyVictory(new Random(1), player, stage, database, DateTime.UnixEpoch);
			Assert.Equal(0, again.Scrolls, "repetir não dá Pergaminhos");
			Assert.Equal(stage.Essence, again.Essence, "repetir dá Essência");
		}

		[Test]
		private static void SaveRoundTripsAndOldSavesAreRefused()
		{
			var player = TestData.PlayerWith("wolf_fire", "wizard_fire");
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
			Assert.Equal(player.BestTimes.Count, loaded.BestTimes.Count, "melhores tempos");

			Assert.True(PlayerSave.FromJson("{ \"Scrolls\": 3 }") == null, "save sem versão é recusado");
		}

		/// <summary>Subir <see cref="PlayerState.CurrentVersion"/> zera as contas: só o save mais velho vira jogo novo.</summary>
		[Test]
		private static void OnlyAnOlderSaveIsObsolete()
		{
			var current = PlayerState.CurrentVersion;
			Assert.True(PlayerSave.IsObsolete($"{{ \"Version\": {current - 1}, \"Scrolls\": 3 }}"), "versão anterior: recomeça");
			Assert.True(PlayerSave.IsObsolete("{ \"Scrolls\": 3 }"), "sem versão: de antes do campo existir");
			Assert.False(PlayerSave.IsObsolete(PlayerSave.ToJson(NewPlayer())), "a versão atual fica");
			Assert.False(PlayerSave.IsObsolete($"{{ \"Version\": {current + 1} }}"), "versão mais nova: o jogo é que está velho, nada se apaga");
			Assert.False(PlayerSave.IsObsolete("não é json"), "texto quebrado não conta");
		}

		[Test]
		private static void OpeningAnOldSaveFillsTheStartAndTheDeepestTeam()
		{
			var database = TestData.LoadReal();
			var player = TestData.PlayerWith("wolf_fire", "wizard_fire");
			player.Started = null;
			foreach (var monster in player.Monsters)
				Teams.Toggle(player, "golem", monster.Id);
			player.DungeonFloors["golem"] = 3;
			var older = Records.FloorKey("golem", 2);
			var deepest = Records.FloorKey("golem", 3);
			player.BestTimes[older] = 80;
			player.BestTimes[deepest] = 120;

			Account.Open(player, database, Start);
			Assert.Equal<DateTime?>(Start, player.Started, "o save sem começo começa ao abrir");
			Assert.Equal("wolf_fire,wizard_fire", string.Join(",", Records.Team(player, deepest).Select(m => m.Summon)), "o andar mais fundo ganha a equipe salva da Masmorra");
			Assert.Equal(0, Records.Team(player, older).Count, "os andares de antes ficam sem equipe");

			player.BestTeams[deepest] = [new RecordMember("wolf_dark", false)];
			Account.Open(player, database, Start.AddDays(5));
			Assert.Equal<DateTime?>(Start, player.Started, "abrir de novo não muda o começo");
			Assert.Equal("wolf_dark", Records.Team(player, deepest).Single().Summon, "nem a equipe que já existe");

			Assert.Equal<DateTime?>(null, player.GuildJoined, "sem guilda, sem data de entrada");
			player.GuildJoined = Start.AddDays(2);
			Assert.Equal<DateTime?>(Start.AddDays(2), PlayerSave.FromJson(PlayerSave.ToJson(player))!.GuildJoined, "o save guarda a entrada na guilda");
		}

    }
}
