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
	/// Inimigo que é invocação usa a mesma variante que o jogador invoca — os mesmos atributos do
	/// arquivo —, com as habilidades no nível 1, sem runas e sem Despertar: por isso Vida e Ataque vêm
	/// multiplicados (<see cref="FoeScale"/>), e depois pela força do encontro. Todo inimigo, comum ou
	/// chefe, ainda leva <see cref="FoeBoost"/> em Vida, Ataque e Defesa, e passa do 6★ nível 40 até o
	/// nível 60 (<see cref="Growth.FoeFraction"/>).
	/// </summary>
	public static class BattleFactory
	{
		/// <summary>
		/// Quanto Vida, Ataque e Defesa de todo inimigo da Campanha e das Masmorras são multiplicados. A
		/// Velocidade fica: 30% a mais nela tiraria o primeiro turno de qualquer equipe.
		/// </summary>
		public const double FoeBoost = 1.3;

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
			var allies = team.Members.Select(member => Ally(member, leader)).ToList();
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

		private static BattleUnit Ally(TeamMember member, LeaderDefinition? leader)
		{
			var summon = member.Summon;
			var sheet = SummonStats.For(summon, member.Stars, member.Level, member.Awakened, member.Runes.ToList());

			// A Liderança da primeira invocação vale para o time inteiro, sobre a base (não sobre as runas).
			var stats = sheet.TotalWith(leader);
			var (actives, passive) = Prepare(summon.SkillsFor(member.Awakened), member.SkillLevels, member.Awakened);

			return new BattleUnit(
				summon.Id,
				summon.NameFor(member.Awakened),
				summon.Image,
				Side.Allies,
				summon.Element,
				member.Level,
				member.Awakened,
				stats,
				actives,
				passive,
				sheet.Runes.Effects);
		}

		/// <summary>
		/// Um inimigo de <paramref name="encounter"/>, pronto para lutar: a tela da fase usa o mesmo para
		/// mostrar o resumo dele antes da luta (os números que o jogador vê são os que lutam).
		/// </summary>
		public static BattleUnit Foe(GameDatabase database, StageEnemy slot, Encounter encounter) =>
			slot.Summon is { } id ? SummonFoe(database.Summon(id), encounter) : EnemyFoe(database.Enemy(slot.Enemy!), slot.Element, encounter);

		private static IReadOnlyList<BattleUnit> Wave(GameDatabase database, IReadOnlyList<StageEnemy> slots, Encounter encounter)
		{
			var units = slots.Select(slot => Foe(database, slot, encounter)).ToList();
			foreach (var unit in units)
				unit.Team = units;
			return units;
		}

		private static BattleUnit SummonFoe(SummonDefinition summon, Encounter encounter)
		{
			var stats = Growth.FoeStats(summon.Stats, encounter.Stars, encounter.Level);
			var (health, attack) = FoeScale(summon.Rarity);
			stats = Boosted(stats with { Health = stats.Health * health * encounter.Scale, Attack = stats.Attack * attack * encounter.Scale });
			var (actives, passive) = Prepare(summon.Skills, System.Array.Empty<int>(), false);
			return new BattleUnit(
				summon.Id,
				summon.Name,
				summon.Image,
				Side.Enemies,
				summon.Element,
				encounter.Level,
				false,
				stats,
				actives,
				passive,
				RuneSetEffects.None);
		}

		private static BattleUnit EnemyFoe(EnemyDefinition enemy, Element element, Encounter encounter)
		{
			var stats = Growth.FoeStats(enemy.Stats, encounter.Stars, encounter.Level);
			stats = Boosted(stats with { Health = stats.Health * enemy.HealthScale * encounter.Scale, Attack = stats.Attack * enemy.AttackScale * encounter.Scale });
			var (actives, passive) = Prepare(enemy.Skills, System.Array.Empty<int>(), false);
			// Inimigo único é chefe: só o que não existe como invocação mora em Data/enemies.json.
			return new BattleUnit(
				enemy.Id,
				enemy.Name,
				enemy.Image,
				Side.Enemies,
				element,
				encounter.Level,
				false,
				stats,
				actives,
				passive,
				RuneSetEffects.None) { IsBoss = true };
		}

		/// <summary>A força a mais de todo inimigo (<see cref="FoeBoost"/>).</summary>
		private static StatBlock Boosted(StatBlock stats) => stats with
		{
			Health = stats.Health * FoeBoost,
			Attack = stats.Attack * FoeBoost,
			Defense = stats.Defense * FoeBoost,
		};
	}
}
