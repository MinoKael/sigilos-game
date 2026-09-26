using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// Uma equipe por conteúdo do jogo: a Campanha tem a dela e cada Masmorra a sua (a chave é o id da
	/// Masmorra em Data/dungeons.json). Até <see cref="PlayerState.TeamSize"/> monstros; a primeira é a
	/// Líder. Monstro no Baú não entra em equipe.
	/// </summary>
	public static class Teams
	{
		public const string Campaign = "campaign";

		public static IReadOnlyList<int> Of(PlayerState player, string content) =>
			player.Teams.TryGetValue(content, out var team) ? team : Array.Empty<int>();

		/// <summary>Tira o monstro se está na equipe; põe se há vaga. Devolve falso quando não mudou nada.</summary>
		public static bool Toggle(PlayerState player, string content, int monsterId)
		{
			var team = Get(player, content);
			if (team.Remove(monsterId))
				return true;

			if (team.Count >= PlayerState.TeamSize || player.Monster(monsterId) is not { Stored: false })
				return false;

			team.Add(monsterId);
			return true;
		}

		public static bool MakeLeader(PlayerState player, string content, int monsterId)
		{
			var team = Get(player, content);
			if (!team.Remove(monsterId))
				return false;

			team.Insert(0, monsterId);
			return true;
		}

		/// <summary>Tira o monstro de todas as equipes (foi para o Baú, fundido ou liberado).</summary>
		public static void Leave(PlayerState player, int monsterId)
		{
			foreach (var team in player.Teams.Values)
				team.Remove(monsterId);
		}

		/// <summary>Monstros novos entram na equipe da Campanha se ainda há vaga: a primeira luta não espera o jogador achar a tela de Equipes.</summary>
		public static void FillCampaign(PlayerState player, IEnumerable<OwnedSummon> monsters)
		{
			foreach (var monster in monsters.Where(m => !m.Stored))
			{
				var team = Get(player, Campaign);
				if (team.Count >= PlayerState.TeamSize)
					return;
				if (!team.Contains(monster.Id))
					team.Add(monster.Id);
			}
		}

		private static List<int> Get(PlayerState player, string content)
		{
			if (!player.Teams.TryGetValue(content, out var team))
			{
				team = new List<int>();
				player.Teams[content] = team;
			}

			return team;
		}
	}
}
