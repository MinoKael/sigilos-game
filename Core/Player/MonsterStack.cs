using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Cópias iguais do Baú num grupo só: mesma variante, estrelas, nível, Despertar e níveis de
	/// habilidade, e o mesmo bloqueio, favorito, o fato de ter ou não runas e de ser novo
	/// (<see cref="Roster.IsNew"/>). Basta uma dessas coisas mudar para a cópia ficar noutro grupo, então
	/// nenhuma ação sobre o grupo pisa num estado que só uma cópia tem. Só arruma a tela: as cópias seguem
	/// sendo monstros à parte.
	/// </summary>
	public sealed class MonsterStack
	{
		public MonsterStack(IReadOnlyList<OwnedSummon> copies) => Copies = copies;

		/// <summary>As cópias, na ordem em que chegaram à lista; a primeira representa o grupo.</summary>
		public IReadOnlyList<OwnedSummon> Copies { get; }

		public OwnedSummon First => Copies[0];
		public int Count => Copies.Count;

		/// <summary>Agrupa mantendo a ordem: cada grupo fica onde a primeira cópia dele estava.</summary>
		public static IReadOnlyList<MonsterStack> Group(IEnumerable<OwnedSummon> monsters, PlayerState player)
		{
			var runed = player.Runes.Where(r => r.EquippedOn != null).Select(r => r.EquippedOn!.Value).ToHashSet();
			return monsters
				.GroupBy(m => Key(m, runed.Contains(m.Id), Roster.IsNew(player, m)))
				.Select(group => new MonsterStack(group.ToList()))
				.ToList();
		}

		private static string Key(OwnedSummon monster, bool hasRunes, bool isNew)
		{
			var skills = monster.SkillLevels.Select(level => level < 1 ? 1 : level).ToList();
			while (skills.Count > 0 && skills[^1] == 1)
				skills.RemoveAt(skills.Count - 1);
			return string.Join('|', monster.SummonId, monster.Stars, monster.Level, monster.Awakened,
				string.Join('.', skills), monster.Locked, monster.Favorite, hasRunes, isNew);
		}
	}
}
