using System;
using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O começo de uma conta: três Diabretes de Selo e Pergaminhos para a primeira invocação, que é
	/// uma 5★ garantida (GDD, seção 12: "Primeira 5★ na primeira sessão").
	/// </summary>
	public static class NewGame
	{
		public const int StartingScrolls = 10;

		public static readonly IReadOnlyList<string> StarterSummons = new[] { "diabrete_fogo", "diabrete_agua", "diabrete_luz" };

		/// <summary>Páginas de quem começa: uma por Glifo do time inicial (Estilhaço, Ossada, Olho) e um Círculo III.</summary>
		public static readonly IReadOnlyList<string> StarterPages = new[] { "dardo_arcano", "colheita", "presagio", "chuva_de_estilhacos" };

		public static PlayerState Create(DateTime now)
		{
			var player = new PlayerState
			{
				Scrolls = StartingScrolls,
				LastIdleCollect = now,
				Team = new List<string>(StarterSummons),
				Grimoire = new List<string>(StarterPages),
			};

			foreach (var id in StarterSummons)
				player.Collection[id] = 0;

			return player;
		}
	}
}
