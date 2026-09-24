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
				.Where(id => database.HasSummon(id) && player.Owns(id))
				.Select(id =>
				{
					var owned = player.Summon(id);
					return new TeamMember(database.Summon(id), owned.Level, owned.Echoes, owned.Awakened, player.RunesOn(id));
				})
				.ToList();

			return new BattleTeam(members);
		}
	}
}
