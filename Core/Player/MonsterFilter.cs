using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// A busca da grade de Monstros: elementos, papéis, estrelas de agora, estrelas naturais, Despertar,
	/// situações (<see cref="MonsterCondition"/>), o que as habilidades fazem, em que escalam e que efeitos põem
	/// (<see cref="SkillTraits"/>, na forma de agora do monstro), e a ordem. Cada campo aceita vários valores
	/// (basta o monstro ter um deles) e campos diferentes valem juntos; campo nulo ou vazio não filtra. Em qualquer ordem os
	/// favoritos vêm antes, e o empate cai nas estrelas, nas estrelas naturais, no nível, no elemento e
	/// na chegada.
	/// </summary>
	public sealed record MonsterFilter
	{
		public IReadOnlyList<Element> Elements { get; init; } = Array.Empty<Element>();

		/// <summary>Os papéis; conta o grupo de cada um (<see cref="RoleGroups"/>).</summary>
		public IReadOnlyList<Role> Roles { get; init; } = Array.Empty<Role>();

		/// <summary>As estrelas de agora, exatas.</summary>
		public IReadOnlyList<int> Stars { get; init; } = Array.Empty<int>();

		/// <summary>As estrelas naturais da variante, exatas.</summary>
		public IReadOnlyList<int> Rarities { get; init; } = Array.Empty<int>();

		public bool? Awakened { get; init; }
		public IReadOnlyList<MonsterCondition> Conditions { get; init; } = Array.Empty<MonsterCondition>();
		public IReadOnlyList<SkillBehavior> Behaviors { get; init; } = Array.Empty<SkillBehavior>();
		public IReadOnlyList<SkillScaling> Scalings { get; init; } = Array.Empty<SkillScaling>();

		/// <summary>Efeitos de status que alguma habilidade põe (basta pôr um deles).</summary>
		public IReadOnlyList<StatusKind> Applies { get; init; } = Array.Empty<StatusKind>();

		public MonsterSort Sort { get; init; } = MonsterSort.Stars;

		/// <summary>O atributo da ordem <see cref="MonsterSort.Stat"/>.</summary>
		public Stat SortStat { get; init; } = Stat.Speed;

		/// <summary>Quantos campos estão filtrando (a ordem não conta; vários valores num campo contam um).</summary>
		public int Active =>
			new[] { Elements.Count, Roles.Count, Stars.Count, Rarities.Count, Conditions.Count, Behaviors.Count, Scalings.Count, Applies.Count }.Count(n => n > 0)
			+ (Awakened != null ? 1 : 0);

		public bool Matches(OwnedSummon monster, SummonDefinition summon, PlayerState player) =>
			(Elements.Count == 0 || Elements.Contains(summon.Element)) &&
			(Roles.Count == 0 || Roles.Any(role => RoleGroups.Of(role) == RoleGroups.Of(summon.Role))) &&
			(Stars.Count == 0 || Stars.Contains(monster.Stars)) &&
			(Rarities.Count == 0 || Rarities.Contains(summon.Rarity)) &&
			(Awakened == null || monster.Awakened == Awakened) &&
			(Conditions.Count == 0 || Conditions.Any(condition => Has(monster, player, condition))) &&
			(Behaviors.Count == 0 || SkillTraits.BehaviorsOf(summon, monster.Awakened).Intersect(Behaviors).Any()) &&
			(Scalings.Count == 0 || SkillTraits.ScalingsOf(summon, monster.Awakened).Intersect(Scalings).Any()) &&
			(Applies.Count == 0 || SkillTraits.StatusesOf(summon, monster.Awakened).Intersect(Applies).Any());

		/// <summary>Os monstros que passam (e que o jogo conhece), na ordem escolhida.</summary>
		public IEnumerable<OwnedSummon> Apply(IEnumerable<OwnedSummon> monsters, GameDatabase database, PlayerState player)
		{
			var matching = monsters
				.Where(m => database.HasSummon(m.SummonId))
				.Select(m => (Monster: m, Summon: database.Summon(m.SummonId)))
				.Where(x => Matches(x.Monster, x.Summon, player))
				.OrderByDescending(x => x.Monster.Favorite);
			var ordered = Sort switch
			{
				MonsterSort.Level => matching.ThenByDescending(x => x.Monster.Level),
				MonsterSort.Rarity => matching.ThenByDescending(x => x.Summon.Rarity),
				MonsterSort.Element => matching.ThenBy(x => x.Summon.Element),
				MonsterSort.Name => matching.ThenBy(x => x.Summon.NameFor(x.Monster.Awakened), StringComparer.CurrentCultureIgnoreCase),
				MonsterSort.Newest => matching.ThenByDescending(x => x.Monster.Id),
				MonsterSort.Stat => matching.ThenByDescending(x => Value(x.Monster, x.Summon, player, SortStat)),
				_ => matching,
			};

			return ordered
				.ThenByDescending(x => x.Monster.Stars)
				.ThenByDescending(x => x.Summon.Rarity)
				.ThenByDescending(x => x.Monster.Level)
				.ThenBy(x => x.Summon.Element)
				.ThenBy(x => x.Monster.Id)
				.Select(x => x.Monster);
		}

		/// <summary>O atributo do monstro com as runas: o mesmo número da ficha.</summary>
		public static double Value(OwnedSummon monster, SummonDefinition summon, PlayerState player, Stat stat) =>
			SummonStats.For(summon, monster.Stars, monster.Level, monster.Awakened, player.RunesOn(monster.Id)).Total.Get(stat);

		private static bool Has(OwnedSummon monster, PlayerState player, MonsterCondition condition) => condition switch
		{
			MonsterCondition.Favorite => monster.Favorite,
			MonsterCondition.Locked => monster.Locked,
			MonsterCondition.Unlocked => !monster.Locked,
			MonsterCondition.InTeam => player.Teams.Values.Any(team => team.Contains(monster.Id)),
			MonsterCondition.MaxLevel => Leveling.IsMaxLevel(monster),
			MonsterCondition.Runed => player.Runes.Any(r => r.EquippedOn == monster.Id),
			_ => player.Runes.All(r => r.EquippedOn != monster.Id),
		};
	}
}
