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
	/// (<see cref="SummonStats"/>), mais a Liderança da primeira, as habilidades no nível de cada uma, e
	/// as ondas de inimigos nas estrelas e no nível do encontro.
	///
	/// Inimigo que é invocação usa a mesma variante que o jogador invoca, com as habilidades no nível 1,
	/// sem runas e sem Despertar: por isso Vida e Ataque vêm multiplicados (<see cref="FoeScale"/>), e
	/// depois pela força do encontro.
	/// </summary>
	public static class BattleFactory
	{
		/// <summary>Quanto Vida e Ataque de invocação inimiga são multiplicados, pelas estrelas naturais.</summary>
		public static (double Health, double Attack) FoeScale(int rarity) => rarity switch
		{
			>= 5 => (1.2, 0.95),
			4 => (1.5, 1.15),
			_ => (1.5, 1.15),
		};

		public static BattleSession Create(GameDatabase database, BattleTeam team, Encounter encounter, int seed)
		{
			var leader = team.Members.FirstOrDefault()?.Summon.Leader;
			var allies = team.Members.Select(member => Ally(database, member, leader)).ToList();
			foreach (var ally in allies)
				ally.Team = allies;

			var waves = encounter.Waves
				.Select(wave => Wave(database, wave, encounter))
				.ToList();

			return new BattleSession(allies, waves, seed);
		}

		/// <summary>Separa as ativas (no nível e na versão certa) da Passiva.</summary>
		public static (IReadOnlyList<SkillDefinition> Actives, PassiveDefinition? Passive) Prepare(IReadOnlyList<SkillDefinition> skills, IReadOnlyList<int> levels, bool awakened)
		{
			var prepared = skills.Select((skill, i) => skill.At(i < levels.Count ? levels[i] : 1, awakened)).ToList();
			return (prepared.Where(s => !s.IsPassive).ToList(), prepared.FirstOrDefault(s => s.IsPassive)?.Passive);
		}

		private static BattleUnit Ally(GameDatabase database, TeamMember member, LeaderDefinition? leader)
		{
			var summon = member.Summon;
			var sheet = SummonStats.For(database.Roles[summon.Role], summon, member.Stars, member.Level, member.Awakened, member.Runes.ToList());

			// A Liderança da primeira invocação vale para o time inteiro, sobre a base (não sobre as runas).
			var stats = sheet.TotalWith(leader);
			var (actives, passive) = Prepare(summon.SkillsFor(member.Awakened), member.SkillLevels, member.Awakened);

			return new BattleUnit(
				summon.Id,
				summon.NameFor(member.Awakened),
				summon.ImageFor(member.Awakened),
				Side.Allies,
				summon.Element,
				member.Level,
				member.Awakened,
				stats,
				actives,
				passive,
				sheet.Runes.Effects);
		}

		private static IReadOnlyList<BattleUnit> Wave(GameDatabase database, IReadOnlyList<StageEnemy> slots, Encounter encounter)
		{
			var units = slots
				.Select(slot => slot.Summon is { } id ? SummonFoe(database, database.Summon(id), encounter) : EnemyFoe(database, slot, encounter))
				.ToList();

			foreach (var unit in units)
				unit.Team = units;
			return units;
		}

		private static BattleUnit SummonFoe(GameDatabase database, SummonDefinition summon, Encounter encounter)
		{
			var stats = Growth.Stats(database.Roles[summon.Role], summon.Rarity, encounter.Stars, encounter.Level);
			var (health, attack) = FoeScale(summon.Rarity);
			stats = stats with { Health = stats.Health * health * encounter.Scale, Attack = stats.Attack * attack * encounter.Scale };
			var (actives, passive) = Prepare(summon.Skills, System.Array.Empty<int>(), false);
			return new BattleUnit(
				summon.Id,
				summon.Name,
				summon.ImageFor(false),
				Side.Enemies,
				summon.Element,
				encounter.Level,
				false,
				stats,
				actives,
				passive,
				RuneSetEffects.None);
		}

		private static BattleUnit EnemyFoe(GameDatabase database, StageEnemy slot, Encounter encounter)
		{
			var enemy = database.Enemy(slot.Enemy!);
			var stats = Growth.Stats(database.Roles[enemy.Role], enemy.Rarity, encounter.Stars, encounter.Level);
			stats = stats with { Health = stats.Health * enemy.HealthScale * encounter.Scale, Attack = stats.Attack * enemy.AttackScale * encounter.Scale };
			var (actives, passive) = Prepare(enemy.Skills, System.Array.Empty<int>(), false);
			return new BattleUnit(
				enemy.Id,
				enemy.Name,
				enemy.Image,
				Side.Enemies,
				slot.Element,
				encounter.Level,
				false,
				stats,
				actives,
				passive,
				RuneSetEffects.None);
		}
	}
}
