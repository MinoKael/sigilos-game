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
				.Concat(Enum.GetValues<Stat>().Select(s => (new Choice(Texts.Name(s).ToLower(Culture), Rune: Texts.GlyphOf(s)), StatOrder + (int)s)))
				.ToList();

		/// <summary>A janela dos filtros: um campo por linha. Muda na hora.</summary>
		private static void Open(Control from, GameDatabase database, MonsterFilter filter, Action<MonsterFilter> changed)
		{
			var current = filter;
			FilterDialog.Open(from, T("filter.title_monsters"), dialog =>
			{
				dialog.Field("Element", T("filter.element"), Enum.GetValues<Element>().Select(e => (new Choice(Texts.Name(e), Art.Element(e), Palette.Of(e)), (int)e)), current.Element is { } element ? (int)element : FilterDialog.All,
					value => current = current with { Element = value < 0 ? null : (Element)value });
				dialog.Field("Role", T("filter.role"), Enum.GetValues<Role>().Select(r => (new Choice(Texts.Name(r)), (int)r)), current.Role is { } role ? (int)role : FilterDialog.All,
					value => current = current with { Role = value < 0 ? null : (Role)value });
				dialog.Field("Stars", T("filter.stars"), Enumerable.Range(1, Growth.MaxStars).Select(s => (new Choice(Texts.Stars(s)), s)), current.Stars ?? FilterDialog.All,
					value => current = current with { Stars = value < 0 ? null : value });
				dialog.Field("Rarity", T("filter.natural"), database.Summons.Select(s => s.Rarity).Distinct().OrderBy(r => r).Select(r => (new Choice(Texts.Stars(r)), r)), current.Rarity ?? FilterDialog.All,
					value => current = current with { Rarity = value < 0 ? null : value });
				dialog.Field("Awakening", T("filter.awakening"), new[] { (new Choice(T("filter.awakened")), 1), (new Choice(T("filter.not_awakened")), 0) }, current.Awakened is { } awakened ? (awakened ? 1 : 0) : FilterDialog.All,
					value => current = current with { Awakened = value < 0 ? null : value == 1 });
				dialog.Field("Condition", T("filter.condition"), Enum.GetValues<MonsterCondition>().Select(c => (new Choice(T($"filter.condition_kind.{c}")), (int)c)), current.Condition is { } condition ? (int)condition : FilterDialog.All,
					value => current = current with { Condition = value < 0 ? null : (MonsterCondition)value });

				// O que as habilidades fazem: só as opções que algum monstro do jogo tem.
				dialog.Field("Behavior", T("filter.behavior"), SkillTraits.BehaviorsIn(database.Summons).Select(b => (new Choice(T($"filter.behavior_kind.{b}")), (int)b)), current.Behavior is { } behavior ? (int)behavior : FilterDialog.All,
					value => current = current with { Behavior = value < 0 ? null : (SkillBehavior)value });
				dialog.Field("Scaling", T("filter.scaling"), SkillTraits.ScalingsIn(database.Summons).Select(s => (new Choice(T($"filter.scaling_kind.{s}")), (int)s)), current.Scaling is { } scaling ? (int)scaling : FilterDialog.All,
					value => current = current with { Scaling = value < 0 ? null : (SkillScaling)value });
				dialog.Field("Applies", T("filter.applies"), SkillTraits.StatusesIn(database.Summons).Select(s => (new Choice(Texts.Name(s)), (int)s)), current.Applies is { } status ? (int)status : FilterDialog.All,
					value => current = current with { Applies = value < 0 ? null : (StatusKind)value });
			}, () => changed(current), () => current = new MonsterFilter { Sort = current.Sort, SortStat = current.SortStat });
		}
	}
}
