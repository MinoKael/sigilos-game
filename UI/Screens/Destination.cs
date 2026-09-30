using System;
using System.Collections.Generic;
using System.Linq;
using Sigilos.Core.Player;
using static Sigilos.UI.Locale;

namespace Sigilos.UI.Screens
{
	/// <summary>Para onde um sigilo de navegação leva: os atalhos do Santuário, a Bolsa e o Mapa.</summary>
	public enum Destination
	{
		Campaign,
		Dungeons,
		Summon,
		Monsters,
		Runes,
		Teams,
		Shop,
		Compendium,
		Grimoire,
		Map,
		Bag,
	}

	/// <summary>O símbolo e o nome de cada destino, e os atalhos do Santuário guardados no save.</summary>
	public static class Destinations
	{
		/// <summary>Vagas na constelação do Santuário.</summary>
		public const int Slots = 7;

		/// <summary>Os atalhos de uma conta nova (e de quem nunca mexeu).</summary>
		public static readonly Destination?[] Defaults =
		{
			Destination.Summon, Destination.Campaign, Destination.Dungeons, Destination.Monsters,
			Destination.Runes, Destination.Teams, Destination.Compendium,
		};

		public static string Icon(Destination destination) => destination switch
		{
			Destination.Campaign => "campaign",
			Destination.Dungeons => "dungeon",
			Destination.Summon => "summon",
			Destination.Monsters => "monster",
			Destination.Runes => "rune",
			Destination.Teams => "team",
			Destination.Shop => "shop",
			Destination.Compendium => "compendium",
			Destination.Grimoire => "grimoire",
			Destination.Map => "map",
			_ => "bag",
		};

		public static string Name(Destination destination) => T($"destination.{destination}");

		/// <summary>Os atalhos da conta, vaga a vaga (nulo = vaga vazia).</summary>
		public static IReadOnlyList<Destination?> Shortcuts(PlayerState player)
		{
			if (player.Shortcuts.Count == 0)
				return Defaults;

			return Enumerable.Range(0, Slots)
				.Select(i => i < player.Shortcuts.Count && Enum.TryParse<Destination>(player.Shortcuts[i], out var destination) ? destination : (Destination?)null)
				.ToList();
		}

		/// <summary>O que o save guarda: o nome de cada destino, "" na vaga vazia.</summary>
		public static List<string> Save(IEnumerable<Destination?> shortcuts) => shortcuts.Select(d => d?.ToString() ?? "").ToList();
	}
}
