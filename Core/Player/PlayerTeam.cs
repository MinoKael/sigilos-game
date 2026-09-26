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
		/// <summary>A equipe do conteúdo (<see cref="Teams"/>), sem monstros que sumiram ou foram para o Baú.</summary>
		public static BattleTeam Build(PlayerState player, GameDatabase database, string content)
		{
			var members = Teams.Of(player, content)
				.Select(player.Monster)
				.Where(m => m is { Stored: false } && database.HasSummon(m.SummonId))
				.Select(m => new TeamMember(database.Summon(m!.SummonId), m.Level, m.Echoes, m.Awakened, player.RunesOn(m.Id)))
				.ToList();

			return new BattleTeam(members);
		}
	}
}
