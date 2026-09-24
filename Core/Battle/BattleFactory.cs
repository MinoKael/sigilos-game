using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Monta uma <see cref="BattleSession"/> a partir de um time e de uma fase: atributos de cada
	/// invocação pela mesma ficha que a tela de Monstros mostra (<see cref="SummonStats"/>), mais a
	/// Liderança da primeira, e as ondas de inimigos no nível da fase.
	/// </summary>
	public static class BattleFactory
	{
		public static BattleSession Create(GameDatabase database, BattleTeam team, StageDefinition stage, int seed)
		{
			var leader = team.Members.FirstOrDefault()?.Summon.Leader;
			var allies = team.Members.Select(member => Ally(database, member, leader)).ToList();
			foreach (var ally in allies)
				ally.Team = allies;

			var waves = stage.Waves
				.Select(wave => Wave(database, wave, stage.Level))
				.ToList();

			return new BattleSession(allies, waves, seed);
		}

		private static BattleUnit Ally(GameDatabase database, TeamMember member, LeaderDefinition? leader)
		{
			var summon = member.Summon;
			var sheet = SummonStats.For(database.Roles[summon.Role], summon, member.Level, member.Echoes, member.Awakened, member.Runes.ToList());

			// A Liderança da primeira invocação vale para o time inteiro, sobre a base (não sobre as runas).
			var stats = sheet.TotalWith(leader);

			return new BattleUnit(
				summon.Id,
				summon.NameFor(member.Awakened),
				summon.ImageFor(member.Awakened),
				Side.Allies,
				summon.Element,
				summon.Glyph,
				member.Level,
				member.Awakened,
				stats,
				summon.Basic,
				summon.GlyphSkill,
				summon.Family.Passive,
				Growth.SkillPower(member.Echoes),
				sheet.Runes.Effects);
		}

		private static IReadOnlyList<BattleUnit> Wave(GameDatabase database, IReadOnlyList<StageEnemy> slots, int level)
		{
			var units = slots.Select(slot =>
			{
				var enemy = database.Enemy(slot.Enemy);
				var stats = Growth.Stats(database.Roles[enemy.Role], enemy.Rarity, level);
				stats = stats with { Health = stats.Health * enemy.HealthScale, Attack = stats.Attack * enemy.AttackScale };
				return new BattleUnit(
					enemy.Id,
					enemy.Name,
					enemy.Image,
					Side.Enemies,
					slot.Element,
					null,
					level,
					false,
					stats,
					enemy.Basic,
					enemy.GlyphSkill,
					null,
					1,
					RuneSetEffects.None);
			}).ToList();

			foreach (var unit in units)
				unit.Team = units;
			return units;
		}
	}
}
