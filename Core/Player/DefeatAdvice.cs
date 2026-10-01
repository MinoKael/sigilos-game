using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;
using Sigilos.Core.Progression;
using Sigilos.Core.Runes;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Um conselho depois da derrota: o que fazer e em quantos (monstros, runas, vagas). No de elemento,
	/// <paramref name="Element"/> é o que tem vantagem e <paramref name="Foe"/> o que mais aparece na luta.
	/// </summary>
	public sealed record DefeatTip(DefeatAdvice.Kind Kind, int Count, Element? Element = null, Element? Foe = null);

	/// <summary>
	/// O que a equipe que perdeu ainda pode fazer para ficar mais forte, na ordem em que mais rende: runas
	/// primeiro (as vagas vazias e as que ainda não passaram de +9), depois o nível, o elemento certo para
	/// a luta, a evolução e o Despertar. O resultado da derrota mostra os primeiros <see cref="Shown"/>.
	/// </summary>
	public static class DefeatAdvice
	{
		public enum Kind
		{
			/// <summary>Vagas de runa vazias na equipe.</summary>
			EquipRunes,

			/// <summary>Runas da equipe abaixo de <see cref="RuneTarget"/>.</summary>
			UpgradeRunes,

			/// <summary>Monstros abaixo do nível máximo das estrelas deles.</summary>
			LevelUp,

			/// <summary>Monstros em desvantagem contra o elemento que mais aparece na luta; <see cref="DefeatTip.Element"/> é o que tem vantagem.</summary>
			Element,

			/// <summary>Monstros no nível máximo que já podem evoluir.</summary>
			Evolve,

			/// <summary>Monstros ainda não despertos.</summary>
			Awaken,
		}

		public const int Shown = 3;

		/// <summary>Até aqui a runa é "nova": +9 é onde os subatributos já subiram três vezes.</summary>
		public const int RuneTarget = 9;

		public static IReadOnlyList<DefeatTip> For(PlayerState player, GameDatabase database, IReadOnlyList<OwnedSummon> team, Encounter encounter)
		{
			var tips = new List<DefeatTip>();
			var runes = team.SelectMany(m => player.RunesOn(m.Id)).ToList();

			Add(tips, Kind.EquipRunes, team.Count * RuneRules.Slots - runes.Count);
			Add(tips, Kind.UpgradeRunes, runes.Count(r => r.Level < RuneTarget));
			Add(tips, Kind.LevelUp, team.Count(m => !Leveling.IsMaxLevel(m)));

			var foes = encounter.Waves.SelectMany(wave => wave).Select(slot => database.Foe(slot).Element).ToList();
			if (foes.Count > 0)
			{
				var dominant = foes.GroupBy(e => e).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
				var counter = Counter(dominant);
				var weak = team.Count(m => ElementChart.HasAdvantage(dominant, database.Summon(m.SummonId).Element));
				if (weak > 0 && counter is { } element)
					tips.Add(new DefeatTip(Kind.Element, weak, element, dominant));
			}

			Add(tips, Kind.Evolve, team.Count(Evolution.IsReady));
			Add(tips, Kind.Awaken, team.Count(m => !m.Awakened));
			return tips.Take(Shown).ToList();
		}

		/// <summary>O elemento que tem vantagem contra <paramref name="element"/>.</summary>
		public static Element? Counter(Element element)
		{
			foreach (var candidate in System.Enum.GetValues<Element>())
			{
				if (ElementChart.HasAdvantage(candidate, element))
					return candidate;
			}

			return null;
		}

		private static void Add(List<DefeatTip> tips, Kind kind, int count)
		{
			if (count > 0)
				tips.Add(new DefeatTip(kind, count));
		}
	}
}
