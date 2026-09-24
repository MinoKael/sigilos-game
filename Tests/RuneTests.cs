using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>As regras de runa de Summoners War, com a melhora que nunca falha.</summary>
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

		private static RuneSubstat Sub(RuneStat stat, double value) => new() { Stat = stat, Value = value };

		[Test]
		private static void DropsFollowTheSlotRules()
		{
			Assert.Equal(RuneStat.AttackFlat, RuneRules.MainOptions(1).Single(), "espaço 1");
			Assert.Equal(RuneStat.DefenseFlat, RuneRules.MainOptions(3).Single(), "espaço 3");
			Assert.Equal(RuneStat.HealthFlat, RuneRules.MainOptions(5).Single(), "espaço 5");
			Assert.True(RuneRules.MainOptions(2).Contains(RuneStat.Speed), "Velocidade só no 2");
			Assert.True(RuneRules.MainOptions(4).Contains(RuneStat.CritDamage), "Dano crítico só no 4");
			Assert.True(RuneRules.MainOptions(6).Contains(RuneStat.Accuracy), "Precisão só no 6");

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
		private static void MainStatMatchesTheSummonersWarTable()
		{
			Assert.Near(0.11, RuneRules.MainValue(RuneStat.AttackPercent, 6, 0), "6★ Ataque% +0", 1e-9);
			Assert.Near(0.47, RuneRules.MainValue(RuneStat.AttackPercent, 6, 12), "6★ Ataque% +12", 1e-9);
			Assert.Near(0.63, RuneRules.MainValue(RuneStat.AttackPercent, 6, 15), "6★ Ataque% +15", 1e-9);
			Assert.Near(0.12, RuneRules.MainValue(RuneStat.AttackPercent, 5, 2), "5★ Ataque% +2 arredonda para baixo", 1e-9);
			Assert.Near(42, RuneRules.MainValue(RuneStat.Speed, 6, 15), "6★ Velocidade +15");
			Assert.Near(2448, RuneRules.MainValue(RuneStat.HealthFlat, 6, 15), "6★ Vida +15");
			Assert.Near(7, RuneRules.MainValue(RuneStat.Speed, 3, 3), "3★ Velocidade +3 (passo de 4/3)");
			Assert.Near(0.80, RuneRules.MainValue(RuneStat.CritDamage, 6, 15), "6★ Dano crítico +15", 1e-9);
		}

		[Test]
		private static void PowerUpNeverFailsAndAddsSubstatsUntilFour()
		{
			var random = new Random(3);
			var rune = Rune(RuneSet.Swift, 2, RuneStat.Speed);
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
		private static void LegendaryRuneGrowsAnExistingSubstat()
		{
			var rune = Rune(RuneSet.Violent, 2, RuneStat.Speed, 6, 2,
				Sub(RuneStat.Crit, 0.05), Sub(RuneStat.AttackPercent, 0.05), Sub(RuneStat.HealthPercent, 0.05), Sub(RuneStat.Accuracy, 0.05));
			RuneForge.RaiseLevel(new Random(1), rune);
			Assert.Equal(4, rune.Substats.Count, "continua com 4");
			Assert.True(rune.Substats.Sum(s => s.Value) > 0.20 + 1e-9, "um deles cresceu em +3");
		}

		[Test]
		private static void UpgradeCostIsTheSummonersWarAverage()
		{
			Assert.Equal(1, RuneRules.UpgradeCost(1, 0), "1★ +0→+1: 100 de Mana");
			Assert.Equal(3270, RuneRules.UpgradeCost(6, 14), "6★ +14→+15: 16350 de Mana com 5% de chance");

			// A tabela de custo médio de Summoners War dá 894.206 de Mana para uma 6★ de +0 a +15.
			var full = RuneRules.UpgradeCost(Rune(RuneSet.Energy, 1, RuneStat.AttackFlat), RuneRules.MaxLevel);
			Assert.True(full >= 8942 && full <= 8942 + RuneRules.MaxLevel, $"6★ +0→+15 custa {full}");

			var player = new PlayerState();
			var rune = Rune(RuneSet.Energy, 1, RuneStat.AttackFlat, grade: 3);
			player.Dust = RuneRules.UpgradeCost(rune, 6) - 1;
			Assert.False(RuneInventory.Upgrade(new Random(1), player, rune, 6), "sem Pó para o caminho todo não melhora");
			Assert.Equal(0, rune.Level, "nada mudou");

			player.Dust++;
			Assert.True(RuneInventory.Upgrade(new Random(1), player, rune, 6), "com Pó melhora");
			Assert.Equal(6, rune.Level, "+6 de uma vez");
			Assert.Equal(0, player.Dust, "Pó gasto");
			Assert.Equal(9, RuneRules.NextMilestone(6), "próximo marco");
		}

		[Test]
		private static void PercentStatsAreOnTheBaseRoundedUpAndSetsAddUp()
		{
			var baseStats = new StatBlock { Health = 1001, Attack = 100, Defense = 100, Speed = 100 };
			var runes = new List<Rune>
			{
				Rune(RuneSet.Energy, 2, RuneStat.HealthPercent, level: 15),
				Rune(RuneSet.Energy, 4, RuneStat.AttackPercent, level: 15),
				Rune(RuneSet.Swift, 1, RuneStat.AttackFlat, level: 15),
				Rune(RuneSet.Swift, 3, RuneStat.DefenseFlat, level: 15),
				Rune(RuneSet.Swift, 5, RuneStat.HealthFlat, level: 15),
				Rune(RuneSet.Swift, 6, RuneStat.Accuracy, level: 15),
			};

			var bonus = RuneBonuses.Compute(baseStats, runes);
			Assert.Near(631 + 2448 + 151, bonus.Stats.Health, "63% de 1001 (630,63 → 631), 2448 e Energia: 15% de 1001 (150,15 → 151)");
			Assert.Near(63 + 160, bonus.Stats.Attack, "63% de 100, mais 160");
			Assert.Near(160, bonus.Stats.Defense, "160 da runa");
			Assert.Near(25, bonus.Stats.Speed, "Rapidez: +25% de 100");
			Assert.Near(0.64, bonus.Stats.Accuracy, "Precisão 64%", 1e-9);
			Assert.Equal(2, bonus.ActiveSets.Count, "Energia (2) e Rapidez (4)");
		}

		[Test]
		private static void ThreeTwoPieceSetsCountThreeTimes()
		{
			var runes = Enumerable.Range(1, 6).Select(slot => Rune(RuneSet.Guard, slot, RuneRules.MainOptions(slot)[0], grade: 1)).ToList();
			var bonus = RuneBonuses.Compute(new StatBlock { Defense = 1000 }, runes);
			Assert.Equal(3, bonus.ActiveSets.Count, "três Guardas");
			Assert.True(bonus.Stats.Defense >= 450, "+45% de Defesa dos conjuntos");
		}

		[Test]
		private static void RemovingARuneCostsLikeSummonersWar()
		{
			var player = new PlayerState { Dust = 1000 };
			var small = Rune(RuneSet.Energy, 2, RuneStat.Speed, grade: 3);
			var big = Rune(RuneSet.Swift, 2, RuneStat.Speed, grade: 5);
			player.Runes.AddRange(new[] { small, big });

			Assert.True(RuneInventory.Equip(player, small, "a"), "espaço vazio: de graça");
			Assert.Equal(1000, player.Dust, "nada cobrado");

			Assert.Equal(50, RuneInventory.EquipCost(player, big, "a"), "por cima de uma 3★: paga a saída dela");
			Assert.True(RuneInventory.Equip(player, big, "a"), "troca");
			Assert.Equal(null, small.EquippedOn, "a antiga voltou ao inventário");
			Assert.Equal(950, player.Dust, "50 de Pó");

			Assert.True(RuneInventory.Equip(player, big, "b"), "muda de dono");
			Assert.Equal(700, player.Dust, "paga a saída da 5★: 250");

			Assert.True(RuneInventory.Unequip(player, big), "tira");
			Assert.Equal(450, player.Dust, "mais 250");

			player.Dust = 0;
			RuneInventory.Equip(player, big, "a");
			Assert.True(RuneInventory.Equip(player, small, "b"), "espaço vazio segue de graça");
			Assert.False(RuneInventory.Unequip(player, big), "sem Pó não tira");
			Assert.Equal("a", big.EquippedOn, "continua equipada");
		}

		[Test]
		private static void SellOnlyFromTheInventory()
		{
			var player = new PlayerState();
			var rune = RuneInventory.Create(new Random(1), player, 3);
			rune.EquippedOn = "a";
			Assert.Equal(0, RuneInventory.Sell(player, rune), "equipada não se desfaz");

			rune.EquippedOn = null;
			var value = RuneInventory.Sell(player, rune);
			Assert.True(value > 0, "rende Pó");
			Assert.Equal(value, player.Dust, "Pó da venda");
			Assert.Equal(0, player.Runes.Count, "runa desfeita");
		}

		[Test]
		private static void GrindstoneRaisesOnlyItsStatAndReplacesTheOldBonus()
		{
			var random = new Random(2);
			var rune = Rune(RuneSet.Fatal, 2, RuneStat.Speed, substats: new[] { Sub(RuneStat.AttackPercent, 0.05), Sub(RuneStat.Crit, 0.05) });
			var hero = new RuneTool(RuneToolKind.Grindstone, RuneStat.AttackPercent, RuneRarity.Hero);

			Assert.False(RuneForge.CanGrind(rune, 1, hero), "pedra de Ataque% não serve no Crítico");
			Assert.False(RuneForge.CanGrind(rune, 1, hero with { Stat = RuneStat.Crit }), "Crítico não se afia");
			Assert.True(RuneForge.Grind(random, rune, 0, hero), "afia o Ataque%");
			Assert.True(rune.Substats[0].Grind is >= 0.04 - 1e-9 and <= 0.07 + 1e-9, "Heroica: 4% a 7%");
			Assert.Near(0.05, rune.Substats[0].Value, "o valor de origem fica");

			rune.Substats[0].Grind = 0.07;
			Assert.False(RuneForge.CanGrind(rune, 0, hero), "no máximo da pedra, a mesma pedra não serve mais");
			Assert.True(RuneForge.CanGrind(rune, 0, hero with { Grade = RuneRarity.Legendary }), "a Lendária ainda pode passar");

			var player = new PlayerState();
			player.Runes.Add(rune);
			var legendary = hero with { Grade = RuneRarity.Legendary };
			player.Tools.Add(legendary);
			Assert.True(RuneInventory.Grind(random, player, rune, 0, legendary), "usa a pedra do inventário");
			Assert.Equal(0, player.Tools.Count, "pedra gasta");
		}

		[Test]
		private static void GemNeedsPlusTwelveAndOnlyOneSubstatPerRune()
		{
			var random = new Random(4);
			var rune = Rune(RuneSet.Violent, 1, RuneStat.AttackFlat, 6, 11,
				Sub(RuneStat.HealthFlat, 300), Sub(RuneStat.Crit, 0.05), Sub(RuneStat.Speed, 5), Sub(RuneStat.Resistance, 0.05));
			var gem = new RuneTool(RuneToolKind.Gem, RuneStat.CritDamage, RuneRarity.Legendary);

			Assert.False(RuneForge.CanEnchant(rune, 0, gem), "+11 ainda não aceita gema");
			rune.Level = 12;
			Assert.False(RuneForge.CanEnchant(rune, 0, gem with { Stat = RuneStat.DefensePercent }), "espaço 1 não aceita Defesa");
			Assert.False(RuneForge.CanEnchant(rune, 0, gem with { Stat = RuneStat.Crit }), "não repete outro subatributo");
			Assert.True(RuneForge.Enchant(random, rune, 0, gem), "troca Vida fixa por Dano crítico");
			Assert.Equal(RuneStat.CritDamage, rune.Substats[0].Stat, "atributo novo");
			Assert.True(rune.Substats[0].Enchanted, "marcado como encantado");
			Assert.True(rune.Substats[0].Value is >= 0.07 - 1e-9 and <= 0.10 + 1e-9, "Lendária: 7% a 10%");

			Assert.False(RuneForge.CanEnchant(rune, 3, gem with { Stat = RuneStat.Accuracy }), "só um subatributo encantado por runa");
			Assert.True(RuneForge.CanEnchant(rune, 0, gem with { Stat = RuneStat.Accuracy }), "o encantado pode ser trocado de novo");
		}

		[Test]
		private static void LeaderGrowsOnlyTheBase()
		{
			var sheet = new StatSheet(new StatBlock { Attack = 100 }, RuneBonus.None with { Stats = new StatBlock { Attack = 200 } });
			var total = sheet.TotalWith(new LeaderDefinition { Stat = Stat.Attack, Value = 0.2 });
			Assert.Near(320, total.Attack, "base 100 + runas 200 + 20% de 100");
		}
	}
}
