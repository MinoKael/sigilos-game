using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Content;
using Sigilos.Core.Player;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>
	/// O que as telas de monstros dizem antes de um monstro sumir (soltar, fundir): em que equipes ele
	/// está e o aviso quando a escolha leva alguém que deu trabalho.
	/// </summary>
	public static class MonsterNotes
	{
		/// <summary>Os nomes dos conteúdos em que o monstro está na equipe.</summary>
		public static List<string> Teams(GameDatabase database, PlayerState player, int monsterId) => player.Teams
			.Where(pair => pair.Value.Contains(monsterId))
			.Select(pair => pair.Key == Core.Player.Teams.Campaign ? T("teams.campaign") : database.Dungeons.FirstOrDefault(d => d.Id == pair.Key)?.Name ?? pair.Key)
			.ToList();

		/// <summary>Aviso (com a linha em branco antes) quando a escolha leva monstro desperto, evoluído, com nível, com habilidade subida, em equipe ou com runas.</summary>
		public static string Warning(GameDatabase database, PlayerState player, IEnumerable<OwnedSummon> monsters) =>
			monsters.Any(m => Valuable(database, player, m)) ? "\n\n" + T("monsters.valuable_warning") : "";

		public static bool Valuable(GameDatabase database, PlayerState player, OwnedSummon monster) =>
			monster.Awakened
			|| monster.Level > 1
			|| monster.Stars > database.Summon(monster.SummonId).Rarity
			|| monster.SkillLevels.Any(level => level > 1)
			|| Teams(database, player, monster.Id).Count > 0
			|| player.RunesOn(monster.Id).Count > 0;
	}
}
