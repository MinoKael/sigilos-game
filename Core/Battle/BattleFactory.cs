using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Battle
{
	/// <summary>
	/// Monta uma <see cref="BattleSession"/> a partir de um time e de um <see cref="Encounter"/> (fase
	/// ou andar de Masmorra): atributos de cada invocação pela mesma ficha que a tela de Monstros mostra
	/// (<see cref="SummonStats"/>), mais a Liderança da primeira, e as ondas de inimigos no nível dele.
	///
	/// Inimigo que é invocação usa a mesma variante que o jogador invoca, no nível do encontro, sem
	/// runas, sem Éter e sem Despertar: por isso Vida e Ataque vêm multiplicados (<see cref="FoeScale"/>),
	/// e depois pela força do encontro.
	/// </summary>
	public static class BattleFactory
	{
		/// <summary>
		/// Quanto Vida e Ataque de invocação inimiga são multiplicados, por estrelas. A 4★ e a 5★ já nascem
		/// mais fortes (raridade e nível 1 maiores), então levam menos reforço que a 3★.
		/// </summary>
		public static (double Health, double Attack) FoeScale(int rarity) => rarity switch
		{
			>= 5 => (1.05, 0.8),
			4 => (1.4, 1.05),
			_ => (1.35, 1.05),
		};

		public static BattleSession Create(GameDatabase database, BattleTeam team, Encounter encounter, int seed)
		{
			var leader = team.Members.FirstOrDefault()?.Summon.Leader;
			var allies = team.Members.Select(member => Ally(database, member, leader)).ToList();
			foreach (var ally in allies)
				ally.Team = allies;

			var waves = encounter.Waves
				.Select(wave => Wave(database, wave, encounter.Level, encounter.Scale))
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
				member.Level,
				member.Awakened,
				stats,
				summon.Basic,
				summon.Special,
				summon.Family.Passive,
				Growth.SkillPower(member.Echoes),
				sheet.Runes.Effects);
		}

		private static IReadOnlyList<BattleUnit> Wave(GameDatabase database, IReadOnlyList<StageEnemy> slots, int level, double scale)
		{
			var units = slots
				.Select(slot => slot.Summon is { } id ? SummonFoe(database, database.Summon(id), level, scale) : EnemyFoe(database, slot, level, scale))
				.ToList();

			foreach (var unit in units)
				unit.Team = units;
			return units;
		}

		private static BattleUnit SummonFoe(GameDatabase database, SummonDefinition summon, int level, double scale)
		{
			var stats = Growth.Stats(database.Roles[summon.Role], summon.Rarity, level);
			var (health, attack) = FoeScale(summon.Rarity);
			stats = stats with { Health = stats.Health * health * scale, Attack = stats.Attack * attack * scale };
			return new BattleUnit(
				summon.Id,
				summon.Name,
				summon.ImageFor(false),
				Side.Enemies,
				summon.Element,
				level,
				false,
				stats,
				summon.Basic,
				summon.Special,
				summon.Family.Passive,
				1,
				RuneSetEffects.None);
		}

		private static BattleUnit EnemyFoe(GameDatabase database, StageEnemy slot, int level, double scale)
		{
			var enemy = database.Enemy(slot.Enemy!);
			var stats = Growth.Stats(database.Roles[enemy.Role], enemy.Rarity, level);
			stats = stats with { Health = stats.Health * enemy.HealthScale * scale, Attack = stats.Attack * enemy.AttackScale * scale };
			return new BattleUnit(
				enemy.Id,
				enemy.Name,
				enemy.Image,
				Side.Enemies,
				slot.Element,
				level,
				false,
				stats,
				enemy.Basic,
				enemy.Special,
				null,
				1,
				RuneSetEffects.None);
		}
	}
}
