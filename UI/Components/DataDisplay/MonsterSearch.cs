using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using Sigilos.Core.Progression;
using Sigilos.UI.Style;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Components
{
	/// <summary>
	/// A busca de monstros que as telas dividem (Monstros, a preparação da luta): o botão Filtros, que diz
	/// quantos campos filtram e abre a janela deles (<see cref="FilterDialog"/>), e a Ordem, com as por
	/// atributo no fim (<see cref="MonsterFilter"/>). Cada mudança chega inteira a quem pôs os botões.
	/// </summary>
	public static class MonsterSearch
	{
		/// <summary>Na lista da Ordem, as por atributo valem isto mais o atributo; as outras, a própria <see cref="MonsterSort"/>.</summary>
		private const int StatOrder = 100;

		/// <summary>Põe em <paramref name="bar"/> os botões Filtros e Ordem, com a busca de agora.</summary>
		public static void Fill(Container bar, GameDatabase database, MonsterFilter filter, Action<MonsterFilter> changed, float height = 44)
		{
			var active = filter.Active;
			bar.AddChild(GameButton.Of(active == 0 ? T("filter.button") : T("filter.button_active", active), () => Open(bar, database, filter, changed), active == 0 ? ButtonKind.Secondary : ButtonKind.Primary, "search", height).Named("Filters"));
			var current = filter.Sort == MonsterSort.Stat ? StatOrder + (int)filter.SortStat : (int)filter.Sort;
			var sort = new ChoiceButton(T("filter.sort"), SortOptions(), current, height) { Name = "Sort" };
			sort.Changed += value => changed(value >= StatOrder
				? filter with { Sort = MonsterSort.Stat, SortStat = (Stat)(value - StatOrder) }
				: filter with { Sort = (MonsterSort)value });
			bar.AddChild(sort);
		}

		/// <summary>As ordens da grade e, depois, uma por atributo.</summary>
		private static IReadOnlyList<(Choice Choice, int Value)> SortOptions() =>
			Enum.GetValues<MonsterSort>().Where(s => s != MonsterSort.Stat).Select(s => (new Choice(Texts.Name(s)), (int)s))
				.Concat(Enum.GetValues<Stat>().Select(s => (new Choice(Texts.Name(s).ToLower(Culture), Symbol: Texts.PlanetOf(s)), StatOrder + (int)s)))
				.ToList();

		/// <summary>A janela dos filtros: um campo por linha. Muda na hora.</summary>
		private static void Open(Control from, GameDatabase database, MonsterFilter filter, Action<MonsterFilter> changed)
		{
			var current = filter;
			FilterDialog.Open(from, T("filter.title_monsters"), dialog =>
			{
				// Cada campo aceita vários valores (fogo e luz): basta o monstro ser de um deles.
				dialog.MultiField("Element", T("filter.element"), Enum.GetValues<Element>().Select(e => (new Choice(Texts.Name(e), Art.Element(e), Palette.Of(e)), (int)e)), current.Elements.Select(e => (int)e),
					values => current = current with { Elements = values.Select(v => (Element)v).ToList() });
				dialog.MultiField("Role", T("filter.role"), RoleGroups.Representatives.Select(r => (new Choice(Texts.Name(r)), (int)r)),
					current.Roles.Select(role => (int)RoleGroups.Representatives.First(r => RoleGroups.Of(r) == RoleGroups.Of(role))).Distinct(),
					values => current = current with { Roles = values.Select(v => (Role)v).ToList() });
				dialog.MultiField("Stars", T("filter.stars"), Enumerable.Range(1, Growth.MaxStars).Select(s => (new Choice(Texts.Stars(s)), s)), current.Stars,
					values => current = current with { Stars = values });
				dialog.MultiField("Rarity", T("filter.natural"), database.Summons.Select(s => s.Rarity).Distinct().OrderBy(r => r).Select(r => (new Choice(Texts.Stars(r)), r)), current.Rarities,
					values => current = current with { Rarities = values });
				dialog.Field("Awakening", T("filter.awakening"), new[] { (new Choice(T("filter.awakened")), 1), (new Choice(T("filter.not_awakened")), 0) }, current.Awakened is { } awakened ? (awakened ? 1 : 0) : FilterDialog.All,
					value => current = current with { Awakened = value < 0 ? null : value == 1 });
				dialog.MultiField("Condition", T("filter.condition"), Enum.GetValues<MonsterCondition>().Select(c => (new Choice(T($"filter.condition_kind.{c}")), (int)c)), current.Conditions.Select(c => (int)c),
					values => current = current with { Conditions = values.Select(v => (MonsterCondition)v).ToList() });

				// O que as habilidades fazem: só as opções que algum monstro do jogo tem.
				dialog.MultiField("Behavior", T("filter.behavior"), SkillTraits.BehaviorsIn(database.Summons).Select(b => (new Choice(T($"filter.behavior_kind.{b}")), (int)b)), current.Behaviors.Select(b => (int)b),
					values => current = current with { Behaviors = values.Select(v => (SkillBehavior)v).ToList() });
				dialog.MultiField("Scaling", T("filter.scaling"), SkillTraits.ScalingsIn(database.Summons).Select(s => (new Choice(T($"filter.scaling_kind.{s}")), (int)s)), current.Scalings.Select(s => (int)s),
					values => current = current with { Scalings = values.Select(v => (SkillScaling)v).ToList() });
				dialog.MultiField("Applies", T("filter.applies"), SkillTraits.StatusesIn(database.Summons).Select(s => (new Choice(Texts.Name(s)), (int)s)), current.Applies.Select(s => (int)s),
					values => current = current with { Applies = values.Select(v => (StatusKind)v).ToList() });
			}, () => changed(current), () => current = new MonsterFilter { Sort = current.Sort, SortStat = current.SortStat });
		}
	}
}
