using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>A conta "Debugger" (<c>-- --debugger</c>): tudo feito, tudo no máximo, sempre igual.</summary>
	internal static class DebugAccountTests
	{
		private static PlayerState Build() => DebugAccount.Create(DateTime.UnixEpoch, TestData.Database);

		[Test]
		private static void DebuggerOwnsEveryHomunculusAtItsBest()
		{
			var database = TestData.Database;
			var player = Build();
			Assert.Equal(database.Summons.Count, player.Monsters.Count, "um de cada variante em jogo");
			Assert.Equal(database.Summons.Count, player.Monsters.Select(m => m.SummonId).Distinct().Count(), "sem repetir");
			foreach (var monster in player.Monsters)
			{
				var summon = database.Summon(monster.SummonId);
				Assert.Equal(Growth.MaxStars, monster.Stars, $"{monster.SummonId}: 6★");
				Assert.True(Leveling.IsMaxLevel(monster), $"{monster.SummonId}: nível máximo");
				Assert.Equal(Awakening.CanBeAwakened(summon), monster.Awakened, $"{monster.SummonId}: desperto quando pode");
				Assert.Equal(0, Fusion.SkillUpsLeft(database, monster), $"{monster.SummonId}: habilidades no máximo");
				Assert.False(monster.Stored, $"{monster.SummonId}: na coleção, não no Baú");
			}
		}

		[Test]
		private static void DebuggerHasEverythingCleared()
		{
			var database = TestData.Database;
			var player = Build();
			Assert.Equal(Account.MaxLevel, player.AccountLevel, "conta no nível máximo");
			Assert.Equal(database.Stages.Max(s => s.Number), player.HighestStage, "toda a Campanha");
			foreach (var dungeon in database.Dungeons)
				Assert.Equal(dungeon.Floors.Count, player.DungeonFloors.GetValueOrDefault(dungeon.Id), $"{dungeon.Id}: todos os andares");

			var constellations = database.Exploration.Constellations.Count;
			Assert.Equal(constellations, Exploration.Cleared(player, DateTime.UnixEpoch), "toda a Exploração do mês");
			Assert.True(player.TutorialDone, "sem tutorial");
			Assert.True(Teams.Of(player, Teams.Campaign).Count == PlayerState.TeamSize, "a Campanha já tem equipe");
		}

		[Test]
		private static void DebuggerRunesAreSevenHundredPerfectLegendarySixStars()
		{
			var player = Build();
			Assert.Equal(DebugAccount.RuneCount, player.Runes.Count, "700 runas");
			Assert.True(RuneInventory.Count(player) <= RuneInventory.Capacity, "cabem no inventário");
			Assert.Equal(player.Runes.Count, player.Runes.Select(r => r.Id).Distinct().Count(), "ids únicos");
			foreach (var rune in player.Runes)
			{
				var tag = $"runa {rune.Id}";
				Assert.Equal(RuneRules.MaxGrade, rune.Grade, $"{tag}: 6★");
				Assert.Equal(RuneRules.MaxLevel, rune.Level, $"{tag}: +15");
				Assert.Equal(RuneRarity.Legendary, rune.Rarity, $"{tag}: Lendária");
				Assert.True(RuneSets.IsActive(rune.Set), $"{tag}: conjunto em jogo");
				Assert.True(RuneRules.MainOptions(rune.Slot).Contains(rune.Main), $"{tag}: principal do espaço");

				var all = rune.Substats.Prepend(rune.Innate!).ToList();
				Assert.Equal(RuneRules.MaxSubstats + 1, all.Count, $"{tag}: nativo e 4 subatributos");
				Assert.Equal(all.Count, all.Select(s => s.Stat).Distinct().Count(), $"{tag}: atributos todos diferentes");
				Assert.True(all.All(s => RuneRules.CanBeSubstat(rune.Slot, rune.Main, s.Stat)), $"{tag}: atributos aceitos no espaço");

				var origin = RuneRules.SubstatRange(rune.Innate!.Stat, rune.Grade).Max;
				Assert.Near(origin, rune.Innate.Value, $"{tag}: nativo no máximo");
				foreach (var substat in rune.Substats)
				{
					var max = RuneRules.SubstatRange(substat.Stat, rune.Grade).Max;
					Assert.Equal(2, substat.Rolls.Count, $"{tag}: sorteio de origem e uma melhora");
					Assert.True(substat.Rolls.All(roll => Math.Abs(roll.Amount - max) < 1e-9), $"{tag}: {substat.Stat} no máximo");
					if (RuneRules.GrindRange(substat.Stat, RuneRarity.Legendary) is { } grind)
						Assert.Near(grind.Max, substat.Grind, $"{tag}: {substat.Stat} com a Pedra no máximo");
				}

				Assert.True(rune.EquippedOn == null, $"{tag}: solta");
			}

			Assert.Equal(RuneSets.ActiveSets.Count, player.Runes.Select(r => r.Set).Distinct().Count(), "todos os conjuntos em jogo");
			Assert.Equal(RuneRules.Slots, player.Runes.Select(r => r.Slot).Distinct().Count(), "todos os espaços");
		}

		[Test]
		private static void DebuggerIsTheSameEveryTimeAndSurvivesTheSave()
		{
			var first = PlayerSave.ToJson(Build());
			Assert.Equal(first, PlayerSave.ToJson(Build()), "a conta é refeita igual");
			var loaded = PlayerSave.FromJson(first);
			Assert.True(loaded != null, "o save dela abre");
			Assert.Equal(first, PlayerSave.ToJson(loaded!), "e volta igual");
			Assert.False(DebugAccount.Slot == "sigilos", "o arquivo nunca é o do save de verdade");
		}
	}
}
