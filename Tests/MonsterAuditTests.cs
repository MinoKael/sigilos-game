using System;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.Core.Summoning;

namespace Sigilos.Tests
{
	/// <summary>A conferência do crescimento dos monstros no save enviado: o jogo de verdade passa, a conta que ninguém pagou não.</summary>
	internal static class MonsterAuditTests
	{
		private static AuditContext Context(double minutes = 0.05) => new(TestData.Database, TimeSpan.FromMinutes(minutes), 0);

		private static PlayerState Player(int monsters = 6)
		{
			var player = NewGame.Create(DateTime.UnixEpoch, new Random(1), TestData.Database);
			player.Essence = 50_000_000;
			player.Fragments = 5_000;
			foreach (var summon in TestData.Database.Summons.Where(s => s.Rarity >= 3).Take(monsters))
				Roster.Add(player, summon);
			return player;
		}

		private static PlayerState Copy(PlayerState player) => PlayerSave.FromJson(PlayerSave.ToJson(player))!;

		private static string? Check(PlayerState next, PlayerState before, double minutes = 0.05) => SaveAudit.Check(next, before, false, Context(minutes));

		[Test]
		private static void InfusingEveryMonsterToMaxInOneWindowIsLegitimate()
		{
			var before = Player();
			var next = Copy(before);
			foreach (var monster in next.Monsters)
				Leveling.Infuse(next, monster, int.MaxValue);

			Assert.True(next.Monsters.All(Leveling.IsMaxLevel), "todos no nível máximo");
			Assert.Equal(null, Check(next, before), "a Essência gasta paga a experiência");
		}

		[Test]
		private static void EvolvingFromTheNaturalStarsToSixIsLegitimate()
		{
			var before = Player();
			var next = Copy(before);
			var monster = next.Monsters.First();
			while (monster.Stars < Growth.MaxStars)
			{
				Leveling.Infuse(next, monster, int.MaxValue);
				Assert.True(Evolution.Evolve(next, monster), "evolui");
			}

			Leveling.Infuse(next, monster, int.MaxValue);
			Assert.Equal(Growth.MaxStars, monster.Stars, "6 estrelas");
			Assert.Equal(null, Check(next, before), "estrelas pagas com Fragmentos");
		}

		[Test]
		private static void FusingAndAwakeningAreLegitimate()
		{
			var before = Player(1);
			var target = before.Monsters.First();
			var family = TestData.Database.Summons.Where(s => s.FamilyId == TestData.Database.Summon(target.SummonId).FamilyId).ToList();
			for (var i = 0; i < 12; i++)
				Roster.Add(before, family[i % family.Count]);

			var next = Copy(before);
			var fused = Fusion.FuseMany(new Random(2), next, TestData.Database, target.Id, next.Monsters.Skip(1).Select(m => m.Id).ToList());
			Assert.True(fused > 0, "fundiu");
			Assert.True(Awakening.Awaken(next, next.Monster(target.Id)!, TestData.Database.Summon(target.SummonId)), "despertou");
			Assert.Equal(null, Check(next, before), "materiais e Essência pagam");
		}

		[Test]
		private static void ASummonRacedByADropStaysConsistent()
		{
			var server = Player(2);
			var client = Copy(server);
			var baseline = ServerActions.Snapshot(client);
			var delta = ServerActions.Apply(new Random(4), TestData.Database, server, new ActionRequest { Kind = ServerActionKind.Summon, Count = 10, Scroll = ScrollKind.Mystic })!;

			// Um drop chegou ao aparelho enquanto o pedido corria: o monstro de id igual ao do servidor é de outra variante.
			Roster.Add(client, TestData.Database.Summons.Last());
			ServerActions.ApplyDelta(client, delta, baseline);
			Assert.Equal(null, SaveAudit.Check(client, server, false, Context(0.1)), "id trocado é remoção e adição, e nada cresceu");
		}

		[Test]
		private static void ArenaCycleResetAndLegitimateFightsPass()
		{
			var before = Player(1);
			before.Arena.Points = 800;
			before.Arena.Cycle = 3;
			var next = Copy(before);
			next.Arena.Cycle = 4;
			next.Arena.Points = 1000;
			Assert.Equal(null, Check(next, before), "o ciclo novo leva à pontuação inicial");

			var fights = Copy(before);
			fights.Arena.Invites = before.Arena.Invites - 3;
			fights.Arena.Points = before.Arena.Points + 90;
			Assert.Equal(null, Check(fights, before), "três lutas vencidas");
		}

		[Test]
		private static void UnchangedLegacyMonstersAreNeverRefused()
		{
			var before = Player(2);
			before.Monsters[0].Level = 99;
			Assert.Equal(null, Check(Copy(before), before), "igual ao guardado, mesmo fora da tabela");
		}

