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
	///
	/// O guardião de uma constelação (<see cref="StageEnemy.Guardian"/>) é uma invocação que luta como
	/// chefe: desperta, com todas as habilidades no máximo e a Vida multiplicada por <see cref="GuardianHealth"/>.
	/// As regras da Influência da constelação (<see cref="Encounter.Rules"/>) entram em cada unidade do lado
	/// delas antes da primeira onda, e valem a luta inteira.
	/// </summary>
	public static class BattleFactory
	{
		/// <summary>
		/// Quanto Vida, Ataque e Defesa de todo inimigo da Campanha e das Masmorras são multiplicados. A
		/// Velocidade fica: 30% a mais nela tiraria o primeiro turno de qualquer equipe.
		/// </summary>
		public const double FoeBoost = 1.3;

		/// <summary>
		/// Quanto a Vida da invocação que guarda uma constelação é multiplicada, além do reforço de toda
		/// invocação inimiga: ela vale um chefe, não um inimigo comum.
		/// </summary>
		public const double GuardianHealth = 3.0;

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

			foreach (var rule in encounter.Rules ?? System.Array.Empty<InfluenceRule>())
			{
				var passive = rule.Prepared;
				foreach (var unit in Bearers(rule.Side, allies, waves))
					unit.AddInfluence(passive);
			}

			return new BattleSession(allies, waves, seed, encounter.Victory);
		}

		/// <summary>
		/// Um duelo da Arena: o time do jogador (com o bônus da sequência de vitórias, em frações de Vida e Ataque)
		/// contra a defesa de outro alquimista, uma onda só. A defesa luta como o jogador: sem o reforço
		/// de inimigo de Campanha (<see cref="FoeBoost"/>, <see cref="FoeScale"/>), com a Liderança da própria Líder.
		/// </summary>
		public static BattleSession CreateDuel(GameDatabase database, BattleTeam attacker, BattleTeam defender, int seed, double health = 0, double attack = 0)
		{
			var allies = attacker.Members.Select(member => Duelist(member, attacker.Members[0].Summon.Leader, Side.Allies, health, attack)).ToList();
			foreach (var ally in allies)
				ally.Team = allies;

			var foes = defender.Members.Select(member => Duelist(member, defender.Members[0].Summon.Leader, Side.Enemies, 0, 0)).ToList();
			foreach (var foe in foes)
				foe.Team = foes;

			return new BattleSession(allies, new List<IReadOnlyList<BattleUnit>> { foes }, seed, VictoryCondition.AllWaves);
		}

		/// <summary>Uma invocação de defesa da Arena pronta para lutar, para o resumo dela antes da luta.</summary>
		public static BattleUnit DefenderUnit(BattleTeam team, int index) =>
			Duelist(team.Members[index], team.Members[0].Summon.Leader, Side.Enemies, 0, 0);

		/// <summary>Quem leva uma regra da Influência: os inimigos de todas as ondas, só o guardião, o time do jogador ou todos.</summary>
		private static IEnumerable<BattleUnit> Bearers(InfluenceSide side, IReadOnlyList<BattleUnit> allies, IReadOnlyList<IReadOnlyList<BattleUnit>> waves)
		{
			var foes = waves.SelectMany(wave => wave);
			return side switch
			{
				InfluenceSide.Allies => allies,
				InfluenceSide.Guardian => foes.Where(unit => unit.IsBoss),
				InfluenceSide.Everyone => allies.Concat(foes),
				_ => foes,
			};
		}

		/// <summary>Separa as ativas (no nível e na versão certa) da Passiva.</summary>
		public static (IReadOnlyList<SkillDefinition> Actives, PassiveDefinition? Passive) Prepare(IReadOnlyList<SkillDefinition> skills, IReadOnlyList<int> levels, bool awakened)
		{
			var prepared = skills.Select((skill, i) => skill.At(i < levels.Count ? levels[i] : 1, awakened)).ToList();
			return (prepared.Where(s => !s.IsPassive).ToList(), prepared.FirstOrDefault(s => s.IsPassive)?.Passive);
		}

		private static BattleUnit Ally(TeamMember member, LeaderDefinition? leader) => Duelist(member, leader, Side.Allies, 0, 0);

		/// <summary>Uma invocação do jogador (ou de uma defesa da Arena) pronta para lutar, de qualquer lado.</summary>
		private static BattleUnit Duelist(TeamMember member, LeaderDefinition? leader, Side side, double health, double attack)
		{
			var summon = member.Summon;
			var sheet = SummonStats.For(summon, member.Stars, member.Level, member.Awakened, member.Runes.ToList());

			// A Liderança da primeira invocação vale para o time inteiro, sobre a base (não sobre as runas).
			var stats = sheet.TotalWith(leader);
			if (health != 0 || attack != 0)
				stats = stats with { Health = stats.Health * (1 + health), Attack = stats.Attack * (1 + attack) };
			var (actives, passive) = Prepare(summon.SkillsFor(member.Awakened), member.SkillLevels, member.Awakened);

			return new BattleUnit(
				summon.Id,
				summon.NameFor(member.Awakened),
				summon.Image,
				side,
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
			slot.Summon is { } id
				? slot.Guardian ? GuardianFoe(database.Summon(id), encounter) : SummonFoe(database.Summon(id), encounter)
				: EnemyFoe(database.Enemy(slot.Enemy!), slot.Element, encounter);

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
			stats = Pressed(Boosted(stats with { Health = stats.Health * health * encounter.Scale, Attack = stats.Attack * attack * encounter.Scale }), encounter);
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

		/// <summary>
		/// A invocação que guarda uma constelação: a forma desperta (atributos, bônus e nome do Despertar),
		/// todas as habilidades no nível máximo, e a Vida de chefe.
		/// </summary>
		private static BattleUnit GuardianFoe(SummonDefinition summon, Encounter encounter)
		{
			var stats = Awakening.Apply(Growth.FoeStats(summon.AwakenedStats, encounter.Stars, encounter.Level), summon.Awakening);
			var (health, attack) = FoeScale(summon.Rarity);
			stats = Pressed(Boosted(stats with
			{
				Health = stats.Health * health * GuardianHealth * encounter.Scale,
				Attack = stats.Attack * attack * encounter.Scale,
			}), encounter);
			var skills = summon.AllSkills;
			var (actives, passive) = Prepare(skills, skills.Select(skill => skill.MaxLevel).ToList(), true);
			return new BattleUnit(
				summon.Id,
				summon.Awakening.Name,
				summon.Image,
				Side.Enemies,
				summon.Element,
				encounter.Level,
				true,
				stats,
				actives,
				passive,
				RuneSetEffects.None) { IsBoss = true };
		}

		private static BattleUnit EnemyFoe(EnemyDefinition enemy, Element element, Encounter encounter)
		{
			var stats = Growth.FoeStats(enemy.Stats, encounter.Stars, encounter.Level);
			stats = Pressed(Boosted(stats with
			{
				Health = stats.Health * enemy.HealthScale * encounter.Scale,
				Attack = stats.Attack * enemy.AttackScale * encounter.Scale,
				Defense = stats.Defense * enemy.DefenseScale,
			}), encounter);
			var (actives, passive) = Prepare(enemy.Skills, System.Array.Empty<int>(), false);
			// Inimigo único é chefe (só o que não existe como invocação mora em Data/enemies.json), menos o lacaio.
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
				RuneSetEffects.None) { IsBoss = !enemy.Minion };
		}

		/// <summary>
		/// A pressão da fase (<see cref="Encounter.Attack"/> e <see cref="Encounter.Speed"/>): Ataque e Velocidade a
		/// mais conforme a Campanha avança. Vale 1 onde não se pede.
		/// </summary>
		private static StatBlock Pressed(StatBlock stats, Encounter encounter) =>
			encounter.Attack == 1 && encounter.Speed == 1
				? stats
				: stats with { Attack = stats.Attack * encounter.Attack, Speed = stats.Speed * encounter.Speed };

		/// <summary>A força a mais de todo inimigo (<see cref="FoeBoost"/>).</summary>
		private static StatBlock Boosted(StatBlock stats) => stats with
		{
			Health = stats.Health * FoeBoost,
			Attack = stats.Attack * FoeBoost,
			Defense = stats.Defense * FoeBoost,
		};
	}
}
