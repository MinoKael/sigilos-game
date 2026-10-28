using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>O auto-equipar da tela de Runas: a melhor combinação pelo papel, pelas habilidades e pelos conjuntos.</summary>
	internal static class RuneAutoEquipTests
	{
		private static int _ids;

		private static Rune Rune(RuneSet set, int slot, RuneStat main, int? on = null, params RuneSubstat[] subs) => new()
		{
			Id = ++_ids,
			Set = set,
			Slot = slot,
			Grade = 6,
			Level = 12,
			Main = main,
			Substats = subs.ToList(),
			EquippedOn = on,
		};

		private static SummonDefinition WithRole(Role role) => TestData.Database.Summons.First(s => s.Role == role);

		private static OwnedSummon Owned(SummonDefinition summon) => new() { Id = 1, SummonId = summon.Id, Stars = 6, Level = 40 };

		/// <summary>Um sigilo de cada espaço, em Vida, Ataque e Defesa porcentuais ou os fixos (4, 5, 6).</summary>
		private static List<Rune> Pool(RuneSet set, RuneStat percent)
		{
			var list = new List<Rune>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
				list.Add(Rune(set, slot, slot <= 3 ? percent : RuneRules.MainOptions(slot)[0]));
			return list;
		}

		[Test]
		private static void ItNeverTakesRunesFromOtherHomunculi()
		{
			var summon = WithRole(Role.Attack);
			var mine = Owned(summon);
			var stolen = Rune(RuneSet.Lethal, 1, RuneStat.AttackPercent, on: 99);
			var loose = Rune(RuneSet.Vigor, 1, RuneStat.HealthPercent);
			var chosen = RuneAutoEquip.Choose(summon, mine, new[] { stolen, loose });
			Assert.False(chosen.Contains(stolen), "o sigilo de outro homúnculo fica onde está");
			Assert.True(chosen.Contains(loose), "o solto entra");
		}

		[Test]
		private static void AnAttackerPrefersAttackAndCritOverHealth()
		{
			var summon = WithRole(Role.Attack);
			var runes = new List<Rune>
			{
				Rune(RuneSet.Vigor, 1, RuneStat.HealthPercent),
				Rune(RuneSet.Vigor, 1, RuneStat.AttackPercent),
				Rune(RuneSet.Vigor, 3, RuneStat.HealthPercent),
				Rune(RuneSet.Vigor, 3, RuneStat.AttackPercent),
			};
			var chosen = RuneAutoEquip.Choose(summon, Owned(summon), runes);
			Assert.True(chosen.Where(r => r.Slot is 1 or 3).All(r => r.Main == RuneStat.AttackPercent), "Ataque% nos espaços 1 e 3");
		}

		[Test]
		private static void ASupportPrefersSpeed()
		{
			var summon = WithRole(Role.Support);
			var runes = new List<Rune>
			{
				Rune(RuneSet.Vigor, 3, RuneStat.DefensePercent),
				Rune(RuneSet.Vigor, 3, RuneStat.Speed),
			};
			var chosen = RuneAutoEquip.Choose(summon, Owned(summon), runes);
			Assert.Equal(RuneStat.Speed, chosen.Single().Main, "o suporte escolhe Velocidade");
		}

		[Test]
		private static void AFourPieceSetIsFoundEvenWhenEachPieceAloneIsWeak()
		{
			// Letal (4 peças, +35% de Ataque) nos espaços 1 a 4, com Vida fixa; Vigor (2) tem Vida% em cada
			// espaço, melhor peça a peça, mas o conjunto de 4 vale mais que a soma das peças soltas.
			var summon = WithRole(Role.Attack);
			var runes = new List<Rune>();
			for (var slot = 1; slot <= 4; slot++)
			{
				runes.Add(Rune(RuneSet.Lethal, slot, RuneRules.MainOptions(slot)[slot == 4 ? 0 : 4]));
				runes.Add(Rune(RuneSet.Vigor, slot, RuneRules.MainOptions(slot)[slot == 4 ? 0 : 5]));
			}

			var chosen = RuneAutoEquip.Choose(summon, Owned(summon), runes);
			Assert.True(chosen.Count(r => r.Set == RuneSet.Lethal) >= 4, "Letal completo: " + string.Join(",", chosen.Select(r => r.Set)));
		}

		[Test]
		private static void TheResultIsNeverWorseThanTheCurrentBuild()
		{
			var summon = WithRole(Role.Defense);
			var mine = Owned(summon);
			var current = Pool(RuneSet.Vigor, RuneStat.HealthPercent);
			foreach (var rune in current)
				rune.EquippedOn = mine.Id;
			var better = Pool(RuneSet.Vigor, RuneStat.DefensePercent);
			var all = current.Concat(better).ToList();

			double Total(IEnumerable<Rune> runes) => SummonStats.For(summon, mine.Stars, mine.Level, false, runes.ToList()).Total.Defense;
			var chosen = RuneAutoEquip.Choose(summon, mine, all);
			Assert.True(Total(chosen) >= Total(current), "não perde Defesa para um Defensor");

			// Sem nada melhor solto, fica como está.
			var same = RuneAutoEquip.Choose(summon, mine, current);
			Assert.Equal(0, RuneAutoEquip.Changes(same, mine).Count, "nenhuma troca");
		}

		[Test]
		private static void TheSameInputGivesTheSameBuild()
		{
			var summon = WithRole(Role.Attack);
			var pool = Pool(RuneSet.Vigor, RuneStat.AttackPercent).Concat(Pool(RuneSet.Lethal, RuneStat.AttackPercent)).ToList();
			var first = RuneAutoEquip.Choose(summon, Owned(summon), pool).Select(r => r.Id).ToList();
			var second = RuneAutoEquip.Choose(summon, Owned(summon), pool.AsEnumerable().Reverse()).Select(r => r.Id).ToList();
			Assert.True(first.SequenceEqual(second), "a ordem da lista não muda o resultado");
		}

		[Test]
		private static void ARequiredSetIsKeptEvenWhenAnotherScoresMore()
		{
			// Para um atacante, Letal em 1 a 4 vale mais que Pressa; exigindo Pressa (4 peças), ela entra.
			var summon = WithRole(Role.Attack);
			var runes = Pool(RuneSet.Lethal, RuneStat.AttackPercent);
			for (var slot = 1; slot <= 4; slot++)
				runes.Add(Rune(RuneSet.Haste, slot, RuneRules.MainOptions(slot)[0]));

			var free = RuneAutoEquip.Choose(summon, Owned(summon), runes);
			Assert.True(free.Count(r => r.Set == RuneSet.Lethal) >= 4, "sem exigência, Letal: " + string.Join(",", free.Select(r => r.Set)));
			var chosen = RuneAutoEquip.Choose(summon, Owned(summon), runes, null, new[] { RuneSet.Haste });
			Assert.True(chosen != null && chosen.Count(r => r.Set == RuneSet.Haste) >= 4, "com Pressa exigida, Pressa completa");
		}

		[Test]
		private static void ARequiredSetWithoutEnoughPiecesGivesNothing()
		{
			// Só três sigilos de Pressa: o conjunto de 4 não fecha, e o resultado diz que não dá (nulo).
			var summon = WithRole(Role.Attack);
			var runes = Pool(RuneSet.Lethal, RuneStat.AttackPercent);
			for (var slot = 1; slot <= 3; slot++)
				runes.Add(Rune(RuneSet.Haste, slot, RuneRules.MainOptions(slot)[0]));
			Assert.True(RuneAutoEquip.Choose(summon, Owned(summon), runes, null, new[] { RuneSet.Haste }) == null, "nulo quando o conjunto não fecha");
		}

		[Test]
		private static void TheVariantLeansOnItsStrongestStat()
		{
			// Vida% e Defesa% no mesmo espaço: quem tem mais Defesa que Vida, em relação à média, escolhe Defesa%.
			var peers = TestData.Database.Summons;
			double Rel(SummonDefinition s, Stat stat) => s.Stats.Get(stat) / peers.Average(p => p.Stats.Get(stat));
			var defenders = peers.Where(s => s.Role is Role.Defense or Role.HP).ToList();
			var tank = defenders.OrderByDescending(s => Rel(s, Stat.Defense) - Rel(s, Stat.Health)).First();
			var wall = defenders.OrderByDescending(s => Rel(s, Stat.Health) - Rel(s, Stat.Defense)).First();
			var runes = new List<Rune> { Rune(RuneSet.Vigor, 3, RuneStat.HealthPercent), Rune(RuneSet.Vigor, 3, RuneStat.DefensePercent) };
			var withTank = RuneAutoEquip.Choose(tank, Owned(tank), runes, peers).Single().Main;
			var withWall = RuneAutoEquip.Choose(wall, Owned(wall), runes, peers).Single().Main;
			Assert.Equal(RuneStat.DefensePercent, withTank, "o tanque de Defesa");
			Assert.Equal(RuneStat.HealthPercent, withWall, "o de Vida");
		}
	}
}
