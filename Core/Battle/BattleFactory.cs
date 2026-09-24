using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Monta uma <see cref="BattleSession"/> a partir de um time e de uma fase: calcula atributos
	/// (papel, raridade, nível, Ecos, Liderança), cria as ondas e o Grimório com custos e Ressonância.
	/// </summary>
	public static class BattleFactory
	{
		public static BattleSession Create(GameDatabase database, BattleTeam team, StageDefinition stage, int seed)
		{
			var allies = team.Members.Select(member => Ally(database, member, team)).ToList();
			foreach (var ally in allies)
				ally.Team = allies;

			var waves = stage.Waves
				.Select(wave => Wave(database, wave, stage.Level))
				.ToList();

			return new BattleSession(allies, waves, Conjurer(team), seed);
		}

		private static BattleUnit Ally(GameDatabase database, TeamMember member, BattleTeam team)
		{
			var summon = member.Summon;
			var stats = Growth.Stats(database.Roles[summon.Role], summon.Rarity, team.Level, member.Echoes);

			// A Liderança da primeira invocação vale para o time inteiro.
			var leader = team.Members.FirstOrDefault()?.Summon.Leader;
			if (leader != null)
				stats = WithLeader(stats, leader);

			return new BattleUnit(
				summon.Id,
				summon.Name,
				summon.Family.Image,
				Side.Allies,
				summon.Element,
				summon.Glyph,
				stats,
				summon.Basic,
				summon.GlyphSkill,
				summon.Family.Passive,
				Growth.SkillPower(member.Echoes));
		}

		private static IReadOnlyList<BattleUnit> Wave(GameDatabase database, IReadOnlyList<StageEnemy> slots, int level)
		{
			var units = slots.Select(slot =>
			{
				var enemy = database.Enemy(slot.Enemy);
				var stats = Growth.Stats(database.Roles[enemy.Role], enemy.Rarity, level);
				stats = stats with { Health = stats.Health * enemy.HealthScale };
				return new BattleUnit(
					enemy.Id,
					enemy.Name,
					enemy.Image,
					Side.Enemies,
					slot.Element,
					null,
					stats,
					enemy.Basic,
					enemy.GlyphSkill,
					null,
					1);
			}).ToList();

			foreach (var unit in units)
				unit.Team = units;
			return units;
		}

		private static ConjurerSeat Conjurer(BattleTeam team)
		{
			var glyphCounts = team.Members
				.GroupBy(m => m.Summon.Glyph)
				.ToDictionary(g => g.Key, g => g.Count());

			var pages = team.Pages
				.Take(team.Conjurer.PageSlots)
				.Select(page =>
				{
					var count = glyphCounts.TryGetValue(page.Glyph, out var n) ? n : 0;
					return new PageSlot(page, PageFormula.Cost(page, team.Conjurer, count), count > 0, PageFormula.Effects(page));
				})
				.ToList();

			return new ConjurerSeat(team.Conjurer, Growth.ConjurerPower(team.Conjurer.Power, team.Level), pages);
		}

		private static StatBlock WithLeader(StatBlock stats, LeaderDefinition leader)
		{
			var current = stats.Get(leader.Stat);
			var absolute = leader.Stat is Stat.Health or Stat.Attack or Stat.Defense or Stat.Speed;
			return stats.With(leader.Stat, absolute ? current * (1 + leader.Value) : current + leader.Value);
		}
	}
}
