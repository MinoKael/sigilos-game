using System;
using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O começo de uma conta: 60 Pergaminhos para a primeira invocação — uma 5★
	/// garantida (GDD, seção 12).
	/// </summary>
	public static class NewGame
	{
		public const int StartingScrolls = 60;
		public const int StartingDust = 300;
		public const int StarterRunes = 0;
		public const int StarterRuneGrade = 2;

		public static readonly IReadOnlyList<string> StarterSummons = [];

		public static PlayerState Create(DateTime now, Random random)
		{
			var player = new PlayerState
			{
				Version = PlayerState.CurrentVersion,
				Scrolls = StartingScrolls,
				Dust = StartingDust,
				LastIdleCollect = now,
				Team = [.. StarterSummons],
			};

			foreach (var id in StarterSummons)
				player.Summons[id] = new OwnedSummon();

			for (var i = 0; i < StarterRunes; i++)
				RuneInventory.Create(random, player, StarterRuneGrade);

			return player;
		}
	}
}
