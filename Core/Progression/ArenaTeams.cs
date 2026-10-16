using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Progression
{
	/// <summary>
	/// As equipes de quatro que a Arena enfrenta, montadas por especificação (<see cref="ArenaTeamSpec"/>) e por uma
	/// semente: a mesma semente dá sempre a mesma equipe, então a lista não muda ao reabrir o jogo. Servem os
	/// adversários da lista (pelo Grau deles) e os Rivais (integrantes fixos).
	/// </summary>
	public static class ArenaTeams
	{
		public const int Size = 4;

		/// <summary>Quatro invocações de famílias diferentes, sorteadas pela semente.</summary>
		public static BattleTeam Defense(GameDatabase database, ArenaTeamSpec spec, Random random)
		{
			var picks = database.Summons
				.GroupBy(s => s.Family.Id)
				.OrderBy(g => g.Key, StringComparer.Ordinal)
				.Select(g => g.OrderBy(s => s.Id, StringComparer.Ordinal).ToList())
				.OrderBy(_ => random.Next())
				.Take(Size)
				.Select(variants => variants[random.Next(variants.Count)])
				.ToList();
			return Build(picks, spec, random);
		}

		/// <summary>A equipe fixa de um Rival, na ordem do arquivo (a primeira é a Líder).</summary>
		public static BattleTeam Rival(GameDatabase database, ArenaRival rival) =>
			Build(rival.Members.Select(database.Summon).ToList(), rival.Team, new Random(StableSeed(rival.Id)));

		private static int StableSeed(string text)
		{
			var hash = 17;
			foreach (var c in text)
				hash = unchecked(hash * 31 + c);
			return hash;
		}

		private static BattleTeam Build(IReadOnlyList<SummonDefinition> summons, ArenaTeamSpec spec, Random random)
		{
			var id = 1;
			return new BattleTeam(summons.Select(summon =>
			{
				var levels = summon.AllSkills.Select(s => 1 + (int)Math.Round((s.MaxLevel - 1) * Math.Clamp(spec.Skills, 0, 1))).ToList();
				var runes = spec.RuneGrade > 0 ? Runes(random, summon, spec.RuneGrade, spec.RuneLevel, ref id) : new List<Rune>();
				return new TeamMember(summon, spec.Stars, spec.Level, spec.Awakened, levels, runes);
			}).ToList());
		}

		/// <summary>
		/// Seis runas como um jogador monta: Fatal e Lâmina em quem ataca, Vigor e Sustento nos outros, com o
		/// principal que cada espaço aceita (Velocidade no 3, Ataque no 6).
		/// </summary>
		private static List<Rune> Runes(Random random, SummonDefinition summon, int grade, int level, ref int id)
		{
			var attack = summon.Role == Role.Attack;
			var sets = attack
				? new[] { RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Lethal, RuneSet.Strike, RuneSet.Strike }
				: new[] { RuneSet.Vigor, RuneSet.Vigor, RuneSet.Sustain, RuneSet.Sustain, RuneSet.Vigor, RuneSet.Vigor };
			var wanted = new[]
			{
				RuneStat.AttackFlat,
				attack ? RuneStat.AttackPercent : RuneStat.HealthPercent,
				RuneStat.Speed,
				RuneStat.DefenseFlat,
				RuneStat.HealthFlat,
				RuneStat.AttackFlat,
			};
			var runes = new List<Rune>();
			for (var slot = 1; slot <= RuneRules.Slots; slot++)
			{
				var main = RuneRules.MainOptions(slot).Contains(wanted[slot - 1]) ? wanted[slot - 1] : RuneRules.MainOptions(slot)[0];
				Rune rune;
				do
					rune = RuneForge.Generate(random, id++, grade, slot, new[] { sets[slot - 1] });
				while (rune.Main != main);
				while (rune.Level < level)
					RuneForge.RaiseLevel(random, rune);
				runes.Add(rune);
			}

			return runes;
		}
	}
}
