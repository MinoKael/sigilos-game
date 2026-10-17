using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>As regras de runa, com a melhora que nunca falha.</summary>
	internal static class RuneTests
	{
		private static Rune Rune(RuneSet set, int slot, RuneStat main, int grade = 6, int level = 0, params RuneSubstat[] substats) => new()
		{
			Set = set,
			Slot = slot,
			Grade = grade,
			Level = level,
			Main = main,
			Substats = substats.ToList(),
		};

		private static RuneSubstat Sub(RuneStat stat, double value) => RuneSubstat.Rolled(stat, 0, value);

		[Test]
		private static void DropsFollowTheSlotRules()
		{
			// 0.1: os espaços 1, 2 e 3 variam (inclusive Defesa%, em qualquer estrela); 4, 5 e 6 são fixos.
			Assert.Equal(RuneStat.DefenseFlat, RuneRules.MainOptions(4).Single(), "espaço 4");
			Assert.Equal(RuneStat.HealthFlat, RuneRules.MainOptions(5).Single(), "espaço 5");
			Assert.Equal(RuneStat.AttackFlat, RuneRules.MainOptions(6).Single(), "espaço 6");
			Assert.True(RuneRules.MainOptions(1).Contains(RuneStat.CritDamage), "Dano crítico no 1");
			Assert.True(RuneRules.MainOptions(2).Contains(RuneStat.Accuracy), "Precisão no 2");
			Assert.True(RuneRules.MainOptions(3).Contains(RuneStat.Speed), "Velocidade no 3");
			foreach (var slot in new[] { 1, 2, 3 })
				Assert.True(RuneRules.MainOptions(slot).Contains(RuneStat.DefensePercent), $"Defesa% pode ser o principal do espaço {slot}, de qualquer estrela");

			// Qualquer estrela: o principal sai sempre da lista do espaço (não há restrição por estrela).
			var grades = new Random(7);
			for (var grade = 1; grade <= 6; grade++)
			{
				for (var i = 0; i < 120; i++)
				{
					var rune = RuneForge.Generate(grades, i, grade);
					Assert.True(RuneRules.MainOptions(rune.Slot).Contains(rune.Main), $"{grade}★: principal {rune.Main} no espaço {rune.Slot}");
				}
			}

			var random = new Random(5);
			for (var i = 0; i < 500; i++)
			{
				var rune = RuneForge.Generate(random, i, 6);
				var stats = rune.Substats.Select(s => s.Stat).ToList();
				if (rune.Innate != null)
					stats.Add(rune.Innate.Stat);

				Assert.True(RuneRules.MainOptions(rune.Slot).Contains(rune.Main), $"principal {rune.Main} no espaço {rune.Slot}");
				Assert.True(rune.Substats.Count <= RuneRules.MaxSubstats, "até 4 subatributos");
				Assert.Equal(stats.Count, stats.Distinct().Count(), "nenhum atributo repetido");
				Assert.False(stats.Contains(rune.Main), "nenhum subatributo igual ao principal");
				if (rune.Slot == 1)
					Assert.False(stats.Any(s => s is RuneStat.DefenseFlat or RuneStat.DefensePercent), "espaço 1 sem Defesa");
				if (rune.Slot == 3)
					Assert.False(stats.Any(s => s is RuneStat.AttackFlat or RuneStat.AttackPercent), "espaço 3 sem Ataque");
			}
		}

		[Test]
		private static void PowerUpNeverFailsAndAddsSubstatsUntilFour()
		{
			var random = new Random(3);
			var rune = Rune(RuneSet.Haste, 2, RuneStat.Speed);
			Assert.Equal(RuneRarity.Normal, rune.Rarity, "sem subatributo é Normal");

			for (var level = 1; level <= RuneRules.MaxLevel; level++)
			{
				var before = rune.Substats.Sum(s => s.Value);
				Assert.True(RuneForge.RaiseLevel(random, rune), $"+{level} nunca falha");
				Assert.Equal(level, rune.Level, "o nível sobe");
				Assert.Equal(Math.Min(level / 3, 4), rune.Substats.Count, $"subatributos em +{level}");
				if (level is 13 or 14 or 15)
					Assert.Near(before, rune.Substats.Sum(s => s.Value), $"+{level} não mexe nos subatributos");
			}

			Assert.Equal(RuneRarity.Legendary, rune.Rarity, "com 4 subatributos vira Lendária");
			Assert.Near(42, rune.MainValue, "+15 dá o salto do principal");
			Assert.False(RuneForge.RaiseLevel(random, rune), "+15 é o teto");
		}

		[Test]
		private static void EquipSwapsTheSlotAndStoredMonstersKeepTheirRunes()
		{
			var player = TestData.PlayerWith("wolf_fire", "wolf_water");
			var (a, b) = (player.Monsters[0].Id, player.Monsters[1].Id);
			var small = Rune(RuneSet.Vigor, 2, RuneStat.Speed, grade: 3);
			var big = Rune(RuneSet.Haste, 2, RuneStat.Speed, grade: 5);
			player.Runes.AddRange(new[] { small, big });

			Assert.True(RuneInventory.Equip(player, small, a), "espaço vazio");
			Assert.True(RuneInventory.Equip(player, big, a), "troca");
			Assert.Equal(null, small.EquippedOn, "a antiga voltou ao inventário");
			Assert.True(RuneInventory.Equip(player, big, b), "muda de dono");
			Assert.Equal(0, player.RunesOn(a).Count, "o primeiro ficou sem runa");

			Roster.Store(player, b);
			Assert.Equal(b, big.EquippedOn, "no Baú, o monstro fica com as runas");
			Assert.True(RuneInventory.Equip(player, small, b), "e ainda recebe outras");
			Assert.Equal(null, big.EquippedOn, "trocando a do mesmo espaço");
		}

		[Test]
		private static void SellOnlyFromTheInventory()
		{
			var player = new PlayerState();
			var rune = RuneInventory.Create(new Random(1), player, 3);
			rune.EquippedOn = 7;
			Assert.Equal(0, RuneInventory.Sell(player, rune), "equipada não se desfaz");

			rune.EquippedOn = null;
			var extra = RuneInventory.Create(new Random(2), player, 1);
			var value = RuneInventory.SellAll(player, new[] { rune, extra });
			Assert.True(value > 0, "rende Essência");
			Assert.Equal(value, player.Essence, "Essência da venda");
			Assert.Equal(0, player.Runes.Count, "as duas desfeitas de uma vez");
		}

		[Test]
		private static void LockedRuneIsNeverSold()
		{
			var player = new PlayerState();
			var locked = RuneInventory.Create(new Random(1), player, 3);
			var loose = RuneInventory.Create(new Random(2), player, 3);
			locked.Locked = true;

			Assert.Equal(0, RuneInventory.Sell(player, locked), "bloqueada não se vende");
			var value = RuneInventory.SellAll(player, new[] { locked, loose });
			Assert.Equal(RuneRules.SellValue(loose), value, "em lote, só a solta rende");
			Assert.Equal(1, player.Runes.Count, "a bloqueada fica");
			Assert.True(player.Runes.Contains(locked), "e é ela que fica");

			player.Version = PlayerState.CurrentVersion;
			var saved = PlayerSave.FromJson(PlayerSave.ToJson(player))!;
			Assert.True(saved.Runes[0].Locked, "o bloqueio vai no save");
		}

	}
}
