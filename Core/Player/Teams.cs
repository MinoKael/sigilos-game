using System;
using System.Collections.Generic;
using System.Linq;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// A última equipe usada em cada conteúdo do jogo: a Campanha tem a dela, a Exploração Estelar a dela
	/// e cada Masmorra a sua (a chave é o id da Masmorra em Data/dungeons.json). Não há equipes prontas: a
	/// preparação da luta abre com a do conteúdo, e o que se troca lá fica guardado. Até
	/// <see cref="PlayerState.TeamSize"/> monstros; a primeira é a Líder. Monstro no Baú não entra em equipe.
	/// </summary>
	public static class Teams
	{
		public const string Campaign = "campaign";

		/// <summary>A equipe da Exploração Estelar, a mesma nas 88 constelações.</summary>
		public const string Exploration = "exploration";

		/// <summary>A equipe que ataca na Arena Astral: quatro, escolhida na preparação de cada luta.</summary>
		public const string Arena = "arena";

		/// <summary>A defesa da Arena: quatro, editada no menu da Arena. Hoje só fica guardada (ninguém a ataca sem servidor).</summary>
		public const string ArenaDefense = "arena_defense";

		/// <summary>Quantos monstros a equipe do conteúdo leva: <see cref="PlayerState.MapTeamSize"/> nos mapas, <see cref="PlayerState.TeamSize"/> nas Masmorras.</summary>
		public static int SizeOf(string content) =>
			content is Campaign or Exploration or Arena or ArenaDefense ? PlayerState.MapTeamSize : PlayerState.TeamSize;

		/// <summary>
		/// A equipe de <paramref name="content"/> ainda vazia começa com a da Campanha (até o tamanho dela): quem
		/// abre a Arena pela primeira vez já tem com quem lutar. Devolve verdadeiro se copiou.
		/// </summary>
		public static bool SeedFromCampaign(PlayerState player, string content)
		{
			var team = Get(player, content);
			if (team.Count > 0)
				return false;

			var size = SizeOf(content);
			foreach (var id in Of(player, Campaign))
			{
				if (team.Count >= size)
					break;
				if (player.Monster(id) is { Stored: false, IsInfusionCore: false })
					team.Add(id);
			}

			return team.Count > 0;
		}

		/// <summary>Corta as equipes dos mapas ao tamanho de agora (saves de quando eram de cinco): ficam a Líder e os seguintes.</summary>
		public static void Trim(PlayerState player)
		{
			foreach (var (content, team) in player.Teams)
			{
				var size = SizeOf(content);
				if (team.Count > size)
					team.RemoveRange(size, team.Count - size);
			}
		}

		public static IReadOnlyList<int> Of(PlayerState player, string content) =>
			player.Teams.TryGetValue(content, out var team) ? team : Array.Empty<int>();

		/// <summary>Tira o monstro se está na equipe; põe se há vaga. Devolve falso quando não mudou nada.</summary>
		public static bool Toggle(PlayerState player, string content, int monsterId)
		{
			var team = Get(player, content);
			if (team.Remove(monsterId))
				return true;

			if (team.Count >= SizeOf(content) || player.Monster(monsterId) is not { Stored: false, IsInfusionCore: false })
				return false;

			team.Add(monsterId);
			return true;
		}

		/// <summary>
		/// <paramref name="incoming"/> entra na vaga de <paramref name="outgoing"/>; se já está na equipe, os
		/// dois trocam de lugar. Devolve falso quando não mudou nada (quem sai não está na equipe, ou quem
		/// entra não pode lutar).
		/// </summary>
		public static bool Replace(PlayerState player, string content, int outgoing, int incoming)
		{
			var team = Get(player, content);
			var place = team.IndexOf(outgoing);
			if (place < 0 || outgoing == incoming)
				return false;

			var other = team.IndexOf(incoming);
			if (other >= 0)
			{
				(team[place], team[other]) = (incoming, outgoing);
				return true;
			}

			if (player.Monster(incoming) is not { Stored: false, IsInfusionCore: false })
				return false;

			team[place] = incoming;
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

		/// <summary>Tira o monstro de todas as equipes (foi para o Baú, fundido ou solto).</summary>
		public static void Leave(PlayerState player, int monsterId)
		{
			foreach (var team in player.Teams.Values)
				team.Remove(monsterId);
		}

		/// <summary>Monstros novos entram na equipe da Campanha se ainda há vaga: a primeira luta já começa com eles na preparação.</summary>
		public static void FillCampaign(PlayerState player, IEnumerable<OwnedSummon> monsters)
		{
			foreach (var monster in monsters.Where(m => !m.Stored && !m.IsInfusionCore))
			{
				var team = Get(player, Campaign);
				if (team.Count >= SizeOf(Campaign))
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
