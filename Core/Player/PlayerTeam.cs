using System.Linq;
using Sigilos.Core.Battle;
using Sigilos.Core.Content;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Traduz o save no que a batalha precisa. A batalha não conhece o <see cref="PlayerState"/>: o
	/// simulador dos testes monta um <see cref="BattleTeam"/> à mão, sem conta nenhuma.
	/// </summary>
	public static class PlayerTeam
	{
		public static BattleTeam Build(PlayerState player, GameDatabase database)
		{
			var members = player.Team
				.Where(database.HasSummon)
				.Where(player.Owns)
				.Select(id => new TeamMember(database.Summon(id), player.Echoes(id)))
				.ToList();

			var pages = player.Grimoire
				.Where(database.HasPage)
				.Select(database.Page)
				.ToList();

			return new BattleTeam(members, player.Level, database.Conjurer(player.ConjurerId), pages);
		}
	}
}