		[Test]
		private static void ForgedGrowthIsRefused()
		{
			var before = Player(2);
			before.Essence = 0;
			before.Fragments = 0;
			Roster.Add(before, TestData.Database.Summons.First(s => s.Rarity == 5));
			var target = before.Monsters.First();
			var summon = TestData.Database.Summon(target.SummonId);

			var level = Copy(before);
			level.Monsters[2].Level = Leveling.MaxLevel(level.Monsters[2]);
			Assert.Equal("monster_growth", Check(level, before), "nível sem Essência");

			var stars = Copy(before);
			stars.Monsters[0].Stars = Growth.MaxStars;
			stars.Monsters[0].Level = Growth.MaxLevel(Growth.MaxStars);
			Assert.True(Check(stars, before) is "monster_growth" or "monster_stars", "estrelas sem Fragmentos");

			var three = Roster.Add(before, TestData.Database.Summons.First(s => s.Rarity == 3));
			var starsOnly = Copy(before);
			var raised = starsOnly.Monster(three.Id)!;
			raised.Stars = Growth.MaxStars;
			Assert.Equal("monster_stars", Check(starsOnly, before), "estrelas sem Fragmentos, sem mexer no nível");

			var skills = Copy(before);
			skills.Monsters[0].SkillLevels = summon.AllSkills.Select(s => s.MaxLevel).ToList();
			Assert.Equal("monster_skills", Check(skills, before), "habilidades sem material");

			var awake = Copy(before);
			awake.Monsters[0].Awakened = true;
			Assert.Equal("monster_growth", Check(awake, before), "despertar sem Essência");

			var impossible = Copy(before);
			impossible.Monsters[0].Level = 400;
			Assert.Equal("monster_structure", Check(impossible, before), "nível que não existe");

			var arena = Copy(before);
			arena.Arena.Points = before.Arena.Points + 5000;
			Assert.Equal("arena_points", Check(arena, before), "pontos sem luta");
		}

		[Test]
		private static void NewMonstersStartFromTheNaturalBaseline()
		{
			var before = Player(1);
			before.Essence = 0;
			var next = Copy(before);
			var forged = Roster.Add(next, TestData.Database.Summons.First(s => s.Rarity == 3));
			forged.Stars = Growth.MaxStars;
			forged.Level = Growth.MaxLevel(Growth.MaxStars);
			Assert.True(Check(next, before) is "monster_growth" or "monster_stars", "monstro novo já no máximo");

			var honest = Copy(before);
			Roster.Add(honest, TestData.Database.Summons.First(s => s.Rarity == 3));
			Assert.Equal(null, Check(honest, before), "monstro novo na base");
		}

		[Test]
		private static void ReplacementAccountsGetOnlyTheStartAllowance()
		{
			var forged = Player(2);
			forged.Essence = 0;
			var top = Roster.Add(forged, TestData.Database.Summons.First(s => s.Rarity == 5));
			top.Stars = Growth.MaxStars;
			top.Level = Growth.MaxLevel(Growth.MaxStars);
			Assert.Equal("monster_growth", SaveAudit.Check(forged, null, true, Context()), "conta nova com monstro no máximo");

			var fresh = Player(2);
			Assert.Equal(null, SaveAudit.Check(fresh, null, true, Context()), "conta nova de verdade");
		}

		[Test]
		private static void ALegacySkillAboveTheCurrentCapStillLevelsUp()
		{
			var before = Player(1);
			var monster = before.Monsters[0];
			var cap = TestData.Database.Summon(monster.SummonId).AllSkills[0].MaxLevel;
			monster.SkillLevels = new() { cap + 3 };
			var next = Copy(before);
			Leveling.Infuse(next, next.Monsters[0], int.MaxValue);
			Assert.Equal(null, Check(next, before), "a habilidade que não mudou não é conferida de novo");
		}

		[Test]
		private static void WinsAreBoundedByTimeAndByMana()
		{
			var before = Player(1);
			Assert.True(MonsterAudit.FreeExperience(TestData.Database) > 0, "há recompensa de experiência no conteúdo");

			var quick = MonsterAudit.WinsPossible(Copy(before), before, Context(0.05));
			Assert.True(quick <= 6, $"três segundos dão poucas vitórias: {quick}");

			var long_ = MonsterAudit.WinsPossible(Copy(before), before, Context(600));
			Assert.True(long_ > quick, "mais tempo, mais vitórias");
			Assert.True(long_ <= 2L * Mana.Max(before) / 2 + 400, $"mas nunca mais que a Mana permite: {long_}");
		}

		[Test]
		private static void EveryFamilyReachesItsLegitimateCeiling()
		{
			foreach (var summon in TestData.Database.Summons)
			{
				var before = Player(0);
				before.Essence = 500_000_000;
				before.Fragments = 5_000;
				var owned = Roster.Add(before, summon);
				var next = Copy(before);
				var monster = next.Monster(owned.Id)!;
				while (monster.Stars < Growth.MaxStars)
				{
					Leveling.Infuse(next, monster, int.MaxValue);
					Evolution.Evolve(next, monster);
				}

				Leveling.Infuse(next, monster, int.MaxValue);
				var materials = 0;
				while (Fusion.Upgradable(TestData.Database, monster).Count > 0)
				{
					var index = Fusion.Upgradable(TestData.Database, monster)[0];
					monster.RaiseSkill(index);
					materials++;
				}

				if (Awakening.CanBeAwakened(summon))
					Awakening.Awaken(next, monster, summon);

				// Os materiais saem da coleção: monstros que existiam e sumiram.
				for (var i = 0; i < materials; i++)
					before.Monsters.Add(new OwnedSummon { Id = 10_000 + i, SummonId = summon.Id, Stars = summon.Rarity });

				Assert.Equal(null, Check(next, before), $"{summon.Id}: o teto legítimo passa");
			}
		}
	}
}
