using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Tests
{
	/// <summary>
	/// Os times de referência da calibragem (GDD, seção 10): o time típico (<see cref="TestData.TypicalTeam"/>)
	/// no ponto em que um jogador estaria em cada fase, e os degraus depois da Campanha que cada andar de
	/// Masmorra pede. As runas são as que um jogador montaria: Fatal e Lâmina em quem ataca, Energia,
	/// Guarda e Escudo nos outros, com o principal certo em cada espaço.
	/// </summary>
	internal static class ReferenceTeams
	{
		/// <summary>
		/// Quem chega à fase: o nível sobe até 20 na fase 40 e fica (o fim da Campanha pede nível 20 com
		/// runas); as runas são as que a Campanha solta até ali (1★ a 4★), melhoradas aos poucos até +12.
		/// </summary>
		public static BattleTeam AtStage(GameDatabase database, int stage)
		{
			var level = (int)Math.Round(1 + 19 * Math.Min(1.0, (stage - 1) / 39.0));
			if (stage < 3)
				return Team(database, s => s.Rarity, level);

			var grade = stage < 8 ? 1 : stage < 13 ? 2 : stage < 18 ? 3 : 4;
			var runeLevel = Math.Min(12, 3 * ((stage - 1) / 8));
			var team = Team(database, s => s.Rarity, level, grade, runeLevel);
			var count = Math.Min(6, stage - 1);
			return new BattleTeam(team.Members.Select(m => m with { Runes = m.Runes.Take(count).ToList() }).ToList());
		}

		/// <summary>O time 6★ nível 40 sem runas: o outro jeito de terminar a Campanha.</summary>
		public static BattleTeam Bare(GameDatabase database) => Team(database, _ => 6, 40);

		/// <summary>
		/// Os degraus de depois da Campanha, do 1 (a fase 35) ao 10 (6★ nível 40, runas 6★ +15, Despertar e
		/// habilidades no máximo). Cada andar de Masmorra pede um degrau (Data/dungeons.json).
		/// </summary>
		public static BattleTeam Tier(GameDatabase database, int tier) => tier switch
		{
			1 => AtStage(database, 35),
			2 => AtStage(database, 45),
			3 => AtStage(database, 50),
			4 => Team(database, s => s.Rarity, 25, 5, 12),
			5 => Team(database, s => Math.Max(s.Rarity, 4), 30, 5, 12),
			6 => Team(database, _ => 5, 35, 5, 12),
			7 => Team(database, _ => 6, 40, 5, 12),
			8 => Team(database, _ => 6, 40, 6, 12),
			9 => Team(database, _ => 6, 40, 6, 15),
			_ => new BattleTeam(Team(database, _ => 6, 40, 6, 15).Members
				.Select(m => m with { Awakened = true, SkillLevels = m.Summon.AllSkills.Select(_ => 99).ToList() })
				.ToList()),
		};

		/// <summary>O degrau que cada andar pede, por Masmorra, do andar 1 ao 5.</summary>
		public static readonly IReadOnlyDictionary<string, int[]> FloorTiers = new Dictionary<string, int[]>
		{
			["golem"] = new[] { 1, 2, 3, 4, 5 },
			["wyvern"] = new[] { 2, 3, 4, 5, 6 },
			["crypt"] = new[] { 3, 4, 5, 6, 7 },
			["sanctum"] = new[] { 4, 5, 6, 7, 8 },
			["forge"] = new[] { 5, 6, 7, 9, 10 },
		};

		/// <summary>Quantas vezes o time vence o encontro, de 0 a 1.</summary>
		public static double WinRate(GameDatabase database, BattleTeam team, Encounter encounter, int fights)
		{
			var wins = 0;
			for (var seed = 1; seed <= fights; seed++)
			{
				if (AutoBattle.Run(BattleFactory.Create(database, team, encounter, seed * 7919)))
					wins++;
			}

			return wins / (double)fights;
		}

		private static BattleTeam Team(GameDatabase database, Func<SummonDefinition, int> stars, int level, int runeGrade = 0, int runeLevel = 0)
		{
			var random = new Random(7);
			var id = 1;
			return new BattleTeam(TestData.TypicalTeam.Select(summonId =>
			{
				var summon = database.Summon(summonId);
				var runes = runeGrade > 0 ? Build(random, summon, runeGrade, runeLevel, ref id) : new List<Rune>();
				return new TeamMember(summon, stars(summon), level, false, Array.Empty<int>(), runes);
			}).ToList());
		}

		private static List<Rune> Build(Random random, SummonDefinition summon, int grade, int level, ref int id)
		{
			var attack = summon.Role == Role.Attack;
			var percent = attack ? RuneStat.AttackPercent : RuneStat.HealthPercent;
			var sets = attack
				? new[] { RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Strike, RuneSet.Strike }
				: new[] { RuneSet.Vigor, RuneSet.Vigor, RuneSet.Ward, RuneSet.Ward, RuneSet.Bulwark, RuneSet.Bulwark };
			var mains = new[] { RuneStat.AttackFlat, attack ? RuneStat.AttackPercent : RuneStat.Speed, RuneStat.DefenseFlat, percent, RuneStat.HealthFlat, percent };
			var runes = new List<Rune>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				Rune rune;
				do
					rune = RuneForge.Generate(random, id++, grade, slot, new[] { sets[slot - 1] });
				while (rune.Main != mains[slot - 1]);
				while (rune.Level < level)
					RuneForge.RaiseLevel(random, rune);
				runes.Add(rune);
			}

			return runes;
		}
	}
}
