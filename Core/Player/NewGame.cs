using System;
using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O começo de uma conta: três Diabretes de Selo, Pergaminhos para a primeira invocação — uma 5★
	/// garantida (GDD, seção 12) — e algumas runas de 2 estrelas para aprender a equipar.
	/// </summary>
	public static class NewGame
	{
		public const int StartingScrolls = 10;
		public const int StartingDust = 300;
		public const int StarterRunes = 6;
		public const int StarterRuneGrade = 2;

		public static readonly IReadOnlyList<string> StarterSummons = new[] { "diabrete_fogo", "diabrete_agua", "diabrete_luz" };

		public static PlayerState Create(DateTime now, Random random)
		{
			var player = new PlayerState
			{
				Version = PlayerState.CurrentVersion,
				Scrolls = StartingScrolls,
				Dust = StartingDust,
				LastIdleCollect = now,
				Team = new List<string>(StarterSummons),
			};

			foreach (var id in StarterSummons)
				player.Summons[id] = new OwnedSummon();

			for (var i = 0; i < StarterRunes; i++)
				RuneInventory.Create(random, player, StarterRuneGrade);

			return player;
		}
	}
}
