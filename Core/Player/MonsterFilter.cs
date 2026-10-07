using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// A busca da grade de Monstros: elemento, papel, estrelas de agora, estrelas naturais, Despertar,
	/// situação (<see cref="MonsterCondition"/>), o que as habilidades fazem, em que escalam e que efeito põem
	/// (<see cref="SkillTraits"/>, na forma de agora do monstro), e a ordem. Campo nulo não filtra. Em qualquer ordem os
	/// favoritos vêm antes, e o empate cai nas estrelas, nas estrelas naturais, no nível, no elemento e
	/// na chegada.
	/// </summary>
	public sealed record MonsterFilter
	{
		public Element? Element { get; init; }
		public Role? Role { get; init; }

		/// <summary>As estrelas de agora, exatas.</summary>
		public int? Stars { get; init; }

		/// <summary>As estrelas naturais da variante, exatas.</summary>
		public int? Rarity { get; init; }

		public bool? Awakened { get; init; }
		public MonsterCondition? Condition { get; init; }
		public SkillBehavior? Behavior { get; init; }
		public SkillScaling? Scaling { get; init; }

		/// <summary>Um efeito de status que alguma habilidade põe.</summary>
		public StatusKind? Applies { get; init; }

		public MonsterSort Sort { get; init; } = MonsterSort.Stars;

		/// <summary>O atributo da ordem <see cref="MonsterSort.Stat"/>.</summary>
		public Stat SortStat { get; init; } = Stat.Speed;

		/// <summary>Quantos campos estão filtrando (a ordem não conta).</summary>
		public int Active => new object?[] { Element, Role, Stars, Rarity, Awakened, Condition, Behavior, Scaling, Applies }.Count(field => field != null);

		public bool Matches(OwnedSummon monster, SummonDefinition summon, PlayerState player) =>
			(Element == null || summon.Element == Element) &&
			(Role == null || summon.Role == Role) &&
			(Stars == null || monster.Stars == Stars) &&
			(Rarity == null || summon.Rarity == Rarity) &&
			(Awakened == null || monster.Awakened == Awakened) &&
			(Condition is not { } condition || Has(monster, player, condition)) &&
			(Behavior is not { } behavior || SkillTraits.BehaviorsOf(summon, monster.Awakened).Contains(behavior)) &&
			(Scaling is not { } scaling || SkillTraits.ScalingsOf(summon, monster.Awakened).Contains(scaling)) &&
			(Applies is not { } status || SkillTraits.StatusesOf(summon, monster.Awakened).Contains(status));

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
			MonsterCondition.InTeam => player.Teams.Values.Any(team => team.Contains(monster.Id)),
			MonsterCondition.MaxLevel => Leveling.IsMaxLevel(monster),
			MonsterCondition.Runed => player.Runes.Any(r => r.EquippedOn == monster.Id),
			_ => player.Runes.All(r => r.EquippedOn != monster.Id),
		};
	}
}
