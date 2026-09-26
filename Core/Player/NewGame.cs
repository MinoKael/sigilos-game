using System;
using System.Collections.Generic;

namespace Sigilos.Core.Player
{
	/// <summary>
	/// O começo de uma conta: 60 Pergaminhos para as primeiras invocações — uma 5★ garantida (GDD,
	/// seção 12) —, Essência para os primeiros níveis, um pouco de Ouro e a Mana cheia.
	/// </summary>
	public static class NewGame
	{
		public const int StartingScrolls = 60;
		public const int StartingEssence = 3000;
		public const int StartingGold = 50;

		/// <summary>A Mana cheia de um nível 1 (Core/Progression/Mana: 60).</summary>
		public const int StartingMana = 60;
		public const int StarterRunes = 0;
		public const int StarterRuneGrade = 2;

		/// <summary>Variantes que a conta já traz, na equipe da Campanha.</summary>
		public static readonly IReadOnlyList<string> StarterSummons = [];

		public static PlayerState Create(DateTime now, Random random)
		{
			var player = new PlayerState
			{
				Version = PlayerState.CurrentVersion,
				Scrolls = StartingScrolls,
				Essence = StartingEssence,
				Gold = StartingGold,
				Mana = StartingMana,
				LastIdleCollect = now,
			};

			var starters = new List<OwnedSummon>();
			foreach (var id in StarterSummons)
				starters.Add(Roster.Add(player, id));
			Teams.FillCampaign(player, starters);

			for (var i = 0; i < StarterRunes; i++)
				RuneInventory.Create(random, player, StarterRuneGrade);

			return player;
		}
	}
}
