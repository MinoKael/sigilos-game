using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	internal static class RuneTests
	{
		private static Rune Rune(Glyph set, int slot, RuneStat main, int grade = 5, int level = 0, params RuneSubstat[] substats) => new()
		{
			Set = set,
			Slot = slot,
			Grade = grade,
			Level = level,
			Main = main,
			Substats = substats.ToList(),
		};

		[Test]
		private static void SlotsOneThreeFiveHaveFixedMainStat()
		{
			Assert.Equal(RuneStat.AttackFlat, RuneRules.MainOptions(1).Single(), "espaço 1");
			Assert.Equal(RuneStat.DefenseFlat, RuneRules.MainOptions(3).Single(), "espaço 3");
			Assert.Equal(RuneStat.HealthFlat, RuneRules.MainOptions(5).Single(), "espaço 5");

			var random = new Random(5);
			for (var i = 0; i < 200; i++)
			{
				var rune = RuneForge.Generate(random, i, 3);
				Assert.True(RuneRules.MainOptions(rune.Slot).Contains(rune.Main), $"principal {rune.Main} no espaço {rune.Slot}");
				Assert.Equal(3, rune.Substats.Select(s => s.Stat).Append(rune.Main).Distinct().Count() - 1, "3 subatributos diferentes do principal");
			}
		}

		[Test]
		private static void MainStatGrowsFromQuarterToFullAtNine()
		{
			Assert.Near(0.10, RuneRules.MainValue(RuneStat.AttackPercent, 5, 0), "+0", 1e-9);
			Assert.Near(0.40, RuneRules.MainValue(RuneStat.AttackPercent, 5, 9), "+9", 1e-9);
		}

		[Test]
		private static void SubstatGrowsOnlyAtMilestones()
		{
			var random = new Random(9);
			var rune = RuneForge.Generate(random, 1, 5);
			var total = rune.Substats.Sum(s => s.Value);
			for (var level = 1; level <= RuneRules.MaxLevel; level++)
			{
				RuneForge.RaiseLevel(random, rune);
				var now = rune.Substats.Sum(s => s.Value);
				Assert.Equal(RuneRules.IsMilestone(level), now > total, $"subatributo cresceu no +{level}");
				total = now;
			}
		}

		[Test]
		private static void PercentStatsAreOnTheBaseAndSetsAddUp()
		{
			var baseStats = new StatBlock { Health = 1000, Attack = 100, Defense = 100, Speed = 100 };
			var runes = new List<Rune>
			{
				Rune(Glyph.Wall, 2, RuneStat.AttackPercent, level: 9),
				Rune(Glyph.Wall, 4, RuneStat.AttackPercent, level: 9),
				Rune(Glyph.Spiral, 1, RuneStat.AttackFlat, level: 9),
				Rune(Glyph.Spiral, 3, RuneStat.DefenseFlat, level: 9),
				Rune(Glyph.Spiral, 5, RuneStat.HealthFlat, level: 9),
				Rune(Glyph.Spiral, 6, RuneStat.Focus, level: 9),
			};

			var bonus = RuneBonuses.Compute(baseStats, runes);
			Assert.Near(40 + 40 + 110, bonus.Stats.Attack, "2 × 40% de 100, mais 110");
			Assert.Near(110 + 15, bonus.Stats.Defense, "110 da runa, mais 15% do conjunto da Muralha");
			Assert.Near(25, bonus.Stats.Speed, "conjunto da Espiral: +25% de 100");
			Assert.Equal(2, bonus.ActiveSets.Count, "Muralha (2) e Espiral (4)");
		}

		[Test]
		private static void EquipReplacesTheRuneInTheSameSlot()
		{
			var player = new PlayerState();
			var first = Rune(Glyph.Wall, 2, RuneStat.Speed);
			var second = Rune(Glyph.Eye, 2, RuneStat.AttackPercent);
			player.Runes.AddRange(new[] { first, second });

			RuneInventory.Equip(player, first, "a");
			RuneInventory.Equip(player, second, "a");
			Assert.Equal(null, first.EquippedOn, "a primeira voltou ao inventário");
			Assert.Equal("a", second.EquippedOn, "a segunda está equipada");
			Assert.Equal(1, player.RunesOn("a").Count, "uma runa no espaço 2");
		}

		[Test]
		private static void UpgradeCostsDustAndSellGivesItBack()
		{
			var player = new PlayerState { Dust = 0 };
			var rune = RuneInventory.Create(new Random(1), player, 3);
			Assert.False(RuneInventory.Upgrade(new Random(1), player, rune), "sem Pó não melhora");

			player.Dust = RuneRules.UpgradeCost(rune);
			Assert.True(RuneInventory.Upgrade(new Random(1), player, rune), "com Pó melhora");
			Assert.Equal(1, rune.Level, "+1");
			Assert.Equal(0, player.Dust, "Pó gasto");

			var value = RuneInventory.Sell(player, rune);
			Assert.Equal(value, player.Dust, "Pó da venda");
			Assert.Equal(0, player.Runes.Count, "runa desfeita");
		}

		[Test]
		private static void BoneSetDrainsAndDoorSetGivesExtraTurns()
		{
			var vampire = TestData.Unit("vampiro", Side.Allies, speed: 300, health: 1000, runeEffects: new RuneSetEffects(0.5, 0, 1));
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { vampire }, new[] { foe });
			session.Start();
			vampire.Health = 500;

			TestData.RunUntilTurnOf(session, vampire);
			var events = session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.Near(550, vampire.Health, "drena 50% de 100");
			Assert.True(events.OfType<ExtraTurn>().Any(), "100% de turno extra");
			Assert.Equal(vampire, session.BeginTurn().Actor, "age de novo em seguida");
		}

		[Test]
		private static void BondSetStunsOnHit()
		{
			var jailer = TestData.Unit("carcereiro", Side.Allies, speed: 300, runeEffects: new RuneSetEffects(0, 1, 0));
			var foe = TestData.Unit("inimigo", Side.Enemies, health: 1_000_000);
			var session = TestData.Session(new[] { jailer }, new[] { foe });
			session.Start();

			TestData.RunUntilTurnOf(session, jailer);
			session.Act(new UnitAction(SkillSlot.Basic, false, foe));
			Assert.True(foe.Has(StatusKind.Stun), "atordoado pelo conjunto do Laço");
		}
	}
}
